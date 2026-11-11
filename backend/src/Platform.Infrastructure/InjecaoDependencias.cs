namespace Platform.Infrastructure;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.Application.Access.Operacoes;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Access.Portas;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Infrastructure.Access;
using Platform.Infrastructure.Access.Operacoes;
using Platform.Infrastructure.Persistence.Auditoria;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Composição da infraestrutura de acesso compartilhada pela API e pelo host operacional.
/// O host fornece <c>IContextoOperacao</c>, <see cref="TimeProvider"/> e <see cref="OpcoesInstalacaoTenant"/>.
/// </summary>
public static class InjecaoDependencias
{
    /// <summary>
    /// Registra o <see cref="AcessoTenantDbContext"/> (Npgsql, histórico em <c>foundation</c>, interceptors),
    /// o Identity com <see cref="GerenciadorUsuariosAplicacao"/> e opções explícitas, e os adaptadores de acesso.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarInfraestruturaAcesso(this IServiceCollection servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        // Interceptors registrados como IInterceptor entram no DbContext; o de auditoria é obrigatório.
        servicos.AddScoped<IInterceptor, InterceptadorAuditoria>();

        servicos.AddDbContext<AcessoTenantDbContext>((provedor, opcoes) =>
        {
            OpcoesInstalacaoTenant config = provedor.GetRequiredService<IOptions<OpcoesInstalacaoTenant>>().Value;
            if (!string.IsNullOrWhiteSpace(config.StringsConexao?.BancoApp))
            {
                opcoes.UseNpgsql(config.StringsConexao.BancoApp, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation"));
            }

            opcoes.AddInterceptors(provedor.GetServices<IInterceptor>());
        });

        servicos.AddIdentityCore<UsuarioAplicacao>(opcoes =>
            {
                opcoes.Lockout.AllowedForNewUsers = true;
                opcoes.Lockout.MaxFailedAccessAttempts = 5;
                opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                opcoes.Password.RequiredLength = 12;
                opcoes.Password.RequireUppercase = true;
                opcoes.Password.RequireLowercase = true;
                opcoes.Password.RequireDigit = true;
                opcoes.Password.RequireNonAlphanumeric = true;

                opcoes.User.RequireUniqueEmail = true;
                opcoes.SignIn.RequireConfirmedEmail = false;
                opcoes.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AcessoTenantDbContext>()
            .AddUserManager<GerenciadorUsuariosAplicacao>()
            .AddPasswordValidator<ValidadorTamanhoMaximoSenha>();

        servicos.AddScoped<IVerificadorIdentidadeBancoTenant, VerificadorIdentidadeBancoTenant>();
        servicos.AddScoped<IRepositorioSessoes, RepositorioSessoes>();
        servicos.AddScoped<IServicoCredenciais, ServicoCredenciaisIdentity>();
        servicos.AddScoped<IConsultaContextoAcesso, ConsultaContextoAcesso>();

        // Portas e adaptadores de operações restritas (FR-033)
        servicos.AddScoped<IManutencaoContas, ManutencaoContasIdentity>();
        servicos.AddScoped<IRegistroOperacoesAcesso, RegistroOperacoesAcesso>();
        servicos.AddScoped<IVerificadorAutorizacaoOperador, VerificadorAutorizacaoOperador>();
        servicos.AddScoped<IRepositorioLimpezaSessoes, RepositorioLimpezaSessoes>();

        return servicos;
    }

    /// <summary>
    /// Registra os casos de uso de operações restritas (FR-033) no host operacional e testes.
    /// Exige que <see cref="IProvedorSenha"/> e <see cref="IContextoOperacao"/> estejam registrados.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarCasosUsoOperacoes(this IServiceCollection servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        servicos.AddScoped<CasoUsoCriarConta>();
        servicos.AddScoped<CasoUsoDesativarConta>();
        servicos.AddScoped<CasoUsoReativarConta>();
        servicos.AddScoped<CasoUsoRedefinirSenha>();
        servicos.AddScoped<CasoUsoRevogarSessoes>();
        servicos.AddScoped<CasoUsoConcederPermissao>();
        servicos.AddScoped<CasoUsoRevogarPermissao>();
        servicos.AddScoped<CasoUsoLimparSessoes>();

        return servicos;
    }
}
