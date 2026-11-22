namespace Platform.IntegrationTests.Http.Suporte;

using System.Collections.Concurrent;
using System.Data.Common;
using App.Api.Access;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Access;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// Configuração de uma instância da API de teste ligada a uma instalação PostgreSQL real.
/// </summary>
public sealed record OpcoesFabricaApi
{
    /// <summary>Ambiente do host (Development por padrão).</summary>
    public string Ambiente { get; init; } = "Development";

    /// <summary>Estado configurado da instalação.</summary>
    public EstadoTenant Estado { get; init; } = EstadoTenant.Ativo;

    /// <summary>Valor bruto de <c>Sessao:PrazoAbsolutoMinutos</c> (nulo = ausente).</summary>
    public string? PrazoAbsolutoMinutos { get; init; }

    /// <summary>Diretório do key ring; nulo cria um temporário por instância.</summary>
    public string? DiretorioChaves { get; init; }

    /// <summary>Limite do limiter de login; nulo usa a política real (T093, G11).</summary>
    public int? LimiteLogin { get; init; } = 100_000;

    /// <summary>Tenant configurado; nulo usa o da instalação (outro valor simula marcador divergente).</summary>
    public Guid? IdTenantConfigurado { get; init; }

    /// <summary>Configurações adicionais (certificado etc.).</summary>
    public IReadOnlyDictionary<string, string?> Extras { get; init; } = new Dictionary<string, string?>();

    /// <summary>Configuração adicional de serviços exclusivos de teste.</summary>
    public Action<IServiceCollection>? ServicosTeste { get; init; }
}

/// <summary>
/// <c>WebApplicationFactory&lt;Program&gt;</c> por instalação: conexão runtime da fixture, relógio
/// controlável, key ring próprio, captura de logs e de comandos SQL e falhas injetáveis.
/// </summary>
public sealed class FabricaApiInstalacao : WebApplicationFactory<Program>
{
    private readonly InstalacaoTestePostgres _instalacao;
    private readonly OpcoesFabricaApi _opcoes;
    private readonly bool _diretorioProprio;

    /// <summary>
    /// Cria a fábrica.
    /// </summary>
    /// <param name="instalacao">Instalação alvo (conexão runtime).</param>
    /// <param name="relogio">Relógio controlável compartilhado com o teste.</param>
    /// <param name="opcoes">Opções; nulo usa o padrão.</param>
    public FabricaApiInstalacao(InstalacaoTestePostgres instalacao, RelogioControlavel relogio, OpcoesFabricaApi? opcoes = null)
    {
        _instalacao = instalacao;
        Relogio = relogio;
        _opcoes = opcoes ?? new OpcoesFabricaApi();
        _diretorioProprio = _opcoes.DiretorioChaves is null;
        DiretorioChaves = _opcoes.DiretorioChaves ?? Directory.CreateTempSubdirectory("bps-chaves-").FullName;
    }

    /// <summary>Relógio controlável usado pela API.</summary>
    public RelogioControlavel Relogio { get; }

    /// <summary>Diretório do key ring desta instância (reutilizável num "restart").</summary>
    public string DiretorioChaves { get; }

    /// <summary>Logs capturados (nível Trace, todas as categorias).</summary>
    public CapturaLogs Logs { get; } = new();

    /// <summary>Comandos SQL executados pela API.</summary>
    public ConcurrentQueue<string> ComandosSql { get; } = new();

    /// <summary>Falhas injetáveis no banco.</summary>
    public FalhasInjetadas Falhas { get; } = new();

    /// <summary>Tenant enviado no header pelos clientes desta fábrica.</summary>
    public Guid IdTenant => _opcoes.IdTenantConfigurado ?? _instalacao.IdTenant;

    /// <summary>
    /// Cria um cliente com cookie jar próprio.
    /// </summary>
    /// <param name="https">Usa base HTTPS (cookies Secure em Production).</param>
    /// <returns>Cliente de acesso.</returns>
    public ClienteAcessoHttp CriarClienteAcesso(bool https = false)
    {
        HttpClient http = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri(https ? "https://localhost" : "http://localhost"),
        });
        return new ClienteAcessoHttp(http, IdTenant);
    }

    /// <summary>
    /// Confere que cookie handler e <see cref="UserManager{TUser}"/> usam o relógio controlável (G11, G13).
    /// </summary>
    public void AssegurarRelogioControlavel()
    {
        CookieAuthenticationOptions cookie = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ConfiguracaoAutenticacaoSessao.Esquema);
        Assert.Same(Relogio, cookie.TimeProvider);
        Assert.Same(Relogio, Services.GetRequiredService<TimeProvider>());

        using IServiceScope escopo = Services.CreateScope();
        UserManager<UsuarioAplicacao> gerenciador = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioAplicacao>>();
        Assert.IsType<GerenciadorUsuariosAplicacao>(gerenciador);
    }

    /// <summary>
    /// Exige que a API não inicialize e devolve o diagnóstico da falha: a exceção recebida mais os logs capturados.
    /// </summary>
    /// <remarks>
    /// G30: quando a aplicação falha ao iniciar antes de o <c>DeferredHost</c> do <c>WebApplicationFactory</c>
    /// consultar o <c>IHostApplicationLifetime</c>, o host já foi descartado e o framework relança
    /// <see cref="ObjectDisposedException"/> no lugar da causa real (corrida que aparece sob carga). O host registra a
    /// falha de inicialização em log antes de ser descartado, então o diagnóstico considera exceção e logs.
    /// </remarks>
    /// <returns>Texto da exceção seguido do texto completo de todas as entradas de log.</returns>
    public string ExigirFalhaInicializacao()
    {
        Exception erro = Assert.ThrowsAny<Exception>(() => CreateClient());
        return erro + Environment.NewLine + string.Join(Environment.NewLine, Logs.Entradas.Select(e => e.TextoCompleto));
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment(_opcoes.Ambiente);
        builder.UseSetting("CodigoAplicacao", "app-teste");
        builder.UseSetting("IdAmbiente", _instalacao.IdAmbiente.ToString());
        builder.UseSetting("IdTenant", IdTenant.ToString());
        builder.UseSetting("NomeExibicaoTenant", $"Instalação {_instalacao.NomeBancoDados[..12]}");
        builder.UseSetting("EstadoTenant", _opcoes.Estado.ToString());
        builder.UseSetting("StringsConexao:BancoApp", _instalacao.StringConexaoExecucao);
        builder.UseSetting("ProtecaoDados:DiretorioChaves", DiretorioChaves);
        if (_opcoes.PrazoAbsolutoMinutos is not null)
        {
            builder.UseSetting("Sessao:PrazoAbsolutoMinutos", _opcoes.PrazoAbsolutoMinutos);
        }

        foreach ((string chave, string? valor) in _opcoes.Extras)
        {
            builder.UseSetting(chave, valor);
        }

        builder.ConfigureLogging(registro => registro
            .ClearProviders()
            .AddProvider(Logs)
            .SetMinimumLevel(LogLevel.Trace)
            .AddFilter(_ => true));

        builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(Relogio);
            if (_opcoes.LimiteLogin is int limite)
            {
                servicos.RemoveAll<OpcoesLimiteLogin>();
                servicos.AddSingleton(new OpcoesLimiteLogin { Limite = limite });
            }

            servicos.AddSingleton(Falhas);
            servicos.AddSingleton(ComandosSql);
            servicos.AddScoped<IInterceptor, InterceptadorTeste>();
            _opcoes.ServicosTeste?.Invoke(servicos);
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _diretorioProprio)
        {
            try
            {
                Directory.Delete(DiretorioChaves, recursive: true);
            }
            catch (IOException)
            {
                // Diretório temporário: limpeza best effort.
            }
        }
    }
}

/// <summary>
/// Falha de banco sintética (subclasse de <see cref="DbException"/>, tratada como dependência → 503).
/// </summary>
public sealed class ExcecaoBancoInjetada() : DbException("Falha de banco injetada pelo teste.");

/// <summary>
/// Falhas injetáveis por teste (contadores de disparo).
/// </summary>
public sealed class FalhasInjetadas
{
    private int _commits;
    private int _conflitosUsuario;
    private int _gravacoesSessao;

    /// <summary>Barreira opcional executada antes do commit (sincronização determinística).</summary>
    public Func<Task>? AntesDoCommit { get; set; }

    /// <summary>Barreira opcional executada logo após o início de cada transação explícita.</summary>
    public Func<Task>? AoIniciarTransacao { get; set; }

    /// <summary>Próximas gravações que alteram sessões falharão com <see cref="ExcecaoBancoInjetada"/>.</summary>
    public void FalharProximasGravacoesSessao(int quantidade) => Volatile.Write(ref _gravacoesSessao, quantidade);

    /// <summary>Próximos commits que falharão.</summary>
    public void FalharProximosCommits(int quantidade) => Volatile.Write(ref _commits, quantidade);

    /// <summary>Próximas gravações de <c>access.usuarios</c> que terão conflito de concorrência.</summary>
    public void ConflitarProximasGravacoesUsuario(int quantidade) => Volatile.Write(ref _conflitosUsuario, quantidade);

    internal bool ConsumirCommit() => Consumir(ref _commits);

    internal bool ConsumirConflitoUsuario() => Consumir(ref _conflitosUsuario);

    internal bool ConsumirGravacaoSessao() => Consumir(ref _gravacoesSessao);

    private static bool Consumir(ref int contador)
    {
        while (true)
        {
            int atual = Volatile.Read(ref contador);
            if (atual <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref contador, atual - 1, atual) == atual)
            {
                return true;
            }
        }
    }
}

/// <summary>
/// Interceptor de teste: registra o SQL executado e injeta falhas de commit e conflitos de concorrência.
/// </summary>
/// <param name="falhas">Falhas configuradas.</param>
/// <param name="comandos">Destino do SQL capturado.</param>
internal sealed class InterceptadorTeste(FalhasInjetadas falhas, ConcurrentQueue<string> comandos)
    : DbCommandInterceptor, IDbTransactionInterceptor, ISaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        comandos.Enqueue(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        comandos.Enqueue(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        comandos.Enqueue(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        if (falhas.AoIniciarTransacao is { } barreira)
        {
            await barreira();
        }

        return result;
    }

    public async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (falhas.AntesDoCommit is { } barreira)
        {
            await barreira();
        }

        return falhas.ConsumirCommit() ? throw new ExcecaoBancoInjetada() : result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        bool gravaUsuario = eventData.Context?.ChangeTracker.Entries<UsuarioAplicacao>()
            .Any(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Modified) ?? false;
        if (gravaUsuario && falhas.ConsumirConflitoUsuario())
        {
            throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Conflito injetado pelo teste.");
        }

        bool gravaSessao = eventData.Context?.ChangeTracker.Entries<SessaoAutenticacao>()
            .Any(e => e.State is Microsoft.EntityFrameworkCore.EntityState.Modified or Microsoft.EntityFrameworkCore.EntityState.Added) ?? false;
        if (gravaSessao && falhas.ConsumirGravacaoSessao())
        {
            throw new ExcecaoBancoInjetada();
        }

        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// Provedor de log que captura todas as entradas formatadas (com escopo e exceção).
/// </summary>
public sealed class CapturaLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<EntradaLog> _entradas = new();

    /// <summary>Entradas capturadas.</summary>
    public IReadOnlyCollection<EntradaLog> Entradas => _entradas;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Registrador(categoryName, _entradas);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class Registrador(string categoria, ConcurrentQueue<EntradaLog> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string estado = state is IEnumerable<KeyValuePair<string, object?>> pares
                ? string.Join(';', pares.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            destino.Enqueue(new EntradaLog(categoria, logLevel, eventId, formatter(state, exception), estado, exception?.ToString()));
        }
    }
}

/// <summary>
/// Entrada de log capturada.
/// </summary>
/// <param name="Categoria">Categoria.</param>
/// <param name="Nivel">Nível.</param>
/// <param name="Evento">Evento.</param>
/// <param name="Mensagem">Mensagem formatada.</param>
/// <param name="Estado">Pares do estado estruturado.</param>
/// <param name="Excecao">Exceção completa, quando houver.</param>
public sealed record EntradaLog(string Categoria, LogLevel Nivel, EventId Evento, string Mensagem, string Estado, string? Excecao)
{
    /// <summary>Todo o texto da entrada, para buscas de vazamento.</summary>
    public string TextoCompleto => $"{Categoria}|{Mensagem}|{Estado}|{Excecao}";
}
