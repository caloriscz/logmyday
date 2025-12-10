using System;
using System.IO;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

// Load local environment variables if file exists
var localEnvFile = File("./local.env");
if (FileExists(localEnvFile))
{
    Information("Loading local environment variables from local.env");
    var lines = System.IO.File.ReadAllLines(localEnvFile.Path.FullPath);
    foreach (var line in lines)
    {
        if (!string.IsNullOrWhiteSpace(line) && line.Contains("=") && !line.StartsWith("#"))
        {
            var parts = line.Split(new char[] {'='}, 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, value);
                Information($"Set {key} from local.env");
            }
        }
    }
}

// Build Variables
var solutionFile = "../LogMyDay.Web.slnf"; // Use solution filter to exclude mobile
var projectFile = "../src/LogMyDay.App/LogMyDay.App.csproj";
var publishDirectory = "./publish";
var artifactsDirectory = "./artifacts";

// Helper function to find msdeploy.exe
string FindMSDeploy()
{
    var commonPaths = new[] {
        @"C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe",
        @"C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\msdeploy.exe",
        @"C:\Program Files\IIS\Microsoft Web Deploy\msdeploy.exe",
        @"C:\Program Files (x86)\IIS\Microsoft Web Deploy\msdeploy.exe"
    };
    
    foreach (var path in commonPaths)
    {
        if (System.IO.File.Exists(path))
        {
            Information($"Found msdeploy at: {path}");
            return path;
        }
    }
    
    // Try to find it via where command
    try
    {
        var whereResult = StartProcess("where", new ProcessSettings {
            Arguments = "msdeploy",
            RedirectStandardOutput = true
        });
        // If where succeeds, just use "msdeploy" (it's in PATH)
        if (whereResult == 0)
            return "msdeploy";
    }
    catch
    {
        // Ignore where command failure
    }
    
    throw new Exception("Could not find msdeploy.exe. Please install Web Deploy or add it to your PATH.");
}

// Web Deploy Variables from Environment (GitHub Secrets)
var deployServer = EnvironmentVariable("LMD_SERVER");
var deployPort = EnvironmentVariable("LMD_PORT") ?? "8172";
var deploySite = EnvironmentVariable("LMD_SITE");
var deployUsername = EnvironmentVariable("LMD_LOGIN");
var deployPassword = EnvironmentVariable("LMD_PASSWORD");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    Information("Running tasks...");
    Information($"Configuration: {configuration}");
    Information($"Target: {target}");
    
    if (string.IsNullOrEmpty(deployServer))
        Warning("LMD_SERVER environment variable is not set");
    if (string.IsNullOrEmpty(deploySite))
        Warning("LMD_SITE environment variable is not set");
    if (string.IsNullOrEmpty(deployUsername))
        Warning("LMD_LOGIN environment variable is not set");
    if (string.IsNullOrEmpty(deployPassword))
        Warning("LMD_PASSWORD environment variable is not set");
});

Teardown(ctx =>
{
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(publishDirectory);
    CleanDirectory(artifactsDirectory);
    
    if (DirectoryExists("../src/LogMyDay.App/bin"))
        CleanDirectory("../src/LogMyDay.App/bin");
    if (DirectoryExists("../src/LogMyDay.App/obj"))
        CleanDirectory("../src/LogMyDay.App/obj");
        
    Information("Cleaned directories");
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    var solutionPath = MakeAbsolute(File(solutionFile)).FullPath;
    var exitCode = StartProcess("dotnet", new ProcessSettings {
        Arguments = $"restore \"{solutionPath}\""
    });
    
    if (exitCode != 0)
        throw new Exception("dotnet restore failed");
        
    Information("NuGet packages restored");
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{
    var solutionPath = MakeAbsolute(File(solutionFile)).FullPath;
    var exitCode = StartProcess("dotnet", new ProcessSettings {
        Arguments = $"build \"{solutionPath}\" --configuration {configuration} --no-restore"
    });
    
    if (exitCode != 0)
        throw new Exception("dotnet build failed");
        
    Information("Build completed successfully");
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("========================================");
    Information("🧪 RUNNING TESTS - Deployment will be blocked if tests fail");
    Information("========================================");
    
    var testProjectPath = MakeAbsolute(File("../src/LogMyDay.Api.Tests/LogMyDay.Api.Tests.csproj")).FullPath;
    var exitCode = StartProcess("dotnet", new ProcessSettings {
        Arguments = $"test \"{testProjectPath}\" --configuration {configuration} --no-build --verbosity normal"
    });
    
    if (exitCode != 0)
    {
        Error("========================================");
        Error("❌ TESTS FAILED - DEPLOYMENT ABORTED");
        Error("========================================");
        throw new Exception("dotnet test failed - fix the failing tests before deploying");
    }
    
    Information("========================================");
    Information("✅ ALL TESTS PASSED - Safe to deploy");
    Information("========================================");
});

Task("Publish")
    .IsDependentOn("Test")
    .Does(() =>
{
    Information("📦 Publishing application...");
    var projectPath = MakeAbsolute(File(projectFile)).FullPath;
    var outputPath = MakeAbsolute(Directory(publishDirectory)).FullPath;
    var exitCode = StartProcess("dotnet", new ProcessSettings {
        Arguments = $"publish \"{projectPath}\" --configuration {configuration} --output \"{outputPath}\" --no-restore"
    });
    
    if (exitCode != 0)
        throw new Exception("dotnet publish failed");
        
    Information("Application published successfully");
});

Task("Package")
    .IsDependentOn("Publish")
    .Does(() =>
{
    EnsureDirectoryExists(artifactsDirectory);
    
    var packageFile = $"{artifactsDirectory}/LogMyDay.App.zip";
    
    Zip(publishDirectory, packageFile);
    
    Information($"Package created: {packageFile}");
    Information($"Package size: {(new FileInfo(packageFile).Length / 1024 / 1024):F2} MB");
});

Task("ValidateDeploymentConfig")
    .Does(() =>
{
    var errors = new List<string>();
    
    if (string.IsNullOrEmpty(deployServer))
        errors.Add("LMD_SERVER environment variable is required");
    if (string.IsNullOrEmpty(deploySite))
        errors.Add("LMD_SITE environment variable is required");
    if (string.IsNullOrEmpty(deployUsername))
        errors.Add("LMD_LOGIN environment variable is required");
    if (string.IsNullOrEmpty(deployPassword))
        errors.Add("LMD_PASSWORD environment variable is required");
    
    if (errors.Any())
    {
        Error("Deployment configuration validation failed:");
        foreach (var error in errors)
            Error($"  - {error}");
        throw new Exception("Deployment configuration is incomplete");
    }
    
    Information("Deployment configuration validated successfully");
    var displayServer = deployServer;
    if (!string.IsNullOrEmpty(deployPort) && !deployServer.Contains(":"))
        displayServer = $"{deployServer}:{deployPort}";
    Information($"Deploy Server: {displayServer}");
    Information($"Deploy Site: {deploySite}");
    Information($"Deploy Username: {deployUsername}");
});

Task("Deploy")
    .IsDependentOn("Package")
    .IsDependentOn("ValidateDeploymentConfig")
    .Does(() =>
{
    Information("Starting Web Deploy deployment...");
    
    var msdeployPath = FindMSDeploy();
    var wmsvcUrl = deployServer;  // Let the hosting provider handle the port
    var publishSource = MakeAbsolute(Directory(publishDirectory)).FullPath;
    
    // Use msdeploy directly to avoid circular dependency issues
    var arguments = $"-verb:sync " +
        $"-source:iisApp=\"{publishSource}\" " +
        $"-dest:iisApp={deploySite},wmsvc={wmsvcUrl},userName={deployUsername},password={deployPassword},authtype=basic " +
        $"-allowUntrusted=true " +
        $"-enableRule:AppOffline";
    
    try
    {
        Information($"Deploying to: {wmsvcUrl}");
        Information($"Site: {deploySite}");
        Information($"Username: {deployUsername}");
        Information($"Source: {publishSource}");
        
        var exitCode = StartProcess(msdeployPath, new ProcessSettings {
            Arguments = arguments
        });
        
        if (exitCode != 0)
            throw new Exception("msdeploy failed");
            
        Information("✅ Web Deploy deployment completed successfully!");
    }
    catch (Exception ex)
    {
        Error($"❌ Web Deploy deployment failed: {ex.Message}");
        
        // Provide troubleshooting information
        Information("Troubleshooting tips:");
        Information("1. Verify that Web Deploy is installed on the target server");
        Information("2. Check that the Management Service is running on the target server");
        Information("3. Verify firewall settings allow connections on the specified port");
        Information("4. Ensure the deployment credentials have sufficient permissions");
        Information("5. Verify msdeploy.exe is installed and in PATH");
        Information($"6. Test connection manually to: {wmsvcUrl}");
        
        throw;
    }
});

Task("FastDeploy")
    .IsDependentOn("Test")
    .IsDependentOn("ValidateDeploymentConfig")
    .Does(() =>
{
    Information("Starting fast deployment (tests passed)...");
    
    // First publish the application
    var projectPath = MakeAbsolute(File(projectFile)).FullPath;
    var outputPath = MakeAbsolute(Directory(publishDirectory)).FullPath;
    
    Information("Publishing application...");
    var publishExitCode = StartProcess("dotnet", new ProcessSettings {
        Arguments = $"publish \"{projectPath}\" --configuration {configuration} --output \"{outputPath}\" --no-restore"
    });
    
    if (publishExitCode != 0) {
        throw new Exception("dotnet publish failed");
    }
    
    // Deploy via msdeploy
    var msdeployPath = FindMSDeploy();
    
    // Build proper wmsvc parameter hosting providers often handle ports automatically
    var wmsvcUrl = deployServer;
    
    Information($"Using wmsvc URL: {wmsvcUrl}");
    
    var arguments = $"-verb:sync " +
        $"-source:iisApp=\"{outputPath}\" " +
        $"-dest:iisApp={deploySite},wmsvc={wmsvcUrl},userName={deployUsername},password={deployPassword},authtype=basic " +
        $"-allowUntrusted=true " +
        $"-enableRule:AppOffline";
    
    try
    {
        Information($"Deploying to: {wmsvcUrl}");
        Information($"Site: {deploySite}");
        Information($"Username: {deployUsername}");
        Information($"Source: {outputPath}");
        
        var exitCode = StartProcess(msdeployPath, new ProcessSettings {
            Arguments = arguments
        });
        
        if (exitCode != 0)
            throw new Exception("msdeploy failed");
            
        Information("✅ Fast deployment completed successfully!");
    }
    catch (Exception ex)
    {
        Error($"❌  Fast deployment failed: {ex.Message}");
        throw;
    }
});

Task("DeployUnsafe")
    .IsDependentOn("Build")
    .IsDependentOn("ValidateDeploymentConfig")
    .Does(() =>
{
    Warning("========================================");
    Warning("⚠️ UNSAFE DEPLOYMENT - BYPASSING TESTS");
    Warning("========================================");
    
    // First publish the application
    var projectPath = MakeAbsolute(File(projectFile)).FullPath;
    var outputPath = MakeAbsolute(Directory(publishDirectory)).FullPath;
    
    Information("Publishing application...");
    var publishExitCode = StartProcess("dotnet", new ProcessSettings {
        Arguments = $"publish \"{projectPath}\" --configuration {configuration} --output \"{outputPath}\" --no-restore"
    });
    
    if (publishExitCode != 0)
        throw new Exception("dotnet publish failed");
    
    // Then deploy using msdeploy
    var msdeployPath = FindMSDeploy();
    var wmsvcUrl = deployServer;
    
    var arguments = $"-verb:sync " +
        $"-source:iisApp=\"{outputPath}\" " +
        $"-dest:iisApp={deploySite},wmsvc={wmsvcUrl},userName={deployUsername},password={deployPassword},authtype=basic " +
        $"-allowUntrusted=true " +
        $"-enableRule:AppOffline";
    
    try
    {
        Information($"Deploying to: {wmsvcUrl}");
        Information($"Site: {deploySite}");
        
        var exitCode = StartProcess(msdeployPath, new ProcessSettings {
            Arguments = arguments
        });
        
        if (exitCode != 0)
            throw new Exception("msdeploy failed");
            
        Warning("⚠️ UNSAFE deployment completed - VERIFY MANUALLY!");
    }
    catch (Exception ex)
    {
        Error($"❌  Unsafe deployment failed: {ex.Message}");
        throw;
    }
});

///////////////////////////////////////////////////////////////////////////////
// TASK TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Deploy");
    
Task("CI")
    .IsDependentOn("Test")
    .IsDependentOn("Package");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);