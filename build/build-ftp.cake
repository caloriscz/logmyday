#addin nuget:?package=FluentFTP&version=34.0.0
#addin nuget:?package=Newtonsoft.Json&version=13.0.1

using FluentFTP;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.IO;

public class Config
{
    public string FtpHost { get; set; }
    public string FtpUsername { get; set; }
    public string FtpPassword { get; set; }
    public string FtpRemoteDirectory { get; set; }
    public string CleanDirectory { get; set; }
    public string OfflineFilePath { get; set; }
    public string PublishDirectory { get; set; }
    public string PublishProfilePath { get; set; }
    public string ProjectPath { get; set; }
}

var configFilePath = "./build-ftp.json";
var configJson = System.IO.File.ReadAllText(configFilePath);
var config = JsonConvert.DeserializeObject<Config>(configJson);

// Use Cake's MakeAbsolute to resolve the project path from the build directory to the project directory
config.ProjectPath = MakeAbsolute(File("../src/LogMyDay.App/LogMyDay.App.csproj")).FullPath;

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(config.CleanDirectory);
    });

Task("AddOfflineFile")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        var offlineContent = @"<!DOCTYPE html>
<html>
<head>
    <title>Application Offline</title>
</head>
<body>
    <h1>The application is offline for maintenance.</h1>
</body>
</html>";
        var directoryPath = System.IO.Path.GetDirectoryName(config.OfflineFilePath);
        EnsureDirectoryExists(directoryPath);
        System.IO.File.WriteAllText(config.OfflineFilePath, offlineContent);
        Information($"Created offline file at {config.OfflineFilePath}");
    });

Task("Build")
    .IsDependentOn("AddOfflineFile")
    .Does(() =>
    {
        StartProcess("dotnet", new ProcessSettings 
        {
            Arguments = $"build {config.ProjectPath} --configuration Release"
        });
    });

Task("Publish")
    .IsDependentOn("Build")
    .Does(() =>
    {
        StartProcess("dotnet", new ProcessSettings 
        {
            Arguments = $"publish {config.ProjectPath} --configuration Release -o {config.PublishDirectory} --no-build"
        });
    });

Task("CleanRemote")
    .IsDependentOn("Publish")
    .Does(async () =>
    {
        if (string.IsNullOrEmpty(config.FtpPassword))
        {
            throw new ArgumentException("FTP password must be provided in the config file.");
        }

        using var client = new FluentFTP.FtpClient(config.FtpHost, new NetworkCredential(config.FtpUsername, config.FtpPassword));
        await client.ConnectAsync();
        LogInformation($"Connected to FTP server {config.FtpHost}");

        if (!config.FtpRemoteDirectory.EndsWith("wwwroot") && !config.FtpRemoteDirectory.Contains("site"))
        {
            throw new InvalidOperationException("Remote directory path seems unsafe to clean. Please double-check.");
        }

        LogInformation($"Cleaning remote directory: {config.FtpRemoteDirectory}");

        var remoteItems = await client.GetListingAsync(config.FtpRemoteDirectory, FluentFTP.FtpListOption.Recursive);

        foreach (var item in remoteItems.OrderByDescending(i => i.FullName))
        {
            try
            {
                if (item.Type.ToString().ToLowerInvariant().Contains("file"))
                {
                    await client.DeleteFileAsync(item.FullName);
                    LogInformation($"Deleted file: {item.FullName}");
                }
                else if (item.Type.ToString().ToLowerInvariant().Contains("dir"))
                {
                    await client.DeleteDirectoryAsync(item.FullName);
                    LogInformation($"Deleted directory: {item.FullName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete {item.FullName}: {ex.Message}");
            }
        }

        LogInformation("Remote directory cleaned.");
    });

Task("UploadToFTP")
    .IsDependentOn("CleanRemote")
    .Does(async () =>
    {
        if (string.IsNullOrEmpty(config.FtpPassword))
        {
            throw new ArgumentException("FTP password must be provided in the config file.");
        }

        if (!System.IO.File.Exists(config.OfflineFilePath))
        {
            throw new FileNotFoundException("Offline file not found", config.OfflineFilePath);
        }

        try
        {
            using var client = new FtpClient(config.FtpHost, new NetworkCredential(config.FtpUsername, config.FtpPassword));
            await client.ConnectAsync();
            LogInformation($"Connecting to FTP server {config.FtpHost}");
            
            var remoteOfflineFilePath = config.FtpRemoteDirectory + "/app_offline.htm";
            LogInformation($"Uploading {config.OfflineFilePath} to {remoteOfflineFilePath}");
            var uploadResult = await client.UploadFileAsync(config.OfflineFilePath, remoteOfflineFilePath);
            
            if (uploadResult == FtpStatus.Success)
            {
                LogInformation($"Successfully uploaded {config.OfflineFilePath} to {remoteOfflineFilePath}");
            }
            else
            {
                throw new Exception($"Failed to upload {config.OfflineFilePath} to {remoteOfflineFilePath}");
            }

            var files = System.IO.Directory.GetFiles(config.PublishDirectory, "*.*", System.IO.SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int filesLeft = totalFiles;
            LogInformation($"Total files to upload: {totalFiles}");

            foreach (var file in files)
            {
                var remotePath = config.FtpRemoteDirectory + file.Replace(config.PublishDirectory, "").Replace("\\", "/");
                var remoteDirectory = System.IO.Path.GetDirectoryName(remotePath).Replace("\\", "/");
                await client.CreateDirectoryAsync(remoteDirectory);

                if (await client.FileExistsAsync(remotePath))
                {
                    await client.DeleteFileAsync(remotePath);
                }

                try
                {
                    uploadResult = await client.UploadFileAsync(file, remotePath);

                    if (uploadResult == FtpStatus.Success)
                    {
                        LogInformation($"Successfully uploaded {file} to {remotePath}");
                    }
                    else
                    {
                        throw new Exception($"Failed to upload {file} to {remotePath}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error uploading file {file} to {remotePath}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        LogError($"Inner Exception: {ex.InnerException.Message}");
                    }
                }

                filesLeft--;
                UpdateProgressBar(totalFiles, totalFiles - filesLeft);
            }

            LogInformation($"Deleting {remoteOfflineFilePath} from FTP server");
            await client.DeleteFileAsync(remoteOfflineFilePath);
            LogInformation($"Successfully deleted {remoteOfflineFilePath} from FTP server");
        }
        catch (Exception ex)
        {
            LogError($"Error while uploading the file to the server: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogError($"Inner Exception: {ex.InnerException.Message}");
            }
            throw;
        }
    });

Task("RemoveOfflineFile")
    .IsDependentOn("UploadToFTP")
    .Does(() =>
    {
        if (System.IO.File.Exists(config.OfflineFilePath))
        {
            DeleteFile(config.OfflineFilePath);
            Information($"Deleted offline file at {config.OfflineFilePath}");
        }
        else
        {
            Warning($"Offline file {config.OfflineFilePath} does not exist.");
        }
    });

Task("Default")
    .IsDependentOn("RemoveOfflineFile");

RunTarget("Default");

void UpdateProgressBar(int total, int current)
{
    int progressBarWidth = 50;
    int progress = (int)((double)current / total * progressBarWidth);
    string progressBar = "[" + new string('#', progress) + new string(' ', progressBarWidth - progress) + $"] {current}/{total}";
    ClearConsoleLine();
    Console.Write(progressBar);
    Console.SetCursorPosition(0, Console.CursorTop);
}

void ClearConsoleLine()
{
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(new string(' ', Console.WindowWidth));
    Console.SetCursorPosition(0, Console.CursorTop);
}

void LogInformation(string message)
{
    ClearConsoleLine();
    Console.WriteLine(message);
    UpdateProgressBar(1, 0);
}

void LogError(string message)
{
    ClearConsoleLine();
    Console.WriteLine(message);
    UpdateProgressBar(1, 0);
}