namespace Platform.IntegrationTests.Http;

using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.Application.Access.Operacoes;
using Platform.Application.Access.Portas;
using Platform.Application.Configuration;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Resultados;
using Platform.Domain.Tempo;
using Platform.Infrastructure.Access;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Platform.IntegrationTests.Suporte;
using Xunit;

/// <summary>
/// T092: troca atômica com falha e disputa (barreiras determinísticas), Renew × logout sem
/// ressurreição, lockout pelo relógio de teste com revogação atômica e sem contagem perdida.
/// </summary>
public sealed class TestesConcorrenciaSessaoLockout(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private const string SenhaErrada = "SenhaErrada#2026x";

    [Fact]
    public async Task FalhaNaTrocaDoLogin_Retorna503_SemSessaoNova_AnteriorValidaComMesmoPrazo()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("trocafalha");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        SessaoAutenticacao anterior = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));

        Relogio.Avancar(TimeSpan.FromMinutes(3));
        api.Falhas.FalharProximosCommits(1);
        using (HttpResponseMessage login = await cliente.LoginAsync(email, ContasSinteticas.SenhaPadraoA))
        {
            await AssegurarProblemaAsync(login, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
            Assert.DoesNotContain(cliente.UltimoSetCookie, c => c.StartsWith("bps-session=", StringComparison.Ordinal));
        }

        SessaoAutenticacao depois = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(anterior.IdSessao, depois.IdSessao);
        Assert.Null(depois.RevogadaEm);
        Assert.Equal(anterior.TetoAbsolutoEm, depois.TetoAbsolutoEm);
        Assert.Equal(anterior.Versao, depois.Versao);
        await cliente.ObterTokenAsync();
        using HttpResponseMessage me = await cliente.MeAsync();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task DoisLoginsComAMesmaSessaoAnterior_Um201EUm409SemCookie()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("disputa");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp outroJar = CriarCliente(api);
        await outroJar.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp primeiro = CriarCliente(api);
        await primeiro.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp segundo = CriarCliente(api);
        foreach ((string nome, string valor) in primeiro.Cookies)
        {
            segundo.Cookies[nome] = valor;
        }

        segundo.TokenCsrf = primeiro.TokenCsrf;

        // Barreira: as duas trocas iniciam a transação (já autenticadas com a mesma versão) antes de prosseguir.
        Encontro encontro = new(2);
        api.Falhas.AoIniciarTransacao = encontro.ChegarAsync;
        string corpo = System.Text.Json.JsonSerializer.Serialize(new { email, password = ContasSinteticas.SenhaPadraoA });
        HttpResponseMessage[] respostas = await Task.WhenAll(
            primeiro.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, corpo),
            segundo.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, corpo));
        api.Falhas.AoIniciarTransacao = null;

        try
        {
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], respostas.Select(r => r.StatusCode).Order().ToArray());
            HttpResponseMessage perdedor = respostas.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            await AssegurarProblemaAsync(perdedor, HttpStatusCode.Conflict, "SESSION_CONFLICT");
            Assert.False(perdedor.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
                && cookies.Any(c => c.StartsWith("bps-session=", StringComparison.Ordinal)));
        }
        finally
        {
            foreach (HttpResponseMessage resposta in respostas)
            {
                resposta.Dispose();
            }
        }

        // Sessões: outro jar + anterior (revogada) + exatamente uma nova.
        List<SessaoAutenticacao> sessoes = await ContasSinteticas.SessoesAsync(A, id);
        Assert.Equal(3, sessoes.Count);
        Assert.Equal(2, sessoes.Count(s => s.RevogadaEm is null));
        using (HttpResponseMessage meOutro = await outroJar.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, meOutro.StatusCode);
        }

        // Perdedor repete com novo GET antiforgery e consegue.
        ClienteAcessoHttp jarPerdedor = respostas[0].StatusCode == HttpStatusCode.Conflict ? primeiro : segundo;
        using HttpResponseMessage repeticao = await jarPerdedor.LoginAsync(email, ContasSinteticas.SenhaPadraoA);
        Assert.Equal(HttpStatusCode.Created, repeticao.StatusCode);
    }

    [Fact]
    public async Task RenewELogoutConcorrentes_SemRessurreicaoNemExtensao()
    {
        string email = ContasSinteticas.NovoEmail("renew");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ContextoOperacaoTeste operacao = ContextoOperacaoTeste.Criar(Relogio, TipoAutor.Usuario, id.ToString());
        Guid idSessao = IdentificadorV7.Gerar(operacao.InstanteOperacao);
        DateTimeOffset tetoAbsolutoEm = operacao.InstanteOperacao.AddMinutes(30);

        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(A.StringConexaoExecucao, operacao))
        {
            RepositorioSessoes repositorio = Repositorio(contexto, operacao);
            Guid epoca = await repositorio.ObterEpocaAcessoAsync(TestContext.Current.CancellationToken);
            Resultado<SessaoEmitida> emitida = await repositorio.EmitirAsync(
                new NovaSessao(idSessao, id, epoca, tetoAbsolutoEm, 30, [1, 2, 3]), null, TestContext.Current.CancellationToken);
            Assert.True(emitida.EhSucesso);
        }

        // Logout vence; Renew com a versão lida antes não reativa, não reinsere ticket nem estende prazo.
        await using (AcessoTenantDbContext logout = BancoTeste.CriarContexto(A.StringConexaoExecucao, operacao))
        {
            Assert.True((await Repositorio(logout, operacao).RevogarAsync(idSessao, 1, TestContext.Current.CancellationToken)).EhSucesso);
        }

        await using (AcessoTenantDbContext renew = BancoTeste.CriarContexto(A.StringConexaoExecucao, operacao))
        {
            Assert.False(await Repositorio(renew, operacao).RenovarAsync(idSessao, 1, [9, 9, 9], TestContext.Current.CancellationToken));
            Assert.False((await Repositorio(renew, operacao).RevogarAsync(idSessao, 1, TestContext.Current.CancellationToken)).EhSucesso);
        }

        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.NotNull(sessao.RevogadaEm);
        Assert.Null(sessao.TicketProtegido);
        Assert.Equal(tetoAbsolutoEm, sessao.TetoAbsolutoEm);
        Assert.Equal(2, sessao.Versao);
    }

    [Fact]
    public async Task QuintaFalha_Bloqueia15Min_RevogaTodasAsSessoesETrocaStamp_SemAfetarB()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("lockout");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        Guid idB = await ContasSinteticas.CriarContaAsync(B, Relogio, email, ContasSinteticas.SenhaPadraoB);
        ClienteAcessoHttp sessaoUm = CriarCliente(api);
        ClienteAcessoHttp sessaoDois = CriarCliente(api);
        await sessaoUm.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        await sessaoDois.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        string stampAntes = (await ContasSinteticas.ContaAsync(A, id)).SecurityStamp!;

        // Instante com fração abaixo de microssegundo: LockoutEnd deve ser normalizado.
        Relogio.DefinirAgora(TesteHttpBase.InstanteInicial.AddMinutes(1).AddTicks(1_234_567));
        DateTimeOffset instanteBloqueio = InstanteUtc.Normalizar(Relogio.GetUtcNow());
        ClienteAcessoHttp atacante = CriarCliente(api);
        for (int tentativa = 1; tentativa <= 4; tentativa++)
        {
            using HttpResponseMessage falha = await atacante.LoginAsync(email, SenhaErrada);
            await AssegurarProblemaAsync(falha, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        Assert.Equal(4, (await ContasSinteticas.ContaAsync(A, id)).AccessFailedCount);
        using (HttpResponseMessage quinta = await atacante.LoginAsync(email, SenhaErrada))
        {
            await AssegurarProblemaAsync(quinta, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        UsuarioAplicacao bloqueada = await ContasSinteticas.ContaAsync(A, id);
        Assert.Equal(instanteBloqueio.AddMinutes(15), bloqueada.LockoutEnd);
        Assert.Equal(TimeSpan.Zero, bloqueada.LockoutEnd!.Value.Offset);
        Assert.Equal(0, bloqueada.LockoutEnd.Value.Ticks % 10);
        Assert.Equal(0, bloqueada.AccessFailedCount);
        Assert.NotEqual(stampAntes, bloqueada.SecurityStamp);
        Assert.All(await ContasSinteticas.SessoesAsync(A, id), s =>
        {
            Assert.Equal(instanteBloqueio, s.RevogadaEm);
            Assert.Null(s.TicketProtegido);
        });

        foreach (ClienteAcessoHttp sessao in new[] { sessaoUm, sessaoDois })
        {
            using HttpResponseMessage me = await sessao.MeAsync();
            await AssegurarProblemaAsync(me, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        // +14:59 ainda bloqueado (senha correta recusada); +15:00 libera novo login, cookie antigo segue inválido.
        Relogio.DefinirAgora(instanteBloqueio.AddMinutes(15).AddSeconds(-1));
        using (HttpResponseMessage aindaBloqueado = await atacante.LoginAsync(email, ContasSinteticas.SenhaPadraoA))
        {
            await AssegurarProblemaAsync(aindaBloqueado, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        Relogio.DefinirAgora(instanteBloqueio.AddMinutes(15));
        using (HttpResponseMessage antigo = await sessaoUm.MeAsync())
        {
            await AssegurarProblemaAsync(antigo, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        using (HttpResponseMessage liberado = await atacante.LoginAsync(email, ContasSinteticas.SenhaPadraoA))
        {
            Assert.Equal(HttpStatusCode.Created, liberado.StatusCode);
        }

        UsuarioAplicacao contaB = await ContasSinteticas.ContaAsync(B, idB);
        Assert.Equal(0, contaB.AccessFailedCount);
        Assert.Null(contaB.LockoutEnd);
    }

    [Fact]
    public async Task FalhasConcorrentes_NaoPerdemContagem()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("concorrente");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp sequencial = CriarCliente(api);
        for (int tentativa = 1; tentativa <= 2; tentativa++)
        {
            using HttpResponseMessage falha = await sequencial.LoginAsync(email, SenhaErrada);
            Assert.Equal(HttpStatusCode.Unauthorized, falha.StatusCode);
        }

        ClienteAcessoHttp[] clientes = [CriarCliente(api), CriarCliente(api), CriarCliente(api)];
        foreach (ClienteAcessoHttp cliente in clientes)
        {
            await cliente.ObterTokenAsync();
        }

        HttpResponseMessage[] respostas = await Task.WhenAll(clientes.Select(c => c.LoginAsync(email, SenhaErrada, novoToken: false)));
        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }

        UsuarioAplicacao conta = await ContasSinteticas.ContaAsync(A, id);
        Assert.NotNull(conta.LockoutEnd);
        Assert.True(conta.LockoutEnd > Relogio.GetUtcNow());
    }

    [Fact]
    public async Task TresConflitosDeConcurrencyStampSeguidos_Retornam503()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("conflitos");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.ObterTokenAsync();

        api.Falhas.ConflitarProximasGravacoesUsuario(ServicoCredenciaisIdentity.MaximoTentativas);
        using (HttpResponseMessage resposta = await cliente.LoginAsync(email, SenhaErrada, novoToken: false))
        {
            await AssegurarProblemaAsync(resposta, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        Assert.Equal(0, (await ContasSinteticas.ContaAsync(A, id)).AccessFailedCount);

        // Dois conflitos seguidos ainda são absorvidos pela reavaliação.
        api.Falhas.ConflitarProximasGravacoesUsuario(ServicoCredenciaisIdentity.MaximoTentativas - 1);
        using (HttpResponseMessage absorvido = await cliente.LoginAsync(email, SenhaErrada))
        {
            await AssegurarProblemaAsync(absorvido, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        Assert.Equal(1, (await ContasSinteticas.ContaAsync(A, id)).AccessFailedCount);
    }

    [Fact]
    public async Task DuasRequisicoesParalelasApos60s_UmaGrava_OutraDescartaConflitoE200()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp um, Guid id) = await EntrarAsync(api);
        ClienteAcessoHttp dois = CriarCliente(api);
        CopiarCookies(um, dois);

        Relogio.DefinirAgora(InstanteInicial.AddSeconds(61));
        Encontro encontro = new(2);
        api.Falhas.AntesDeGravarSessao = encontro.ChegarAsync;
        HttpResponseMessage[] respostas = await Task.WhenAll(um.MeAsync(), dois.ContextoAsync());
        api.Falhas.AntesDeGravarSessao = null;

        try
        {
            Assert.All(respostas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        }
        finally
        {
            foreach (HttpResponseMessage resposta in respostas)
            {
                resposta.Dispose();
            }
        }

        SessaoAutenticacao sessao = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial.AddSeconds(61), sessao.UltimoUsoEm);
        Assert.Equal(2, sessao.Versao);
    }

    [Fact]
    public async Task LogoutConcorrenteComRegistroDeUso_ConcluiRevogada_SemReativar()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp sessao, Guid id) = await EntrarAsync(api);
        ClienteAcessoHttp uso = CriarCliente(api);
        CopiarCookies(sessao, uso);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(2));
        Encontro encontro = new(2);
        api.Falhas.AntesDeGravarSessao = encontro.ChegarAsync;
        Task<HttpResponseMessage> me = uso.MeAsync();
        Task<HttpResponseMessage> logout = sessao.LogoutAsync(novoToken: false);
        HttpResponseMessage[] respostas = await Task.WhenAll(me, logout);
        api.Falhas.AntesDeGravarSessao = null;

        try
        {
            Assert.Equal(HttpStatusCode.OK, respostas[0].StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, respostas[1].StatusCode);
        }
        finally
        {
            foreach (HttpResponseMessage resposta in respostas)
            {
                resposta.Dispose();
            }
        }

        SessaoAutenticacao revogada = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(InstanteInicial.AddMinutes(2), revogada.RevogadaEm);
        Assert.Null(revogada.TicketProtegido);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(4));
        using HttpResponseMessage depois = await uso.MeAsync();
        await AssegurarProblemaAsync(depois, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        Assert.Equal(revogada.Versao, Assert.Single(await ContasSinteticas.SessoesAsync(A, id)).Versao);
    }

    [Fact]
    public async Task LogoutComDoisConflitosDeRegistroDeUso_Repete_E204()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        api.Falhas.ConflitarProximasRevogacoesSessao(2);
        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        Assert.NotNull(Assert.Single(await ContasSinteticas.SessoesAsync(A, id)).RevogadaEm);
    }

    [Fact]
    public async Task LogoutComTresConflitosSeguidosDeRegistroDeUso_Retorna503SemConfirmarRevogacao()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        api.Falhas.ConflitarProximasRevogacoesSessao(3);
        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            await AssegurarProblemaAsync(logout, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
            Assert.DoesNotContain(cliente.UltimoSetCookie, c => c.StartsWith("bps-session=", StringComparison.Ordinal));
        }

        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Null(sessao.RevogadaEm);
        Assert.NotNull(sessao.TicketProtegido);
        using HttpResponseMessage me = await cliente.MeAsync();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task TrocaNoLoginComConflitoDeRegistroDeUso_Conclui201()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("trocauso");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(2));
        api.Falhas.ConflitarProximasRevogacoesSessao(1);
        using (HttpResponseMessage login = await cliente.LoginAsync(email, ContasSinteticas.SenhaPadraoA))
        {
            Assert.Equal(HttpStatusCode.Created, login.StatusCode);
        }

        List<SessaoAutenticacao> sessoes = await ContasSinteticas.SessoesAsync(A, id);
        Assert.Equal(2, sessoes.Count);
        Assert.Equal(InstanteInicial.AddMinutes(2), sessoes[0].RevogadaEm);
        Assert.Null(sessoes[1].RevogadaEm);
    }

    [Fact]
    public async Task LockoutComConflitosDeRegistroDeUso_ConcluiRevogandoTodas()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp sessao, Guid id) = await EntrarAsync(api, "lockoutuso");
        string email = (await ContasSinteticas.ContaAsync(A, id)).Email!;
        ClienteAcessoHttp atacante = CriarCliente(api);
        for (int tentativa = 1; tentativa <= 4; tentativa++)
        {
            using HttpResponseMessage falha = await atacante.LoginAsync(email, SenhaErrada);
            Assert.Equal(HttpStatusCode.Unauthorized, falha.StatusCode);
        }

        api.Falhas.ConflitarProximasRevogacoesSessao(2);
        using (HttpResponseMessage quinta = await atacante.LoginAsync(email, SenhaErrada))
        {
            await AssegurarProblemaAsync(quinta, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        Assert.All(await ContasSinteticas.SessoesAsync(A, id), s => Assert.NotNull(s.RevogadaEm));
        using HttpResponseMessage me = await sessao.MeAsync();
        await AssegurarProblemaAsync(me, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Theory]
    [InlineData("revogar-sessoes")]
    [InlineData("redefinir-senha")]
    [InlineData("desativar")]
    public async Task OperacaoDoOperadorComConflitosDeRegistroDeUso_ConcluiRevogandoTodas(string comando)
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp sessao, Guid id) = await EntrarAsync(api, comando);

        FalhasInjetadas falhas = new();
        falhas.ConflitarProximasRevogacoesSessao(2);
        await using (EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            A, A.StringConexaoOperadorUm, A.LoginOperadorUm, Relogio,
            substituirServicos: s =>
            {
                s.AddSingleton(falhas);
                s.AddSingleton(new ConcurrentQueue<string>());
                s.AddScoped<IInterceptor, InterceptadorTeste>();
            }))
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            ResultadoOperacao resultado = comando switch
            {
                "revogar-sessoes" => await escopo.Servicos.GetRequiredService<CasoUsoRevogarSessoes>().ExecutarAsync(A.IdTenant, A.IdAmbiente, id, ct),
                "redefinir-senha" => await escopo.Servicos.GetRequiredService<CasoUsoRedefinirSenha>().ExecutarAsync(A.IdTenant, A.IdAmbiente, id, ct),
                _ => await escopo.Servicos.GetRequiredService<CasoUsoDesativarConta>().ExecutarAsync(A.IdTenant, A.IdAmbiente, id, ct),
            };
            Assert.True(resultado.Sucesso, resultado.ToString());
        }

        Assert.All(await ContasSinteticas.SessoesAsync(A, id), s => Assert.NotNull(s.RevogadaEm));
        using HttpResponseMessage me = await sessao.MeAsync();
        await AssegurarProblemaAsync(me, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    private static void CopiarCookies(ClienteAcessoHttp origem, ClienteAcessoHttp destino)
    {
        foreach ((string nome, string valor) in origem.Cookies)
        {
            destino.Cookies[nome] = valor;
        }

        destino.TokenCsrf = origem.TokenCsrf;
    }

    private async Task<(ClienteAcessoHttp Cliente, Guid IdUsuario)> EntrarAsync(FabricaApiInstalacao api, string prefixo = "concorrencia")
    {
        string email = ContasSinteticas.NovoEmail(prefixo.Replace("-", string.Empty, StringComparison.Ordinal));
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        return (cliente, id);
    }

    private static RepositorioSessoes Repositorio(AcessoTenantDbContext contexto, ContextoOperacaoTeste operacao) =>
        new(contexto, operacao, Options.Create(new OpcoesInstalacaoTenant { IdTenant = Guid.NewGuid() }));

    /// <summary>
    /// Ponto de encontro de N participantes com limite de espera (evita bloqueio eterno se um participante
    /// ficar preso em lock de linha).
    /// </summary>
    private sealed class Encontro(int participantes)
    {
        private readonly TaskCompletionSource _todos = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _chegadas;

        public async Task ChegarAsync()
        {
            if (Interlocked.Increment(ref _chegadas) >= participantes)
            {
                _todos.TrySetResult();
            }

            await _todos.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
