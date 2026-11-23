namespace LogMyDay.App.Extensions;

/// <summary>
/// Docker secrets: the files in /run/secrets, read as configuration in the Docker environment.
/// </summary>
internal static class DockerSecretsExtensions
{
    internal const string SecretsFolder = "/run/secrets";

    internal static void AddDockerSecrets(this ConfigurationManager configuration, string folder = SecretsFolder)
    {
        configuration.AddKeyPerFile(directoryPath: folder, optional: true, reloadOnChange: false);

        var aiApiKey = configuration["ai_api_key"];
        if (!string.IsNullOrWhiteSpace(aiApiKey))
        {
            configuration["AI:ApiKey"] = aiApiKey;
        }

        // The first admin's password, used only by the first start of a new installation. A file
        // saved on Windows ends with "\r\n", and on Linux the provider strips only the "\n": the
        // password would end with an invisible character that nobody can type.
        var adminPassword = configuration["admin_password"]?.Trim();
        if (!string.IsNullOrEmpty(adminPassword))
        {
            configuration["Admin:InitialPassword"] = adminPassword;
        }
    }
}
