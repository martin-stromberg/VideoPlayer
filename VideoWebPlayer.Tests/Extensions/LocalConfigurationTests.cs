using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using VideoWebPlayer.Extensions;
using Xunit;

namespace VideoWebPlayer.Tests.Extensions;

public sealed class LocalConfigurationTests : IDisposable
{
    private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"vwp-local-config-{Guid.NewGuid():N}");

    [Fact]
    public void LocalFile_IsInsertedBeforeUserSecretsAndEnvironmentVariables()
    {
        var builder = CreateBuilder();
        var lastAppSettingsIndex = LastAppSettingsJsonSourceIndex(builder.Configuration.Sources);
        Assert.True(lastAppSettingsIndex >= 0, "Es wurde keine appsettings*.json-Konfigurationsquelle gefunden.");

        builder.Configuration.Sources.Insert(lastAppSettingsIndex + 1, new JsonConfigurationSource
        {
            Path = "secrets.json",
            Optional = true,
            FileProvider = builder.Environment.ContentRootFileProvider,
        });

        builder.AddLocalJsonConfiguration();

        var sources = builder.Configuration.Sources;
        var localIndex = IndexOfSource(sources, source =>
            source is JsonConfigurationSource json
            && Path.GetFileName(json.Path) == "appsettings.Local.json");
        var secretsIndex = IndexOfSource(sources, source =>
            source is JsonConfigurationSource json
            && Path.GetFileName(json.Path) == "secrets.json");
        // Umgebungsvariablen/Kommandozeile erst hinter der letzten appsettings*.json-Quelle betrachten:
        // Die ASPNETCORE_-präfixierte Quelle des WebApplicationBuilder liegt bewusst davor.
        var environmentIndex = IndexOfSource(sources, source => source is EnvironmentVariablesConfigurationSource, lastAppSettingsIndex + 1);
        var commandLineIndex = IndexOfSource(sources, source => source is CommandLineConfigurationSource, lastAppSettingsIndex + 1);

        Assert.True(localIndex >= 0, "Die lokale Konfigurationsquelle wurde nicht eingefügt.");
        Assert.Equal(lastAppSettingsIndex + 1, localIndex);
        Assert.True(secretsIndex > localIndex, "Die lokale Quelle muss vor den User Secrets liegen.");
        if (environmentIndex >= 0)
        {
            Assert.True(environmentIndex > localIndex, "Die lokale Quelle muss vor den Umgebungsvariablen liegen.");
        }

        if (commandLineIndex >= 0)
        {
            Assert.True(commandLineIndex > localIndex, "Die lokale Quelle muss vor der Kommandozeile liegen.");
        }
    }

    [Fact]
    public void LocalFile_OverridesPackagedValues_WhenPresent()
    {
        WriteJson("appsettings.json", """{ "Test": { "Key": "packaged" } }""");
        WriteJson("appsettings.Local.json", """{ "Test": { "Key": "local" } }""");

        var builder = CreateBuilder();
        builder.AddLocalJsonConfiguration();

        Assert.Equal("local", builder.Configuration["Test:Key"]);
    }

    [Fact]
    public void LocalFile_Missing_IsIgnored()
    {
        WriteJson("appsettings.json", """{ "Test": { "Key": "packaged" } }""");

        var builder = CreateBuilder();
        builder.AddLocalJsonConfiguration();

        Assert.Equal("packaged", builder.Configuration["Test:Key"]);
    }

    [Fact]
    public void LocalFile_LosesAgainstUserSecrets()
    {
        WriteJson("appsettings.json", """{ "Test": { "Key": "packaged" } }""");
        WriteJson("appsettings.Local.json", """{ "Test": { "Key": "local" } }""");
        WriteJson("secrets.json", """{ "Test": { "Key": "secrets" } }""");

        var builder = CreateBuilder();
        var lastAppSettingsIndex = LastAppSettingsJsonSourceIndex(builder.Configuration.Sources);
        Assert.True(lastAppSettingsIndex >= 0, "Es wurde keine appsettings*.json-Konfigurationsquelle gefunden.");
        builder.Configuration.Sources.Insert(lastAppSettingsIndex + 1, new JsonConfigurationSource
        {
            Path = "secrets.json",
            Optional = false,
            FileProvider = builder.Environment.ContentRootFileProvider,
        });

        builder.AddLocalJsonConfiguration();

        Assert.Equal("secrets", builder.Configuration["Test:Key"]);
    }

    [Fact]
    public void LocalFile_LosesAgainstEnvironmentVariable()
    {
        WriteJson("appsettings.json", """{ "VwpLocalConfigTest": { "Key": "packaged" } }""");
        WriteJson("appsettings.Local.json", """{ "VwpLocalConfigTest": { "Key": "local" } }""");

        // Eigener Testschlüssel statt eines Produktionsschlüssels (z. B. Jwt__Key): Die
        // prozessweite Umgebungsvariable darf keine fremden, parallel laufenden Tests beeinflussen.
        Environment.SetEnvironmentVariable("VwpLocalConfigTest__Key", "environment");
        try
        {
            var builder = CreateBuilder();
            builder.AddLocalJsonConfiguration();

            Assert.Equal("environment", builder.Configuration["VwpLocalConfigTest:Key"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VwpLocalConfigTest__Key", null);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private WebApplicationBuilder CreateBuilder()
    {
        Directory.CreateDirectory(testRoot);
        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testRoot,
            EnvironmentName = "Testing",
            Args = [],
        });
    }

    private void WriteJson(string fileName, string content)
    {
        Directory.CreateDirectory(testRoot);
        File.WriteAllText(Path.Combine(testRoot, fileName), content);
    }

    private static int LastAppSettingsJsonSourceIndex(IList<IConfigurationSource> sources)
    {
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is JsonConfigurationSource json
                && Path.GetFileName(json.Path) is { } fileName
                && fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfSource(IList<IConfigurationSource> sources, Func<IConfigurationSource, bool> predicate, int startIndex = 0)
    {
        for (var i = startIndex; i < sources.Count; i++)
        {
            if (predicate(sources[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
