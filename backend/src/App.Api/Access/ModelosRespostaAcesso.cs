namespace App.Api.Access;

using System.Text.Json.Serialization;

/// <summary>
/// <c>AntiforgeryToken</c> do contrato.
/// </summary>
/// <param name="TokenRequisicao">Token para o header <c>X-CSRF-TOKEN</c>.</param>
/// <param name="NomeCabecalho">Sempre <c>X-CSRF-TOKEN</c>.</param>
public sealed record RespostaTokenAntiforgery(
    [property: JsonPropertyName("requestToken")] string TokenRequisicao,
    [property: JsonPropertyName("headerName")] string NomeCabecalho);

/// <summary>
/// <c>LoginResponse</c> do contrato.
/// </summary>
/// <param name="IdTenant">Tenant da instalação.</param>
/// <param name="ExpiraEm">Prazo absoluto da sessão emitida.</param>
public sealed record RespostaLogin(
    [property: JsonPropertyName("tenantId")] Guid IdTenant,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiraEm);

/// <summary>
/// <c>CurrentUser</c> do contrato.
/// </summary>
/// <param name="Id">Conta.</param>
/// <param name="IdTenant">Tenant.</param>
/// <param name="Email">Email.</param>
/// <param name="NomeExibicao">Nome de exibição.</param>
/// <param name="SessaoExpiraEm">Prazo absoluto da sessão corrente.</param>
public sealed record RespostaUsuarioAtual(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid IdTenant,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string NomeExibicao,
    [property: JsonPropertyName("sessionExpiresAt")] DateTimeOffset SessaoExpiraEm);

/// <summary>
/// <c>TenantSummary</c> do contrato.
/// </summary>
/// <param name="Id">Tenant.</param>
/// <param name="NomeExibicao">Nome de apresentação.</param>
public sealed record RespostaResumoTenant(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("displayName")] string NomeExibicao);

/// <summary>
/// <c>TenantContext</c> do contrato.
/// </summary>
/// <param name="Usuario">Identidade corrente.</param>
/// <param name="Tenant">Instalação.</param>
/// <param name="Permissoes">Concessões vigentes.</param>
/// <param name="VersaoAplicacao"><c>AssemblyInformationalVersion</c> do <c>App.Api</c>.</param>
public sealed record RespostaContextoTenant(
    [property: JsonPropertyName("user")] RespostaUsuarioAtual Usuario,
    [property: JsonPropertyName("tenant")] RespostaResumoTenant Tenant,
    [property: JsonPropertyName("permissions")] IReadOnlyList<string> Permissoes,
    [property: JsonPropertyName("applicationVersion")] string VersaoAplicacao);
