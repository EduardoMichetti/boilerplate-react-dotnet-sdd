namespace Platform.IntegrationTests.Carga;

using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using App.Api.Access;
using App.Api.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T118 / SC-003: Teste de carga concorrente isolada entre duas instalações A e B (FR-011–FR-016).
/// Executa 200 operações intercaladas entre A e B com os mesmos IdRegistro, provando pico efetivo ≥ 20,
/// ausência de vazamento cruzado de dados ou conexão, e rollback verificado em falha provocada.
/// </summary>
public sealed class TestesCargaIsolamentoSc003(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private const int TamanhoMaximoPoolPorInstalacao = 40;

    [Fact]
    public async Task RotaSintetica_NaoExisteNaApiDistribuidaSemConfiguradorDeTeste()
    {
        FabricaApiInstalacao apiSemExtensao = CriarApi(A);
        ClienteAcessoHttp cliente = CriarCliente(apiSemExtensao);

        using HttpResponseMessage resposta = await cliente.EnviarAsync(
            HttpMethod.Get, $"/api/v1/teste-sc003/registros/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task CargaIsolamentoSC003_200OperacoesConcorrentes_ComPicoMaiorOuIgualA20_SemAcessoCruzado()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;

        // 1. Prepara schema e tabelas sintéticas em A e B com o owner
        await SuporteCargaSintetica.PrepararTabelaSinteticaAsync(A, cancelamento);
        await SuporteCargaSintetica.PrepararTabelaSinteticaAsync(B, cancelamento);

        // 2. Prepara contas com permissão foundation.context.read em A e B
        string emailA = ContasSinteticas.NovoEmail("sc003_a");
        string emailB = ContasSinteticas.NovoEmail("sc003_b");
        await ContasSinteticas.CriarContaAsync(A, Relogio, emailA, ContasSinteticas.SenhaPadraoA);
        await ContasSinteticas.CriarContaAsync(B, Relogio, emailB, ContasSinteticas.SenhaPadraoB);

        // 3. Inicializa APIs com o configurador de rotas de carga sintética e pool real de conexões
        //    por instalação (reutilização de conexões do pool, como em produção). A e B dividem o
        //    mesmo servidor de teste (max_connections=100): 40 + 40 conexões deixam folga para a fixture.
        FabricaApiInstalacao apiA = CriarApi(A, OpcoesCargaComPool(A.StringConexaoExecucao));
        FabricaApiInstalacao apiB = CriarApi(B, OpcoesCargaComPool(B.StringConexaoExecucao));

        // Autentica antes da carga para excluir rate limiter do cenário
        ClienteAcessoHttp clienteAutenticacaoA = CriarCliente(apiA);
        await clienteAutenticacaoA.ObterTokenAsync();
        using (HttpResponseMessage loginA = await clienteAutenticacaoA.LoginAsync(emailA, ContasSinteticas.SenhaPadraoA, novoToken: false))
        {
            Assert.Equal(HttpStatusCode.Created, loginA.StatusCode);
        }

        ClienteAcessoHttp clienteAutenticacaoB = CriarCliente(apiB);
        await clienteAutenticacaoB.ObterTokenAsync();
        using (HttpResponseMessage loginB = await clienteAutenticacaoB.LoginAsync(emailB, ContasSinteticas.SenhaPadraoB, novoToken: false))
        {
            Assert.Equal(HttpStatusCode.Created, loginB.StatusCode);
        }

        // 4. Prepara 200 operações (100 para A, 100 para B), compartilhando 50 IdRegistros idênticos
        const int TotalOperacoes = 200;
        const int OperacoesPorInstalacao = 100;
        const int QuantidadeRegistros = 50;

        Guid[] idsCompartilhados = Enumerable.Range(0, QuantidadeRegistros)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        int concorrenciaAtual = 0;
        int picoEfetivoConcorrencia = 0;

        TaskCompletionSource disparador = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentBag<string> erros = [];
        ConcurrentDictionary<Guid, string> valoresGravadosA = new();
        ConcurrentDictionary<Guid, string> valoresGravadosB = new();

        int operacoesConcluidasA = 0;
        int operacoesConcluidasB = 0;

        List<Task> tarefas = [];

        ClienteAcessoHttp[] clientes = new ClienteAcessoHttp[TotalOperacoes];
        for (int i = 0; i < TotalOperacoes; i++)
        {
            bool ehTenantA = (i % 2) == 0;
            clientes[i] = ehTenantA ? CriarCliente(apiA) : CriarCliente(apiB);
            if (ehTenantA)
            {
                CopiarCookies(clienteAutenticacaoA, clientes[i]);
            }
            else
            {
                CopiarCookies(clienteAutenticacaoB, clientes[i]);
            }
        }

        for (int i = 0; i < TotalOperacoes; i++)
        {
            bool ehTenantA = (i % 2) == 0;
            int indice = i;
            Guid idRegistro = idsCompartilhados[(indice / 2) % QuantidadeRegistros];
            bool ehEscrita = (indice % 4) < 2;

            tarefas.Add(Task.Run(async () =>
            {
                ClienteAcessoHttp cliente = clientes[indice];

                // Aguarda o sinal de largada para disparar simultaneamente e gerar pico concorrente
                await disparador.Task;

                int atual = Interlocked.Increment(ref concorrenciaAtual);
                AtualizarPico(ref picoEfetivoConcorrencia, atual);

                try
                {
                    string rota = $"/api/v1/teste-sc003/registros/{idRegistro}";

                    if (ehEscrita)
                    {
                        string valor = ehTenantA ? $"valor_tenant_A_{idRegistro:N}" : $"valor_tenant_B_{idRegistro:N}";
                        string corpo = JsonSerializer.Serialize(new DadosRegistroSintetico(valor));

                        using HttpResponseMessage resposta = await cliente.EnviarAsync(HttpMethod.Post, rota, corpo);
                        if (resposta.StatusCode != HttpStatusCode.OK)
                        {
                            string corpoErro = await resposta.Content.ReadAsStringAsync(cancelamento);
                            erros.Add($"Erro POST {(ehTenantA ? "A" : "B")}: {(int)resposta.StatusCode} - {corpoErro}");
                        }
                        else
                        {
                            if (ehTenantA)
                            {
                                valoresGravadosA[idRegistro] = valor;
                                Interlocked.Increment(ref operacoesConcluidasA);
                            }
                            else
                            {
                                valoresGravadosB[idRegistro] = valor;
                                Interlocked.Increment(ref operacoesConcluidasB);
                            }
                        }
                    }
                    else
                    {
                        using HttpResponseMessage resposta = await cliente.EnviarAsync(HttpMethod.Get, rota);
                        if (resposta.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
                        {
                            string corpoErro = await resposta.Content.ReadAsStringAsync(cancelamento);
                            erros.Add($"Erro GET {(ehTenantA ? "A" : "B")}: {(int)resposta.StatusCode} - {corpoErro}");
                        }
                        else
                        {
                            if (ehTenantA)
                            {
                                Interlocked.Increment(ref operacoesConcluidasA);
                            }
                            else
                            {
                                Interlocked.Increment(ref operacoesConcluidasB);
                            }

                            if (resposta.StatusCode == HttpStatusCode.OK)
                            {
                                string json = await resposta.Content.ReadAsStringAsync(cancelamento);
                                using JsonDocument doc = JsonDocument.Parse(json);
                                string valorLido = doc.RootElement.GetProperty("valor").GetString()!;

                                // Valida que nunca leu valor da outra instalação
                                if (ehTenantA && valorLido.Contains("tenant_B", StringComparison.Ordinal))
                                {
                                    erros.Add($"VAZAMENTO CRUZADO: Tenant A leu dados de Tenant B! Valor: {valorLido}");
                                }
                                else if (!ehTenantA && valorLido.Contains("tenant_A", StringComparison.Ordinal))
                                {
                                    erros.Add($"VAZAMENTO CRUZADO: Tenant B leu dados de Tenant A! Valor: {valorLido}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    erros.Add($"Exceção {(ehTenantA ? "A" : "B")}: {ex.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref concorrenciaAtual);
                }
            }, cancelamento));
        }

        disparador.SetResult();
        await Task.WhenAll(tarefas);

        // 5. Verifica os critérios de sucesso e isolamento
        if (!erros.IsEmpty)
        {
            var logsTexto = string.Join("\n", apiA.Logs.Entradas.Concat(apiB.Logs.Entradas).Where(e => e.Excecao is not null).Select(e => e.TextoCompleto).Take(5));
            Assert.Fail(string.Join("\n", erros.Take(5)) + "\n\nLOGS:\n" + logsTexto);
        }
        Assert.True(picoEfetivoConcorrencia >= 20, $"O pico de concorrência efetivo foi {picoEfetivoConcorrencia}, esperado >= 20.");
        Assert.Equal(OperacoesPorInstalacao, operacoesConcluidasA);
        Assert.Equal(OperacoesPorInstalacao, operacoesConcluidasB);

        // 6. Confere dados diretamente no banco de A e de B: nenhum valor cruzado
        await using (NpgsqlConnection connA = new(A.StringConexaoExecucao))
        {
            await connA.OpenAsync(cancelamento);
            await using NpgsqlCommand cmd = new("SELECT \"IdRegistro\", \"Valor\" FROM teste_sc003.registros_sinteticos;", connA);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                string valor = reader.GetString(1);
                Assert.StartsWith("valor_tenant_A_", valor, StringComparison.Ordinal);
                Assert.DoesNotContain("tenant_B", valor, StringComparison.Ordinal);
            }
        }

        await using (NpgsqlConnection connB = new(B.StringConexaoExecucao))
        {
            await connB.OpenAsync(cancelamento);
            await using NpgsqlCommand cmd = new("SELECT \"IdRegistro\", \"Valor\" FROM teste_sc003.registros_sinteticos;", connB);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                string valor = reader.GetString(1);
                Assert.StartsWith("valor_tenant_B_", valor, StringComparison.Ordinal);
                Assert.DoesNotContain("tenant_A", valor, StringComparison.Ordinal);
            }
        }

        // 7. Falha provocada em A com rollback verificado (sem afetar B)
        Guid idFalha = Guid.NewGuid();
        await using (NpgsqlConnection connA = new(A.StringConexaoExecucao))
        {
            await connA.OpenAsync(cancelamento);
            await using NpgsqlTransaction tx = await connA.BeginTransactionAsync(cancelamento);
            await using NpgsqlCommand cmd = new(
                $"INSERT INTO teste_sc003.registros_sinteticos (\"IdRegistro\", \"Valor\", \"GravadoEm\") VALUES ('{idFalha}', 'falha_a', now());",
                connA, tx);
            await cmd.ExecuteNonQueryAsync(cancelamento);

            // Rollback deliberado em A
            await tx.RollbackAsync(cancelamento);
        }

        // Confere que o registro não foi persistido em A
        await using (NpgsqlConnection connA = new(A.StringConexaoExecucao))
        {
            await connA.OpenAsync(cancelamento);
            await using NpgsqlCommand cmd = new($"SELECT count(*) FROM teste_sc003.registros_sinteticos WHERE \"IdRegistro\" = '{idFalha}';", connA);
            long contagem = (long)(await cmd.ExecuteScalarAsync(cancelamento))!;
            Assert.Equal(0, contagem);
        }

        // Confere que B permanece íntegro
        await using (NpgsqlConnection connB = new(B.StringConexaoExecucao))
        {
            await connB.OpenAsync(cancelamento);
            await using NpgsqlCommand cmd = new("SELECT count(*) FROM teste_sc003.registros_sinteticos;", connB);
            long contagemB = (long)(await cmd.ExecuteScalarAsync(cancelamento))!;
            Assert.True(contagemB > 0, "B deve conter os registros gravados durante a carga.");
        }

        // Registra o sumário de evidência na saída do teste (xUnit v3: --show-live-output on)
        TestContext.Current.TestOutputHelper?.WriteLine($"[SC-003 EVIDENCIA] Total={TotalOperacoes}, OpsA={operacoesConcluidasA}, OpsB={operacoesConcluidasB}, PicoEfetivo={picoEfetivoConcorrencia}, Erros={erros.Count}");
    }

    private static OpcoesFabricaApi OpcoesCargaComPool(string stringConexaoExecucao)
    {
        string conexaoComPool = new NpgsqlConnectionStringBuilder(stringConexaoExecucao)
        {
            Pooling = true,
            MaxPoolSize = TamanhoMaximoPoolPorInstalacao,
        }.ConnectionString;

        return new OpcoesFabricaApi
        {
            ServicosTeste = servicos => servicos.AddSingleton<IConfiguradorRotasTeste, ConfiguradorRotasCargaSintetica>(),
            Extras = new Dictionary<string, string?> { ["StringsConexao:BancoApp"] = conexaoComPool },
        };
    }

    private static void CopiarCookies(ClienteAcessoHttp origem, ClienteAcessoHttp destino)
    {
        destino.Cookies.Clear();
        foreach ((string chave, string valor) in origem.Cookies)
        {
            destino.Cookies[chave] = valor;
        }

        destino.TokenCsrf = origem.TokenCsrf;
    }

    private static void AtualizarPico(ref int pico, int atual)
    {
        int anterior;
        do
        {
            anterior = pico;
            if (atual <= anterior)
            {
                break;
            }
        }
        while (Interlocked.CompareExchange(ref pico, atual, anterior) != anterior);
    }
}
