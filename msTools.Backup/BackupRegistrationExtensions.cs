using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace msTools.Backup;

/// <summary>
/// Registers backup services in ASP.NET Core applications.
/// </summary>
public static class BackupRegistrationExtensions
{
    /// <summary>
    /// Registers backup services using an options delegate.
    /// </summary>
    public static IServiceCollection AddBackups(this IServiceCollection services, Action<BackupOptions> configure)
    {
        services.Configure(configure);
        return AddBackupCore(services);
    }

    /// <summary>
    /// Registers backup services using a configuration section.
    /// </summary>
    public static IServiceCollection AddBackups(this IServiceCollection services, IConfigurationSection section)
    {
        services.Configure<BackupOptions>(section);
        return AddBackupCore(services);
    }

    /// <summary>
    /// Adds backup middleware or endpoints. The default implementation is intentionally host-neutral.
    /// </summary>
    public static IApplicationBuilder UseBackups(this IApplicationBuilder app) => app;

    private static IServiceCollection AddBackupCore(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IBackupOptionsProvider, OptionsBackupOptionsProvider>();
        services.TryAddScoped<IBackupStore, FileSystemBackupStore>();
        services.TryAddScoped<IBackupRetentionService, BackupRetentionService>();
        services.TryAddScoped<IBackupService, BackupService>();
        services.TryAddScoped<IAutomaticBackupRunner, DefaultAutomaticBackupRunner>();
        services.TryAddSingleton<IBackupRestoreGuard, NoopBackupRestoreGuard>();
        services.AddHostedService<ScheduledBackupService>();
        return services;
    }
}
