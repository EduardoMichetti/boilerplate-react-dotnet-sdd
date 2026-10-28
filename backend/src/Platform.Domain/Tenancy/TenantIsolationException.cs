namespace Platform.Domain.Tenancy;

public sealed class TenantIsolationException()
    : Exception("The configured database identity does not match this installation.");
