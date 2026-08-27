using Microsoft.Extensions.DependencyInjection.Extensions;
using msTools.Updater;
using VideoWebPlayer.Services.Updates;

namespace VideoWebPlayer.Extensions;

/// <summary>
/// Registers the automatic program update subsystem (msTools.Updater) and the backup that is created before an
/// update is installed.
/// </summary>
public static class AutoUpdateExtensions
{
    /// <summary>
    /// The systemd unit name used on Linux to run the installation script.
    /// </summary>
    private const string UpdateUnitName = "VideoWebPlayer-AutoUpdate";

    /// <summary>
    /// Registers the auto-update subsystem. All values except the update source are taken from the
    /// <c>AutoUpdate</c> configuration section; the backup is configured in <c>AutoUpdate:Backup</c>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder instance.</returns>
    public static WebApplicationBuilder AddVideoWebPlayerAutoUpdate(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<UpdateBackupOptions>(builder.Configuration.GetSection(UpdateBackupOptions.SectionName));
        builder.Services.AddSingleton<UpdateBackupCoordinator>();
        builder.Services.AddHostedService<UpdateSettingsInitializer>();

        builder.UseAutoUpdate(cfg =>
        {
            var allowPrereleaseUpdates = builder.Configuration.GetValue($"{AutoUpdateHostBuilderExtensions.DefaultConfigurationSectionName}:{nameof(AutoUpdateOptions.AllowPrereleaseUpdates)}", false);
            var sourceFactory = new VideoWebPlayerUpdateSourceFactory(builder.Configuration);
            cfg.UseSource(sourceFactory.Create(allowPrereleaseUpdates))
               .WithUpdateUnitName(UpdateUnitName);
        });

        // Replace the default process runner to avoid a workspace recovery that deletes
        // the downloaded package after the install script has been generated.
        builder.Services.Replace(ServiceDescriptor.Singleton<IAutoUpdateProcessRunner, SafeAutoUpdateProcessRunner>());

        builder.Services.AddHostedService<UpdateBackupEventBinder>();

        return builder;
    }
}
