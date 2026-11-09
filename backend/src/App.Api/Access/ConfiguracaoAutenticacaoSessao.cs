namespace App.Api.Access;

using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;
using Platform.Application.Access;
using Platform.Application.Configuration;

/// <summary>
/// Opções de Data Protection da instalação (data-model §6).
/// </summary>
public sealed class OpcoesProtecaoDados
{
    /// <summary>Chave do diretório do key ring.</summary>
    public const string ChaveDiretorioChaves = "ProtecaoDados:DiretorioChaves";

    /// <summary>Chave do arquivo PKCS#12 do certificado.</summary>
    public const string ChaveArquivoCertificado = "ProtecaoDados:ArquivoCertificado";

    /// <summary>Chave do arquivo com a senha do certificado.</summary>
    public const string ChaveArquivoSenhaCertificado = "ProtecaoDados:ArquivoSenhaCertificado";

    /// <summary>Volume privado persistente do key ring, gravável pela API.</summary>
    public string? DiretorioChaves { get; set; }

    /// <summary>Arquivo privado PKCS#12 que protege o key ring (obrigatório fora de Development).</summary>
    public string? ArquivoCertificado { get; set; }

    /// <summary>Arquivo privado com a senha do certificado.</summary>
    public string? ArquivoSenhaCertificado { get; set; }
}

/// <summary>
/// Leitura do certificado do key ring a partir dos arquivos privados configurados.
/// </summary>
public static class CertificadoProtecaoDados
{
    /// <summary>
    /// Carrega o certificado na composição para <c>ProtectKeysWithCertificate</c>. Falha de leitura não
    /// é lançada aqui: a validação na inicialização (<see cref="ValidadorOpcoesProtecaoDados"/>) a reporta.
    /// </summary>
    /// <param name="configuracao">Configuração.</param>
    /// <returns>Certificado ou nulo.</returns>
    public static X509Certificate2? TentarCarregar(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        try
        {
            return Carregar(new OpcoesProtecaoDados
            {
                ArquivoCertificado = configuracao[OpcoesProtecaoDados.ChaveArquivoCertificado],
                ArquivoSenhaCertificado = configuracao[OpcoesProtecaoDados.ChaveArquivoSenhaCertificado],
            });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Carrega o certificado com chave privada.
    /// </summary>
    /// <param name="opcoes">Opções com os caminhos dos arquivos.</param>
    /// <returns>Certificado ou nulo quando não configurado.</returns>
    /// <exception cref="InvalidOperationException">Arquivo ilegível, senha incorreta ou sem chave privada.</exception>
    public static X509Certificate2? Carregar(OpcoesProtecaoDados opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.ArquivoCertificado))
        {
            return null;
        }

        try
        {
            string? senha = string.IsNullOrWhiteSpace(opcoes.ArquivoSenhaCertificado)
                ? null
                : File.ReadAllText(opcoes.ArquivoSenhaCertificado).TrimEnd('\r', '\n');
            X509Certificate2 certificado = X509CertificateLoader.LoadPkcs12FromFile(opcoes.ArquivoCertificado, senha);
            if (!certificado.HasPrivateKey)
            {
                certificado.Dispose();
                throw new InvalidOperationException($"{OpcoesProtecaoDados.ChaveArquivoCertificado} não contém chave privada.");
            }

            return certificado;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new InvalidOperationException(
                $"{OpcoesProtecaoDados.ChaveArquivoCertificado}/{OpcoesProtecaoDados.ChaveArquivoSenhaCertificado} ilegíveis ou inválidos.");
        }
    }
}

/// <summary>
/// Valida as opções de Data Protection na inicialização, com diagnóstico que cita só as chaves.
/// </summary>
/// <param name="ambiente">Ambiente do host.</param>
public sealed class ValidadorOpcoesProtecaoDados(IHostEnvironment ambiente)
    : IValidateOptions<OpcoesProtecaoDados>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpcoesProtecaoDados options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> erros = [];

        if (string.IsNullOrWhiteSpace(options.DiretorioChaves))
        {
            erros.Add($"{OpcoesProtecaoDados.ChaveDiretorioChaves} é obrigatório (volume privado do key ring da instalação).");
        }

        if (!ambiente.IsDevelopment() && string.IsNullOrWhiteSpace(options.ArquivoCertificado))
        {
            erros.Add($"{OpcoesProtecaoDados.ChaveArquivoCertificado} é obrigatório fora de Development.");
        }
        else
        {
            try
            {
                using X509Certificate2? certificado = CertificadoProtecaoDados.Carregar(options);
            }
            catch (InvalidOperationException ex)
            {
                erros.Add(ex.Message);
            }
        }

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }
}

/// <summary>
/// Requisito de autorização de recurso próprio: a sessão autenticada nesta requisição pertence
/// à conta do principal, no tenant da instalação. Não é uma política “qualquer autenticado”.
/// </summary>
public sealed class RequisitoRecursoProprio : IAuthorizationRequirement;

/// <summary>
/// Avalia <see cref="RequisitoRecursoProprio"/> contra o estado da requisição e a configuração.
/// </summary>
/// <param name="estado">Estado da requisição.</param>
/// <param name="instalacao">Configuração da instalação.</param>
public sealed class ManipuladorRecursoProprio(EstadoRequisicaoAcesso estado, IOptions<OpcoesInstalacaoTenant> instalacao)
    : AuthorizationHandler<RequisitoRecursoProprio>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequisitoRecursoProprio requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (estado.Sessao is { } sessao
            && string.Equals(context.User.FindFirstValue(ClaimsSessao.Sessao), sessao.IdSessao.ToString(), StringComparison.Ordinal)
            && string.Equals(context.User.FindFirstValue(ClaimsSessao.Usuario), sessao.IdUsuario.ToString(), StringComparison.Ordinal)
            && string.Equals(context.User.FindFirstValue(ClaimsSessao.Tenant), instalacao.Value.IdTenant.ToString(), StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Escreve e remove o cookie de sessão com o <c>TicketDataFormat</c> e o <c>CookieManager</c> do
/// esquema (G21): o cookie contém só a referência <c>IdTenant/EpocaAcesso/IdSessao</c> na claim de
/// sessão do handler nativo e expira em <c>ExpiraEm</c>. Chamado somente após o commit.
/// </summary>
/// <param name="monitor">Opções do esquema de cookie.</param>
public sealed class EmissorCookieSessao(IOptionsMonitor<CookieAuthenticationOptions> monitor)
{
    /// <summary>
    /// Claim de sessão lida pelo <see cref="CookieAuthenticationHandler"/> quando há <c>SessionStore</c>.
    /// Conferida na inicialização por <see cref="ConfiguracaoAutenticacaoSessao.VerificarClaimSessaoHandler"/>.
    /// </summary>
    public const string ClaimSessaoHandler = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    /// <summary>
    /// Emite o cookie da sessão confirmada.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="referencia">Referência da nova sessão.</param>
    /// <param name="expiraEm">Prazo absoluto gravado.</param>
    public void Emitir(HttpContext contexto, ReferenciaSessao referencia, DateTimeOffset expiraEm)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(referencia);
        CookieAuthenticationOptions opcoes = monitor.Get(ConfiguracaoAutenticacaoSessao.Esquema);

        ClaimsIdentity identidade = new(
            [new Claim(ClaimSessaoHandler, referencia.Formatar(), ClaimValueTypes.String, opcoes.ClaimsIssuer)],
            opcoes.ClaimsIssuer);
        AuthenticationTicket ticket = new(new ClaimsPrincipal(identidade), null, ConfiguracaoAutenticacaoSessao.Esquema);
        string valor = opcoes.TicketDataFormat.Protect(ticket);

        CookieOptions cookie = opcoes.Cookie.Build(contexto);
        cookie.Expires = expiraEm;
        opcoes.CookieManager.AppendResponseCookie(contexto, opcoes.Cookie.Name!, valor, cookie);
    }

    /// <summary>
    /// Remove o cookie de sessão (somente após a revogação confirmada).
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    public void Remover(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        CookieAuthenticationOptions opcoes = monitor.Get(ConfiguracaoAutenticacaoSessao.Esquema);
        opcoes.CookieManager.DeleteCookie(contexto, opcoes.Cookie.Name!, opcoes.Cookie.Build(contexto));
    }
}

/// <summary>
/// Autentica o cookie de sessão somente nas rotas L2, depois de tenant e marcador (G2). Health,
/// OpenAPI e rotas não mapeadas nunca consultam sessão, mesmo com cookies enviados.
/// </summary>
/// <param name="proximo">Próximo middleware.</param>
public sealed class MiddlewareAutenticacaoSessao(RequestDelegate proximo)
{
    /// <summary>
    /// Executa a autenticação.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <returns>Tarefa do pipeline.</returns>
    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (contexto.ObterRotaAcessoL2() is not null)
        {
            AuthenticateResult resultado = await contexto.AuthenticateAsync(ConfiguracaoAutenticacaoSessao.Esquema);
            if (resultado.Succeeded)
            {
                contexto.User = resultado.Principal;
            }
        }

        await proximo(contexto);
    }
}

/// <summary>
/// Sem-autenticação padrão: impede que o <c>UseAuthentication</c> adicionado automaticamente pelo
/// host autentique o cookie fora das rotas L2.
/// </summary>
/// <param name="opcoes">Opções.</param>
/// <param name="registrador">Fábrica de registradores.</param>
/// <param name="codificador">Codificador de URL.</param>
public sealed class ManipuladorSemAutenticacao(
    IOptionsMonitor<AuthenticationSchemeOptions> opcoes,
    ILoggerFactory registrador,
    System.Text.Encodings.Web.UrlEncoder codificador)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opcoes, registrador, codificador)
{
    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}

/// <summary>
/// Composição de sessão, cookies, antiforgery, Data Protection e autorização de recurso próprio.
/// </summary>
public static class ConfiguracaoAutenticacaoSessao
{
    /// <summary>Esquema de cookie da sessão.</summary>
    public const string Esquema = "bps-sessao";

    /// <summary>Esquema padrão de autenticação que não autentica nada.</summary>
    public const string EsquemaNenhum = "bps-nenhum";

    /// <summary>Política de recurso próprio (<c>/me</c>, <c>/context</c>, logout).</summary>
    public const string PoliticaRecursoProprio = "RecursoProprio";

    /// <summary>Cookie de sessão em produção.</summary>
    public const string CookieSessaoProducao = "__Host-bps-session";

    /// <summary>Cookie de sessão em Development HTTP localhost.</summary>
    public const string CookieSessaoDesenvolvimento = "bps-session";

    /// <summary>Cookie antiforgery em produção.</summary>
    public const string CookieCsrfProducao = "__Host-bps-csrf";

    /// <summary>Cookie antiforgery em Development HTTP localhost.</summary>
    public const string CookieCsrfDesenvolvimento = "bps-csrf";

    /// <summary>Header do token antiforgery.</summary>
    public const string CabecalhoCsrf = "X-CSRF-TOKEN";

    /// <summary>
    /// Registra sessão, cookies, antiforgery, Data Protection e autorização.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <param name="configuracao">Configuração lida na composição (certificado do key ring).</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarAutenticacaoSessao(this IServiceCollection servicos, IConfiguration configuracao)
    {
        VerificarClaimSessaoHandler();

        servicos.AddHttpContextAccessor();
        servicos.AddScoped<EstadoRequisicaoAcesso>();
        servicos.AddSingleton<ProtetorTicketSessao>();
        servicos.AddSingleton<Platform.Application.Access.Portas.IProtetorTicketSessao>(p => p.GetRequiredService<ProtetorTicketSessao>());
        servicos.AddSingleton<ArmazenamentoTicketsSessao>();
        servicos.AddSingleton<EmissorCookieSessao>();

        AdicionarOpcoesSessao(servicos);
        AdicionarProtecaoDados(servicos, configuracao);

        servicos.AddAuthentication(opcoes =>
            {
                opcoes.DefaultAuthenticateScheme = EsquemaNenhum;
                opcoes.DefaultChallengeScheme = Esquema;
                opcoes.DefaultForbidScheme = Esquema;
            })
            .AddScheme<AuthenticationSchemeOptions, ManipuladorSemAutenticacao>(EsquemaNenhum, null)
            .AddCookie(Esquema, opcoes =>
            {
                opcoes.Cookie.HttpOnly = true;
                opcoes.Cookie.SameSite = SameSiteMode.Lax;
                opcoes.Cookie.Path = "/";
                opcoes.Cookie.Domain = null;
                opcoes.SlidingExpiration = false;
                opcoes.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = contexto => ConfiguracaoProblemDetails.EscreverAsync(
                        contexto.HttpContext, ConfiguracaoProblemDetails.DeErro(ErrosAcesso.AutenticacaoNecessaria)),
                    OnRedirectToAccessDenied = contexto => ConfiguracaoProblemDetails.EscreverAsync(
                        contexto.HttpContext, ConfiguracaoProblemDetails.DeErro(ErrosAcesso.AcessoTenantNegado)),
                    OnRedirectToLogout = _ => Task.CompletedTask,
                    OnRedirectToReturnUrl = _ => Task.CompletedTask,
                    OnValidatePrincipal = contexto =>
                    {
                        // Sem refresh: a validação nunca reemite cookie.
                        contexto.ShouldRenew = false;
                        return Task.CompletedTask;
                    },
                };
            });

        servicos.AddOptions<CookieAuthenticationOptions>(Esquema)
            .Configure<IHostEnvironment, TimeProvider, ArmazenamentoTicketsSessao>((opcoes, ambiente, relogio, armazenamento) =>
            {
                bool desenvolvimento = ambiente.IsDevelopment();
                opcoes.Cookie.Name = desenvolvimento ? CookieSessaoDesenvolvimento : CookieSessaoProducao;
                opcoes.Cookie.SecurePolicy = desenvolvimento ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                opcoes.TimeProvider = relogio;
                opcoes.SessionStore = armazenamento;
            });

        servicos.AddAntiforgery();
        servicos.AddOptions<AntiforgeryOptions>()
            .Configure<IHostEnvironment>((opcoes, ambiente) =>
            {
                bool desenvolvimento = ambiente.IsDevelopment();
                opcoes.Cookie.Name = desenvolvimento ? CookieCsrfDesenvolvimento : CookieCsrfProducao;
                opcoes.Cookie.SecurePolicy = desenvolvimento ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                opcoes.Cookie.HttpOnly = true;
                opcoes.Cookie.SameSite = SameSiteMode.Strict;
                opcoes.Cookie.Path = "/";
                opcoes.HeaderName = CabecalhoCsrf;
            });

        servicos.AddScoped<IAuthorizationHandler, ManipuladorRecursoProprio>();
        servicos.AddAuthorizationBuilder()
            .AddPolicy(PoliticaRecursoProprio, politica => politica
                .RequireAuthenticatedUser()
                .AddRequirements(new RequisitoRecursoProprio()));

        return servicos;
    }

    /// <summary>
    /// Falha na inicialização se a claim de sessão do handler nativo mudar de nome (G21).
    /// </summary>
    /// <exception cref="InvalidOperationException">Claim divergente na versão do framework.</exception>
    public static void VerificarClaimSessaoHandler()
    {
        object? valor = typeof(CookieAuthenticationHandler)
            .GetField("SessionIdClaim", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetRawConstantValue();
        if (!string.Equals(valor as string, EmissorCookieSessao.ClaimSessaoHandler, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A claim de sessão do CookieAuthenticationHandler mudou; revise o EmissorCookieSessao (G21).");
        }
    }

    private static void AdicionarOpcoesSessao(IServiceCollection servicos)
    {
        servicos.AddSingleton<IValidateOptions<OpcoesSessao>, ValidadorOpcoesSessao>();
        servicos.AddOptions<OpcoesSessao>()
            .Configure<IConfiguration>((opcoes, config) => opcoes.PrazoAbsolutoMinutosBruto = config[OpcoesSessao.ChavePrazoAbsolutoMinutos])
            .ValidateOnStart();
    }

    private static void AdicionarProtecaoDados(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddSingleton<IValidateOptions<OpcoesProtecaoDados>, ValidadorOpcoesProtecaoDados>();
        servicos.AddOptions<OpcoesProtecaoDados>()
            .Configure<IConfiguration>((opcoes, config) =>
            {
                opcoes.DiretorioChaves = config[OpcoesProtecaoDados.ChaveDiretorioChaves];
                opcoes.ArquivoCertificado = config[OpcoesProtecaoDados.ChaveArquivoCertificado];
                opcoes.ArquivoSenhaCertificado = config[OpcoesProtecaoDados.ChaveArquivoSenhaCertificado];
            })
            .ValidateOnStart();

        // Nome lógico e key ring por instalação; certificado externo protege as chaves (obrigatório fora de Development).
        IDataProtectionBuilder protecao = servicos.AddDataProtection();
        if (CertificadoProtecaoDados.TentarCarregar(configuracao) is { } certificado)
        {
            protecao.ProtectKeysWithCertificate(certificado);
        }

        servicos.AddOptions<DataProtectionOptions>()
            .Configure<IOptions<OpcoesInstalacaoTenant>>((opcoes, instalacao) =>
                opcoes.ApplicationDiscriminator = $"{instalacao.Value.CodigoAplicacao}/{instalacao.Value.IdAmbiente:D}/{instalacao.Value.IdTenant:D}");
        servicos.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<OpcoesProtecaoDados>, ILoggerFactory>((opcoes, protecaoDados, registradores) =>
            {
                string diretorio = protecaoDados.Value.DiretorioChaves
                    ?? throw new InvalidOperationException($"{OpcoesProtecaoDados.ChaveDiretorioChaves} é obrigatório.");
                opcoes.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(diretorio), registradores);
            });
    }
}
