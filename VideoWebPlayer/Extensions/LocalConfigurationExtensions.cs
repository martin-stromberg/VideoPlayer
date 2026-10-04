using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace VideoWebPlayer.Extensions;

/// <summary>
/// Registers the optional, never packaged local configuration file <c>appsettings.Local.json</c>.
/// </summary>
public static class LocalConfigurationExtensions
{
    /// <summary>
    /// File name of the optional local configuration file, resolved relative to the content root.
    /// </summary>
    private const string LocalSettingsFileName = "appsettings.Local.json";

    /// <summary>
    /// Adds the optional JSON file <c>appsettings.Local.json</c> as a configuration source
    /// (<c>optional</c>, <c>reloadOnChange</c>). The source is inserted directly after the last
    /// <see cref="JsonConfigurationSource"/> whose file name matches <c>appsettings*.json</c>, so its
    /// values override the packaged appsettings files but lose against user secrets, environment
    /// variables and the command line. If no matching source exists, the source is inserted before the
    /// first <see cref="EnvironmentVariablesConfigurationSource"/>; if none exists either, it is appended.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder instance.</returns>
    public static WebApplicationBuilder AddLocalJsonConfiguration(this WebApplicationBuilder builder)
    {
        var sources = builder.Configuration.Sources;

        var insertIndex = -1;
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is JsonConfigurationSource jsonSource && IsAppSettingsFileName(jsonSource.Path))
            {
                insertIndex = i + 1;
                break;
            }
        }

        if (insertIndex < 0)
        {
            var environmentIndex = -1;
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i] is EnvironmentVariablesConfigurationSource)
                {
                    environmentIndex = i;
                    break;
                }
            }

            insertIndex = environmentIndex >= 0 ? environmentIndex : sources.Count;
        }

        sources.Insert(insertIndex, new JsonConfigurationSource
        {
            Path = LocalSettingsFileName,
            Optional = true,
            ReloadOnChange = true,
            FileProvider = builder.Environment.ContentRootFileProvider
        });

        return builder;
    }

    private static bool IsAppSettingsFileName(string? path)
    {
        var fileName = Path.GetFileName(path);
        return fileName is not null
            && fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
}
