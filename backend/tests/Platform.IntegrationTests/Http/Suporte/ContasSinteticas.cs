namespace Platform.IntegrationTests.Http.Suporte;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Access;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Xunit;

/// <summary>
/// Preparação de contas e concessões sintéticas via <c>UserManager</c> com o login individual de
/// operador (o runtime não tem INSERT em contas) e leituras/ajustes de cenário com o owner.
/// Sem host operacional.
/// </summary>
public static class ContasSinteticas
{
    /// <summary>Senha válida pela política (12–128, maiúscula, minúscula, dígito e símbolo).</summary>
    public const string SenhaPadraoA = "SenhaValidaA#2026";

    /// <summary>Outra senha válida (instalação B).</summary>
    public const string SenhaPadraoB = "SenhaValidaB#2026";

    /// <summary>
    /// Gera um email único por teste.
    /// </summary>
    /// <param name="prefixo">Prefixo legível.</param>
    /// <returns>Email.</returns>
    public static string NovoEmail(string prefixo) => $"{prefixo}.{Guid.NewGuid():N}@teste.local";

    /// <summary>
    /// Cria uma conta ativa com lockout habilitado e, opcionalmente, <c>foundation.context.read</c>.
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="relogio">Relógio do teste.</param>
    /// <param name="email">Email.</param>
    /// <param name="senha">Senha.</param>
    /// <param name="conceder">Concede <c>foundation.context.read</c>.</param>
    /// <param name="nome">Nome de exibição.</param>
    /// <returns>Id da conta.</returns>
    public static async Task<Guid> CriarContaAsync(
        InstalacaoTestePostgres instalacao,
        TimeProvider relogio,
        string email,
        string senha,
        bool conceder = true,
        string nome = "Pessoa Sintética")
    {
        Guid id = IdentificadorV7.Gerar(InstanteUtc.Normalizar(relogio.GetUtcNow()));
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(
            instalacao.StringConexaoOperadorUm,
            ContextoOperacaoTeste.Criar(relogio, TipoAutor.Operador, instalacao.LoginOperadorUm));

        UsuarioAplicacao usuario = new()
        {
            Id = id,
            FkIdTenant = instalacao.IdTenant,
            Email = email,
            UserName = email,
            NomeExibicao = nome,
            EstaAtivo = true,
        };
        IdentityResult criado = await escopo.GerenciadorUsuarios.CreateAsync(usuario, senha);
        Assert.True(criado.Succeeded, string.Join(", ", criado.Errors.Select(e => e.Code)));
        Assert.True(usuario.LockoutEnabled);

        if (conceder)
        {
            escopo.Contexto.Permissoes.Add(new PermissaoUsuario { FkIdUsuario = id, CodigoPermissao = CodigosPermissao.FundacaoContextoLer });
            await escopo.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return id;
    }

    /// <summary>
    /// Retira <c>foundation.context.read</c> (DELETE rastreado pelo operador).
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="relogio">Relógio.</param>
    /// <param name="idUsuario">Conta.</param>
    /// <returns>Tarefa.</returns>
    public static async Task RevogarPermissaoAsync(InstalacaoTestePostgres instalacao, TimeProvider relogio, Guid idUsuario)
    {
        await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(
            instalacao.StringConexaoOperadorUm,
            ContextoOperacaoTeste.Criar(relogio, TipoAutor.Operador, instalacao.LoginOperadorUm));
        PermissaoUsuario concessao = await contexto.Permissoes.SingleAsync(
            p => p.FkIdUsuario == idUsuario && p.CodigoPermissao == CodigosPermissao.FundacaoContextoLer,
            TestContext.Current.CancellationToken);
        contexto.Permissoes.Remove(concessao);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Altera a conta pelo operador (desativar, bloquear etc.).
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="relogio">Relógio.</param>
    /// <param name="idUsuario">Conta.</param>
    /// <param name="alteracao">Alteração aplicada.</param>
    /// <returns>Tarefa.</returns>
    public static async Task AlterarContaAsync(InstalacaoTestePostgres instalacao, TimeProvider relogio, Guid idUsuario, Action<UsuarioAplicacao> alteracao)
    {
        ArgumentNullException.ThrowIfNull(alteracao);
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(
            instalacao.StringConexaoOperadorUm,
            ContextoOperacaoTeste.Criar(relogio, TipoAutor.Operador, instalacao.LoginOperadorUm));
        UsuarioAplicacao usuario = (await escopo.GerenciadorUsuarios.FindByIdAsync(idUsuario.ToString()))!;
        alteracao(usuario);
        IdentityResult resultado = await escopo.GerenciadorUsuarios.UpdateAsync(usuario);
        Assert.True(resultado.Succeeded);
    }

    /// <summary>
    /// Troca a <c>EpocaAcesso</c> do marcador com o owner (simula restauração).
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <returns>Tarefa.</returns>
    public static Task RenovarEpocaAsync(InstalacaoTestePostgres instalacao) =>
        ExecutarComoProprietarioAsync(instalacao, $"UPDATE foundation.identidade_banco SET \"EpocaAcesso\" = '{Guid.NewGuid()}'");

    /// <summary>
    /// Executa SQL de preparação de cenário com o owner.
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="sql">Comando.</param>
    /// <returns>Tarefa.</returns>
    public static async Task ExecutarComoProprietarioAsync(InstalacaoTestePostgres instalacao, string sql)
    {
        await using NpgsqlConnection conexao = new(instalacao.StringConexaoProprietario);
        await conexao.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Lê as sessões da conta com o owner, sem rastreamento.
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="idUsuario">Conta.</param>
    /// <returns>Sessões ordenadas pela criação.</returns>
    public static async Task<List<SessaoAutenticacao>> SessoesAsync(InstalacaoTestePostgres instalacao, Guid idUsuario)
    {
        await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(instalacao.StringConexaoProprietario);
        return await contexto.Sessoes.AsNoTracking()
            .Where(s => s.FkIdUsuario == idUsuario)
            .OrderBy(s => s.CriadoEm).ThenBy(s => s.IdSessao)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Lê a conta com o owner, sem rastreamento.
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="idUsuario">Conta.</param>
    /// <returns>Conta.</returns>
    public static async Task<UsuarioAplicacao> ContaAsync(InstalacaoTestePostgres instalacao, Guid idUsuario)
    {
        await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(instalacao.StringConexaoProprietario);
        return await contexto.Users.AsNoTracking().SingleAsync(u => u.Id == idUsuario, TestContext.Current.CancellationToken);
    }
}
