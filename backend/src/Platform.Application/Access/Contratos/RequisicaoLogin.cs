namespace Platform.Application.Access.Contratos;

using System.Text.Json.Serialization;

/// <summary>
/// Corpo de <c>POST /api/v1/sessions</c> (LoginRequest do contrato: <c>email</c>, <c>password</c>).
/// Ausente vira vazio e é rejeitado pelo validador; <c>null</c> explícito é rejeitado na desserialização.
/// </summary>
public sealed class RequisicaoLogin
{
    /// <summary>
    /// Email da conta local (até 254).
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha informada (1 a 128); nunca registrada em log nem ecoada.
    /// </summary>
    [JsonPropertyName("password")]
    public string Senha { get; init; } = string.Empty;
}
