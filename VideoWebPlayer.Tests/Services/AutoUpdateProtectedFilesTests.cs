using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using msTools.Updater;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class AutoUpdateProtectedFilesTests : IDisposable
{
    private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"vwp-protected-files-{Guid.NewGuid():N}");

    [Fact]
    public void ConfigurationBindsProtectedFiles()
    {
        Directory.CreateDirectory(testRoot);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = testRoot,
            EnvironmentName = "Testing",
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AutoUpdate:ProtectedFiles:0:Path"] = "web.config",
            ["AutoUpdate:ProtectedFiles:0:Strategy"] = "Merge",
            ["AutoUpdate:ProtectedFiles:0:XmlElements:0"] = "//aspNetCore/environmentVariables",
            ["AutoUpdate:ProtectedFiles:0:XmlElements:1"] = "//security/ipSecurity",
            ["AutoUpdate:ProtectedFiles:0:XmlAttributes:0"] = "//aspNetCore@requestTimeout",
            ["AutoUpdate:ProtectedFiles:1:Path"] = "appsettings*.json",
            ["AutoUpdate:ProtectedFiles:1:Strategy"] = "Merge",
            ["AutoUpdate:ProtectedFiles:1:JsonKeys:0"] = "Jwt:Key",
            ["AutoUpdate:ProtectedFiles:1:JsonKeys:1"] = "ConnectionStrings:DefaultConnection",
        });

        builder.UseAutoUpdate(cfg => cfg.DisableHostedServices());

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<AutoUpdateOptions>();

        Assert.Equal(2, options.ProtectedFiles.Count);

        var webConfig = options.ProtectedFiles[0];
        Assert.Equal("web.config", webConfig.Path);
        Assert.Equal(AutoUpdateProtectedFileStrategy.Merge, webConfig.Strategy);
        Assert.Equal(new[] { "//aspNetCore/environmentVariables", "//security/ipSecurity" }, webConfig.XmlElements);
        Assert.Equal(new[] { "//aspNetCore@requestTimeout" }, webConfig.XmlAttributes);
        Assert.Empty(webConfig.JsonKeys);

        var appsettings = options.ProtectedFiles[1];
        Assert.Equal("appsettings*.json", appsettings.Path);
        Assert.Equal(AutoUpdateProtectedFileStrategy.Merge, appsettings.Strategy);
        Assert.Equal(new[] { "Jwt:Key", "ConnectionStrings:DefaultConnection" }, appsettings.JsonKeys);
        Assert.Empty(appsettings.XmlElements);
        Assert.Empty(appsettings.XmlAttributes);
    }

    [Fact]
    public void ShippedAppsettingsProtectedFiles_AreValid()
    {
        var options = LoadShippedAutoUpdateOptions();

        var result = new AutoUpdateOptionsValidator().Validate("AutoUpdate", options);

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Equal(2, options.ProtectedFiles.Count);
    }

    [Fact]
    public void ShippedWebConfig_ContainsAllMergeParents()
    {
        var options = LoadShippedAutoUpdateOptions();
        var webConfigPath = FindRepositoryFile("VideoWebPlayer", "web.config");
        var webConfig = XDocument.Load(webConfigPath);
        var entry = Assert.Single(options.ProtectedFiles, file => file.Path == "web.config");

        foreach (var xpath in entry.XmlElements)
        {
            Assert.DoesNotContain('[', xpath);
            Assert.DoesNotContain('@', xpath);

            var parentPath = xpath[..xpath.LastIndexOf('/')];
            Assert.NotEmpty(webConfig.XPathSelectElements(parentPath));
        }

        foreach (var rule in entry.XmlAttributes)
        {
            var elementPath = rule[..rule.LastIndexOf('@')];
            Assert.NotEmpty(webConfig.XPathSelectElements(elementPath));
        }
    }

    [Fact]
    public async Task GeneratedScript_ContainsBackupAndMergeInOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var downloadDirectory = Path.Combine(testRoot, "updates");
        Directory.CreateDirectory(downloadDirectory);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = testRoot,
            EnvironmentName = "Testing",
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AutoUpdate:ProtectedFiles:0:Path"] = "web.config",
            ["AutoUpdate:ProtectedFiles:0:Strategy"] = "Merge",
            ["AutoUpdate:ProtectedFiles:0:XmlElements:0"] = "//aspNetCore/environmentVariables",
            ["AutoUpdate:ProtectedFiles:0:XmlAttributes:0"] = "//aspNetCore@requestTimeout",
            ["AutoUpdate:ProtectedFiles:1:Path"] = "appsettings*.json",
            ["AutoUpdate:ProtectedFiles:1:Strategy"] = "Merge",
            ["AutoUpdate:ProtectedFiles:1:JsonKeys:0"] = "Jwt:Key",
        });

        // Windows-Skript plattformunabhängig erzeugen (TryAdd in UseAutoUpdate greift nicht, wenn vorher registriert).
        builder.Services.AddSingleton<IAutoUpdatePlatformResolver>(
            new AutoUpdatePlatformResolver(_ => true, "win-x64"));

        builder.UseAutoUpdate(cfg => cfg
            .WithDownloadPath(downloadDirectory)
            .DisableHostedServices());

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var generator = serviceProvider.GetRequiredService<IAutoUpdateScriptGenerator>();
        var packageStore = serviceProvider.GetRequiredService<IAutoUpdatePackageStore>();

        var package = new AutoUpdatePackageDescriptor(
            "1.1.0",
            "windows",
            "win-x64",
            "VideoWebPlayer-win-x64.zip",
            new Uri("https://example.invalid/VideoWebPlayer-win-x64.zip"),
            "0123456789abcdef",
            12345);
        var target = new AutoUpdateInstallationTarget("windows", null, null, "VideoWebPlayerPool", "VideoWebPlayer");
        var zipPath = Path.Combine(downloadDirectory, "pending", "VideoWebPlayer-win-x64.zip");

        var scriptPath = await generator.GenerateAsync(package, zipPath, target, cancellationToken);
        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        Assert.Contains("$protectedFiles = @(", script);
        Assert.Contains("Path = 'web.config'", script);
        Assert.Contains("Path = 'appsettings*.json'", script);
        Assert.Contains("Strategy = 'Merge'", script);
        Assert.Contains("//aspNetCore/environmentVariables", script);
        Assert.Contains("//aspNetCore@requestTimeout", script);
        Assert.Contains("Jwt:Key", script);
        Assert.Contains(Path.Combine(packageStore.RootDirectory, "backup"), script);

        var backupIndex = script.IndexOf("Backup-AutoUpdateProtectedFiles -Files", StringComparison.Ordinal);
        var copyIndex = script.IndexOf("Copy-Item -Destination $app", StringComparison.Ordinal);
        var mergeIndex = script.IndexOf("Invoke-AutoUpdateProtectedFiles -Files", StringComparison.Ordinal);

        Assert.True(backupIndex >= 0, "Skript enthält keinen Backup-Aufruf für geschützte Dateien.");
        Assert.True(copyIndex > backupIndex, "Der Paketkopiervorgang muss nach dem Backup der geschützten Dateien liegen.");
        Assert.True(mergeIndex > copyIndex, "Der Merge-/Restore-Schritt muss nach dem Paketkopiervorgang liegen.");
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static AutoUpdateOptions LoadShippedAutoUpdateOptions()
    {
        var appsettingsPath = FindRepositoryFile("VideoWebPlayer", "appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appsettingsPath, optional: false, reloadOnChange: false)
            .Build();
        var options = new AutoUpdateOptions();
        configuration.GetSection("AutoUpdate").Bind(options);
        // Wie BuildOptions in UseAutoUpdate: ohne konfigurierte Quelle wird die lokale Standardquelle abgeleitet.
        options.Source ??= new AutoUpdateLocalFolderSource(Path.Combine(Path.GetTempPath(), "vwp-update-source"));
        return options;
    }

    private static string FindRepositoryFile(string projectDirectory, string fileName)
    {
        var relativePath = Path.Combine(projectDirectory, fileName);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Repository-Datei '{relativePath}' wurde oberhalb von '{AppContext.BaseDirectory}' nicht gefunden.");
    }
}
