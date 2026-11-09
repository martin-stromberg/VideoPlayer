using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Setup-Mutationen gegen den Test-Host, die beide Test-Infrastruktur-Linien teilen:
/// die Playwright-E2E-Basis (<see cref="PlaylistsE2ETestBase"/>) und die Geräte-Client-
/// Basis (<see cref="DeviceClientTestBase"/>) sprechen denselben
/// <see cref="ProgramSettingsService"/>-Setup-Satz an, ohne eine gemeinsame Basisklasse
/// zu haben.
/// </summary>
public static class SetupSeedHelpers
{
    /// <summary>
    /// Applies a mutation to the persisted setup row of the test host: opens a scope,
    /// materializes the row via <see cref="ProgramSettingsService.GetOrCreateSetupAsync"/>,
    /// applies <paramref name="mutate"/> and saves.
    /// </summary>
    /// <param name="services">The service provider of the test host.</param>
    /// <param name="mutate">The mutation to apply to the setup row before saving.</param>
    public static async Task UpdateSetupAsync(IServiceProvider services, Action<Setup> mutate)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var setup = await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(ct);
        mutate(setup);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sets the on-air switch of the TV channel (Issue #348) directly in the persisted setup row.</summary>
    /// <param name="services">The service provider of the test host.</param>
    /// <param name="onAir">The value to persist for <see cref="Setup.TvSenderOnAir"/>.</param>
    public static Task SetOnAirAsync(IServiceProvider services, bool onAir)
        => UpdateSetupAsync(services, s => s.TvSenderOnAir = onAir);
}
