using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Builds a small service provider around an existing SQLite test database with everything the playlist
/// backfill coordinator and worker need, for tests of <see cref="PlaylistBackfillCoordinator"/> and
/// <see cref="PlaylistBackfillWorker"/>.
/// </summary>
internal static class PlaylistBackfillTestServices
{
    /// <summary>
    /// Builds the service provider.
    /// </summary>
    /// <param name="connectionString">The shared-memory SQLite connection string of the test database (kept open by the test).</param>
    /// <param name="settings">The playlist settings.</param>
    /// <param name="signal">The signal wired into every database context (as in production), or <c>null</c> for none.</param>
    /// <param name="gate">The background processing gate the coordinator enters per work unit, or <c>null</c> for none.</param>
    /// <param name="interceptors">Interceptors added to every database context.</param>
    /// <returns>The built provider (dispose it at the end of the test).</returns>
    public static ServiceProvider Build(string connectionString, PlaylistSettings settings, IPlaylistBackfillSignal? signal, IBackgroundProcessingGate? gate = null, params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        if (signal is not null)
            services.AddSingleton(signal);
        if (gate is not null)
            services.AddSingleton(gate);
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString).AddInterceptors(interceptors));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings));
        services.AddScoped<PlaylistBackfillService>();
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        return services.BuildServiceProvider();
    }
}
