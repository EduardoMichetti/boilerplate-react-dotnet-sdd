namespace Platform.IntegrationTests.Http;

using System.Net;
using Microsoft.Extensions.Options;
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
        Assert.Equal(anterior.ExpiraEm, depois.ExpiraEm);
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
        DateTimeOffset expiraEm = operacao.InstanteOperacao.AddMinutes(30);

        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(A.StringConexaoExecucao, operacao))
        {
            RepositorioSessoes repositorio = Repositorio(contexto, operacao);
            Guid epoca = await repositorio.ObterEpocaAcessoAsync(TestContext.Current.CancellationToken);
            Resultado<SessaoEmitida> emitida = await repositorio.EmitirAsync(
                new NovaSessao(idSessao, id, epoca, expiraEm, [1, 2, 3]), null, TestContext.Current.CancellationToken);
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
        Assert.Equal(expiraEm, sessao.ExpiraEm);
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
