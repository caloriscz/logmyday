using LogMyDay.App.Extensions;
using Microsoft.Extensions.Configuration;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// In Docker the secrets are files in /run/secrets. The first admin's password comes from the
/// admin_password secret, and has to be the password the user typed into the file.
/// </summary>
public sealed class DockerSecretsTests : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"lmd-docker-secrets-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    // The provider strips one Environment.NewLine only: "\r\n" on Windows, "\n" on Linux. Each
    // line-break case leaves a character behind on one of them.
    [Theory]
    [InlineData("first-admin-password\r\n")]
    [InlineData("first-admin-password\n")]
    [InlineData("  first-admin-password  ")]
    [InlineData("first-admin-password")]
    public void TheAdminPassword_IsTheTypedPassword_WithoutLineBreaksOrSurroundingSpaces(string content)
    {
        File.WriteAllText(Path.Combine(_folder, "admin_password"), content);

        Assert.Equal("first-admin-password", Load()["Admin:InitialPassword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    public void AnEmptySecret_SetsNoPassword(string content)
    {
        File.WriteAllText(Path.Combine(_folder, "admin_password"), content);

        Assert.Null(Load()["Admin:InitialPassword"]);
    }

    [Fact]
    public void AFolderInPlaceOfAMissingSecretFile_IsSkipped()
    {
        // Docker puts an empty folder in place of a secret whose file does not exist.
        Directory.CreateDirectory(Path.Combine(_folder, "admin_password"));

        Assert.Null(Load()["Admin:InitialPassword"]);
    }

    private ConfigurationManager Load()
    {
        var configuration = new ConfigurationManager();
        configuration.AddDockerSecrets(_folder);

        return configuration;
    }
}
