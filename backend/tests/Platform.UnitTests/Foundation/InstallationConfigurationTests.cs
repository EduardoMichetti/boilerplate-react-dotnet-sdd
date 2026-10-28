using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Xunit;

namespace Platform.UnitTests.Foundation;

public sealed class InstallationConfigurationTests
{
    [Theory]
    [InlineData(TenantState.Inactive)]
    [InlineData(TenantState.Active)]
    public void ValidConfigurationAcceptsBothOperationalStates(TenantState state)
    {
        CreateOptions(state: state).Validate();
    }

    [Fact]
    public void MissingConfigurationReportsEveryRequiredFieldWithoutValues()
    {
        InstallationConfigurationException exception =
            Assert.Throws<InstallationConfigurationException>(() => new InstallationTenantOptions().Validate());

        Assert.Equal(6, exception.Errors.Count);
        Assert.Contains(exception.Errors, error => error.Contains("ApplicationId", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("ConnectionStrings:AppDb", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-connection-string")]
    public void InvalidConnectionStringIsRejectedWithoutLeakingItsValue(string connectionString)
    {
        InstallationConfigurationException exception = Assert.Throws<InstallationConfigurationException>(
            () => CreateOptions(connectionString: connectionString).Validate());

        Assert.Contains(exception.Errors, error => error.Contains("ConnectionStrings:AppDb", StringComparison.Ordinal));
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(100, 120, true)]
    [InlineData(101, 120, false)]
    [InlineData(100, 121, false)]
    public void TextLengthsRespectDeclaredBounds(int applicationLength, int displayLength, bool valid)
    {
        InstallationTenantOptions options = CreateOptions(
            applicationId: new string('a', applicationLength),
            displayName: new string('b', displayLength));

        if (valid)
        {
            options.Validate();
        }
        else
        {
            Assert.Throws<InstallationConfigurationException>(options.Validate);
        }
    }

    [Fact]
    public void EmptyIdentifiersAndUnknownStateAreRejected()
    {
        InstallationTenantOptions options = new()
        {
            ApplicationId = "test-product",
            TenantDisplayName = "Synthetic installation",
            TenantState = (TenantState)42,
            ConnectionStrings = new() { AppDb = "Host=localhost;Database=synthetic;Username=synthetic" }
        };

        InstallationConfigurationException exception = Assert.Throws<InstallationConfigurationException>(options.Validate);

        Assert.Equal(3, exception.Errors.Count);
    }

    [Fact]
    public void MalformedConnectionStringDoesNotAppearInDiagnostics()
    {
        const string privateValue = "synthetic-private-value";
        InstallationTenantOptions options = CreateOptions(
            connectionString: $"Password={privateValue};malformed");

        InstallationConfigurationException exception = Assert.Throws<InstallationConfigurationException>(options.Validate);

        Assert.DoesNotContain(privateValue, exception.ToString(), StringComparison.Ordinal);
        Assert.All(exception.Errors, error => Assert.DoesNotContain(privateValue, error, StringComparison.Ordinal));
    }

    private static InstallationTenantOptions CreateOptions(
        TenantState state = TenantState.Active,
        string applicationId = "test-product",
        string displayName = "Synthetic installation",
        string connectionString = "Host=localhost;Database=synthetic;Username=synthetic")
    {
        return new()
        {
            ApplicationId = applicationId,
            EnvironmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TenantDisplayName = displayName,
            TenantState = state,
            ConnectionStrings = new() { AppDb = connectionString }
        };
    }
}
