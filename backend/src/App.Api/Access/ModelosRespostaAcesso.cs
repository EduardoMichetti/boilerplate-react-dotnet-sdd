namespace App.Api.Access;

using System.ComponentModel.DataAnnotations;
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
/// <param name="TetoAbsolutoEm">Teto absoluto da sessão emitida (nunca estendido pelo uso).</param>
/// <param name="JanelaInatividadeMinutos">Janela de inatividade fixada na emissão (5–240).</param>
public sealed record RespostaLogin(
    [property: JsonPropertyName("tenantId")] Guid IdTenant,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset TetoAbsolutoEm,
    [property: JsonPropertyName("idleTimeoutMinutes"), Range(5, 240)] int JanelaInatividadeMinutos);

/// <summary>
/// <c>CurrentUser</c> do contrato.
/// </summary>
/// <param name="Id">Conta.</param>
/// <param name="IdTenant">Tenant.</param>
/// <param name="Email">Email.</param>
/// <param name="NomeExibicao">Nome de exibição.</param>
/// <param name="SessaoExpiraEm">Teto absoluto da sessão corrente (a expiração por inatividade não é exposta).</param>
/// <param name="SessaoJanelaInatividadeMinutos">Janela de inatividade da sessão corrente (5–240).</param>
public sealed record RespostaUsuarioAtual(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid IdTenant,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string NomeExibicao,
    [property: JsonPropertyName("sessionExpiresAt")] DateTimeOffset SessaoExpiraEm,
    [property: JsonPropertyName("sessionIdleTimeoutMinutes"), Range(5, 240)] int SessaoJanelaInatividadeMinutos);

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
