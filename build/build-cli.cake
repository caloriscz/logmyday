var target = Argument("target", "Install");

var projectFile = "../src/LogMyDay.Cli/LogMyDay.Cli.csproj";
var outputDir   = "./artifacts/cli";

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        if (DirectoryExists(outputDir))
        {
            DeleteDirectory(outputDir, new DeleteDirectorySettings { Recursive = true });
        }

        CreateDirectory(outputDir);
    });

Task("Pack")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        var settings = new DotNetPackSettings
        {
            Configuration = "Release",
            OutputDirectory = outputDir,
            NoBuild = false,
            ArgumentCustomization = args => args.Append("--nologo")
        };

        DotNetPack(projectFile, settings);
    });

Task("Install")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        // Always uninstall first so same-version rebuilds are picked up
        StartProcess("dotnet", new ProcessSettings
        {
            Arguments = "tool uninstall -g LogMyDay.Cli"
        });

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"tool install -g LogMyDay.Cli --add-source {outputDir}"
        });

        if (result != 0)
        {
            throw new Exception("dotnet tool install failed.");
        }

        Information("");
        Information("Done. Run 'lmd --help' to verify.");
    });

Task("Uninstall")
    .Does(() =>
    {
        StartProcess("dotnet", new ProcessSettings
        {
            Arguments = "tool uninstall -g LogMyDay.Cli"
        });
    });

///////////////////////////////////////////////////////////////////////////////
// DEFAULT
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
