namespace App.Api.Health;

using System.Text.Json.Serialization;

/// <summary>
/// Representa a resposta do endpoint de liveness (/health/live).
/// </summary>
public sealed record RespostaLiveness(
    [property: JsonPropertyName("status")] string Status = "alive");

/// <summary>
/// Representa a resposta do endpoint de readiness (/health/ready) quando saudável.
/// </summary>
public sealed record RespostaReadiness(
    [property: JsonPropertyName("status")] string Status = "ready");
