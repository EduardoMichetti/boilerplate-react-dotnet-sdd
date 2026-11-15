using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.Application.Access.Operacoes;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Platform.Infrastructure;
using Platform.Operations;

// 1. Interpretação dos comandos e argumentos
RequisicaoComando comando;
try
{
    comando = InterpretadorComandos.Interpretar(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[ARGUMENTO_INVALIDO] {ex.Message}");
    return 1;
}

// 2. Construção da configuração e serviços DI (sem servidor HTTP)
Dictionary<string, string?> variaveisAmbiente = new(StringComparer.OrdinalIgnoreCase);
foreach (System.Collections.DictionaryEntry entrada in Environment.GetEnvironmentVariables())
{
    if (entrada.Key is string chave && entrada.Value is string valor)
    {
        variaveisAmbiente[chave] = valor;
        if (chave.Contains("__", StringComparison.Ordinal))
        {
            variaveisAmbiente[chave.Replace("__", ":", StringComparison.Ordinal)] = valor;
        }
    }
}

IConfiguration configuracao = new ConfigurationBuilder()
    .AddInMemoryCollection(variaveisAmbiente)
    .Build();

ServiceCollection servicosColecao = new();
servicosColecao.AddSingleton(configuracao);

servicosColecao.AddOptions<OpcoesInstalacaoTenant>()
    .Configure<IConfiguration>((opcoes, config) =>
    {
        opcoes.CodigoAplicacao = config["CodigoAplicacao"];
        _ = Guid.TryParse(config["IdAmbiente"], out Guid idAmbiente);
        _ = Guid.TryParse(config["IdTenant"], out Guid idTenant);
        opcoes.IdAmbiente = idAmbiente;
        opcoes.IdTenant = idTenant;
        opcoes.NomeExibicaoTenant = config["NomeExibicaoTenant"];
        opcoes.EstadoTenant = Enum.TryParse<EstadoTenant>(config["EstadoTenant"], true, out EstadoTenant est) ? est : null;
        opcoes.StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = config["StringsConexao:BancoApp"] ?? config["StringsConexao__BancoApp"]
        };
    })
    .PostConfigure(opcoes => opcoes.Validar());

servicosColecao.AddOptions<OpcoesOperacaoAcesso>()
    .Configure<IConfiguration>((opcoes, config) =>
    {
        opcoes.LoginAutorizado = config[OpcoesOperacaoAcesso.ChaveLoginAutorizado] ?? config["OperacaoAcesso__LoginAutorizado"];
        opcoes.RetencaoSessoesDiasBruto = config[OpcoesOperacaoAcesso.ChaveRetencaoSessoesDias] ?? config["OperacaoAcesso__RetencaoSessoesDias"];
    });
servicosColecao.AddSingleton<IValidateOptions<OpcoesOperacaoAcesso>, ValidadorOpcoesOperacaoAcesso>();

servicosColecao.AddSingleton(TimeProvider.System);
servicosColecao.AddSingleton<IProvedorSenha, LeitorSenhaConsole>();

servicosColecao.AddMemoryCache();
servicosColecao.AdicionarServicosHostOperacoes();

ServiceProvider provedor = servicosColecao.BuildServiceProvider();

// Validação antecipada das opções antes de conectar
try
{
    _ = provedor.GetRequiredService<IOptions<OpcoesInstalacaoTenant>>().Value;
    _ = provedor.GetRequiredService<IOptions<OpcoesOperacaoAcesso>>().Value;
}
catch (OptionsValidationException ex)
{
    foreach (string falha in ex.Failures)
    {
        Console.Error.WriteLine($"[CONFIGURACAO_INVALIDA] {falha}");
    }

    return 1;
}

// 3. Execução do comando via DI scope
using IServiceScope escopo = provedor.CreateScope();
IServiceProvider servicos = escopo.ServiceProvider;

ResultadoOperacao resultado;
try
{
    resultado = comando.Tipo switch
    {
        TipoComandoOperacao.ContaCriar => await servicos.GetRequiredService<CasoUsoCriarConta>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.Email!, comando.NomeExibicao!),
        TipoComandoOperacao.ContaDesativar => await servicos.GetRequiredService<CasoUsoDesativarConta>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.IdUsuario!.Value),
        TipoComandoOperacao.ContaReativar => await servicos.GetRequiredService<CasoUsoReativarConta>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.IdUsuario!.Value),
        TipoComandoOperacao.ContaRedefinirSenha => await servicos.GetRequiredService<CasoUsoRedefinirSenha>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.IdUsuario!.Value),
        TipoComandoOperacao.ContaRevogarSessoes => await servicos.GetRequiredService<CasoUsoRevogarSessoes>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.IdUsuario!.Value),
        TipoComandoOperacao.PermissaoConceder => await servicos.GetRequiredService<CasoUsoConcederPermissao>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.IdUsuario!.Value, comando.CodigoPermissao!),
        TipoComandoOperacao.PermissaoRevogar => await servicos.GetRequiredService<CasoUsoRevogarPermissao>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente, comando.IdUsuario!.Value, comando.CodigoPermissao!),
        TipoComandoOperacao.SessaoLimpar => await servicos.GetRequiredService<CasoUsoLimparSessoes>()
            .ExecutarAsync(comando.IdTenant, comando.IdAmbiente),
        _ => throw new InvalidOperationException($"Tipo de comando '{comando.Tipo}' não mapeado.")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FALHA_OPERACAO] {ex.Message}");
    return 1;
}

SaidaResultadoJson.Escrever(resultado);
return resultado.Sucesso ? 0 : 1;

