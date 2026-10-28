namespace Platform.Application.Tenancy;

public interface ITenantDatabaseIdentityProbe
{
    Task<TenantDatabaseIdentitySnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseIdentitySnapshot(
    Guid TenantId,
    Guid EnvironmentId,
    Guid AccessEpoch,
    bool HasExpectedMigrationVersion);
