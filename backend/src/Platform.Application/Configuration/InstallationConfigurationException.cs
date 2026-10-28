namespace Platform.Application.Configuration;

public sealed class InstallationConfigurationException(IReadOnlyList<string> errors)
    : Exception("Installation configuration is invalid.")
{
    public IReadOnlyList<string> Errors { get; } = Array.AsReadOnly(errors.ToArray());
}
