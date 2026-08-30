using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using msTools.Updater;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class MsToolsUpdaterIntegrationTests : IDisposable
{
    private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"vwp-updater-{Guid.NewGuid():N}");

    [Fact]
    public async Task ManualCheckAsync_ClearsPreviousLastErrorInUpdaterStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceDirectory = Path.Combine(testRoot, "source");
        var downloadDirectory = Path.Combine(testRoot, "updates");
        var platform = GetCurrentPlatform();
        var runtimeIdentifier = GetCurrentRuntimeIdentifier(platform);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(downloadDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(testRoot, "release-metadata.json"),
            $$"""
            {
              "version": "1.0.0",
              "publishedAt": "2026-08-29T10:00:00Z",
              "commitSha": "installed",
              "repository": "test",
              "runtimeIdentifier": "{{runtimeIdentifier}}"
            }
            """,
            cancellationToken);

        var packageFileName = $"VideoWebPlayer-{runtimeIdentifier}.zip";
        var packagePath = Path.Combine(sourceDirectory, packageFileName);
        await File.WriteAllTextAsync(packagePath, "test package", cancellationToken);

        var packageHash = await ComputeSha256Async(packagePath, cancellationToken);
        var packageUri = new Uri(packagePath).AbsoluteUri;
        var packageSize = new FileInfo(packagePath).Length;

        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, "update.json"),
            $$"""
            {
              "version": "1.1.0",
              "publishedAt": "2026-08-30T10:00:00Z",
              "commitSha": "available",
              "repository": "test",
              "isPrerelease": false,
              "packages": [
                {
                  "version": "1.1.0",
                  "platform": "{{platform}}",
                  "runtimeIdentifier": "{{runtimeIdentifier}}",
                  "fileName": "{{packageFileName}}",
                  "uri": "{{packageUri}}",
                  "sha256": "{{packageHash}}",
                  "sizeBytes": {{packageSize}}
                }
              ]
            }
            """,
            cancellationToken);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = testRoot,
            EnvironmentName = "Testing",
        });

        builder.UseAutoUpdate(cfg =>
        {
            cfg.WithDownloadPath(downloadDirectory)
                .UseLocalFolderSource(sourceDirectory, manifestFileName: null)
                .DisableHostedServices();
        });

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var statusService = serviceProvider.GetRequiredService<AutoUpdateStatusService>();
        var orchestrator = serviceProvider.GetRequiredService<IAutoUpdateOrchestrator>();
        var commandHandler = serviceProvider.GetRequiredService<IAutoUpdateCommandHandler>();

        await statusService.UpdateAsync(
            status => status with
            {
                State = AutoUpdateState.Failed,
                LastError = "Alter Fehler",
                LastErrorCode = AutoUpdateErrorCode.SourceUnavailable,
            },
            cancellationToken);

        var before = await orchestrator.GetStatusAsync(cancellationToken);
        Assert.Equal("Alter Fehler", before.LastError);
        Assert.Equal(AutoUpdateErrorCode.SourceUnavailable, before.LastErrorCode);

        var result = await commandHandler.CheckAsync(cancellationToken);
        var after = await orchestrator.GetStatusAsync(cancellationToken);

        Assert.NotEqual(AutoUpdateOutcome.Failed, result.Outcome);
        Assert.Null(after.LastError);
        Assert.True(after.LastErrorCode is null or AutoUpdateErrorCode.None);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "windows";

        if (OperatingSystem.IsLinux())
            return "linux";

        throw new PlatformNotSupportedException("msTools.Updater installations are supported on Windows and Linux.");
    }

    private static string GetCurrentRuntimeIdentifier(string platform)
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };

        return platform == "windows" ? $"win-{architecture}" : $"{platform}-{architecture}";
    }
}
