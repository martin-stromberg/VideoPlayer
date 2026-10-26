using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared base for the Playwright E2E tests of the TV channel administration
/// (<c>/admin/tv-sender</c>): logging in as administrator with media-source access, enabling
/// the TV-sender feature flag and the retry-driven tab switch that survives the Blazor
/// circuit hand-over.
/// </summary>
public abstract class TVSenderAdminE2ETestBase : PlaylistsE2ETestBase
{
    /// <summary>
    /// Promotes <see cref="PlaylistsE2ETestBase.UserAEmail"/> to administrator, grants access
    /// to the shared media source (the media search filters strictly over
    /// <see cref="MediaSourceUser"/> grants — there is no admin bypass; the grant is
    /// idempotent because seed helpers may already have granted it), logs in and opens the
    /// admin page.
    /// </summary>
    /// <param name="readyTabSelector">Selector of a tab button whose presence proves the page
    /// rendered — each test class waits for the tab it actually uses.</param>
    protected async Task OpenAdminPageAsAdminAsync(string readyTabSelector = "#tvsender-tab-regeln")
    {
        await MakeUserAdminAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        await LoginAsync(UserAEmail);
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        // The tab bar is visible on every pane state; field selectors live inside tab panes
        // that start out hidden until their tab is clicked (see SwitchTabAsync below).
        await Page.WaitForSelectorAsync(readyTabSelector, new PageWaitForSelectorOptions { Timeout = 30_000 });
    }

    /// <summary>Enables the TV-sender feature flag directly in the persisted setup row.</summary>
    protected async Task EnableTvSenderAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var setup = await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(ct);
        setup.TvSenderEnabled = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds unclassified video media items (clip candidates) on the shared E2E media source in a
    /// fresh collection and releases the source for every non-admin user so the clips are
    /// schedulable — returns the media item ids in seed order.
    /// </summary>
    protected async Task<List<long>> SeedClipItemsAsync(params (string Name, TimeSpan? Duration)[] clips)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var sourceId = await EnsureMediaSourceIdAsync(db);
        var collection = new MediaCollection
        {
            Name = $"clips-{Guid.NewGuid():N}",
            MediaSourceId = sourceId,
            Path = $"/test/clips-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        foreach (var u in await db.Users.Where(u => !u.IsAdmin).ToListAsync(ct))
            if (!await db.MediaSourceUsers.AnyAsync(m => m.UserId == u.Id && m.MediaSourceId == sourceId, ct))
                db.MediaSourceUsers.Add(new MediaSourceUser { UserId = u.Id, MediaSourceId = sourceId });
        await db.SaveChangesAsync(ct);
        var items = clips.Select(c => new MediaItem
        {
            Name = c.Name,
            Path = $"{collection.Path}/{c.Name}",
            MediaCollectionId = collection.Id,
            Duration = c.Duration,
            DurationProbedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }).ToList();
        db.MediaItems.AddRange(items);
        await db.SaveChangesAsync(ct);
        return items.Select(i => i.Id).ToList();
    }

    // The first interactive action on the page can be lost while the Blazor circuit is still
    // connecting — drive the tab click until the pane is really active; waiting on DOM
    // presence alone proved flaky.
    protected async Task SwitchTabAsync(string tabButtonSelector, string paneSelector)
    {
        await DriveUntilStateAsync(
            async () =>
            {
                var pane = Page.Locator($"{paneSelector}.active");
                if (await pane.CountAsync() > 0) return;
                var tab = Page.Locator(tabButtonSelector);
                if (await tab.CountAsync() > 0) await tab.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator($"{paneSelector}.active").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));
    }
}
