using System.Data.Common;
using Platform.Domain.Tenancy;

namespace Platform.Application.Configuration;

public sealed class InstallationTenantOptions
{
    public string? ApplicationId { get; init; }
    public Guid EnvironmentId { get; init; }
    public Guid TenantId { get; init; }
    public string? TenantDisplayName { get; init; }
    public TenantState? TenantState { get; init; }
    public InstallationConnectionStrings? ConnectionStrings { get; init; }

    public void Validate()
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(ApplicationId) || ApplicationId.Length > 100)
        {
            errors.Add("ApplicationId must contain 1 to 100 characters.");
        }

        if (EnvironmentId == Guid.Empty)
        {
            errors.Add("EnvironmentId must be a non-empty UUID.");
        }

        if (TenantId == Guid.Empty)
        {
            errors.Add("TenantId must be a non-empty UUID.");
        }

        if (string.IsNullOrWhiteSpace(TenantDisplayName) || TenantDisplayName.Length > 120)
        {
            errors.Add("TenantDisplayName must contain 1 to 120 characters.");
        }

        if (TenantState is not { } state || !Enum.IsDefined(state))
        {
            errors.Add("TenantState must explicitly be Inactive or Active.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionStrings?.AppDb))
        {
            errors.Add("ConnectionStrings:AppDb is required.");
        }
        else
        {
            try
            {
                _ = new DbConnectionStringBuilder { ConnectionString = ConnectionStrings.AppDb };
            }
            catch (ArgumentException)
            {
                errors.Add("ConnectionStrings:AppDb has invalid syntax.");
            }
        }

        if (errors.Count != 0)
        {
            throw new InstallationConfigurationException(errors);
        }
    }
}

public sealed class InstallationConnectionStrings
{
    public string? AppDb { get; init; }
}
