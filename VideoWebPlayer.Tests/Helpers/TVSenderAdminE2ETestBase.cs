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

    /// <summary>Applies a mutation to the persisted setup row — the shared pattern behind
    /// <see cref="EnableTvSenderAsync"/> and feature-specific setup seeds.</summary>
    protected async Task UpdateSetupAsync(Action<Setup> mutate)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var setup = await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(ct);
        mutate(setup);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Enables the TV-sender feature flag directly in the persisted setup row.</summary>
    protected Task EnableTvSenderAsync() => UpdateSetupAsync(s => s.TvSenderEnabled = true);

    /// <summary>
    /// Seeds a media collection on the shared E2E media source and releases the source for
    /// every non-admin user so its items are schedulable (the schedule only plans material
    /// released for the whole audience) — returns the persisted collection.
    /// </summary>
    /// <param name="name">Name and path prefix of the collection.</param>
    /// <param name="releaseToAudience">With <see langword="false"/> the grant loop is skipped
    /// and the source stays unreleased — for tests that need a missing audience grant
    /// (e.g. the content-scope check).</param>
    protected async Task<MediaCollection> SeedSharedCollectionAsync(string name, bool releaseToAudience = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var sourceId = await EnsureMediaSourceIdAsync(db);
        var collection = new MediaCollection
        {
            Name = $"{name}-{Guid.NewGuid():N}",
            MediaSourceId = sourceId,
            Path = $"/test/{name}-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        if (releaseToAudience)
        {
            foreach (var u in await db.Users.Where(u => !u.IsAdmin).ToListAsync(ct))
                if (!await db.MediaSourceUsers.AnyAsync(m => m.UserId == u.Id && m.MediaSourceId == sourceId, ct))
                    db.MediaSourceUsers.Add(new MediaSourceUser { UserId = u.Id, MediaSourceId = sourceId });
            await db.SaveChangesAsync(ct);
        }
        return collection;
    }

    /// <summary>
    /// Seeds a movie with a single probed media item on the shared E2E media source — the
    /// shape the schedule needs to plan a movie (a classified <see cref="Movie"/> with a
    /// <see cref="MovieMediaItem"/> link to an item with a probed <see cref="MediaItem.Duration"/>)
    /// and that the media search lists (it only shows classified movies) — returns the
    /// movie id.
    /// </summary>
    /// <param name="name">Movie name; the media item is named "<c>&lt;name&gt;.mkv</c>".</param>
    /// <param name="duration">Probed duration of the media item.</param>
    /// <param name="minAge">Normalized minimum age (<see cref="IAgeRated.AgeRatingMinAge"/>) —
    /// <see langword="null"/> leaves the rating unknown.</param>
    /// <param name="releaseToAudience">Forwarded to <see cref="SeedSharedCollectionAsync"/>.</param>
    protected async Task<long> SeedMovieWithItemAsync(string name, TimeSpan duration, int? minAge = null, bool releaseToAudience = true)
    {
        var collection = await SeedSharedCollectionAsync("movies", releaseToAudience);
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var movie = new Movie
        {
            Name = name,
            MediaSourceId = collection.MediaSourceId,
            CreatedAt = DateTime.UtcNow,
            AgeRatingMinAge = minAge
        };
        db.Movies.Add(movie);
        var item = new MediaItem
        {
            Name = $"{name}.mkv",
            Path = $"{collection.Path}/{name}.mkv",
            MediaCollectionId = collection.Id,
            Duration = duration,
            DurationProbedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
        await db.SaveChangesAsync(ct);
        return movie.Id;
    }

    /// <summary>
    /// Seeds an active daily <see cref="ProgramRuleType.MoviePrimeTime"/> rule that airs a
    /// single movie on the delivered channel (created on demand — the same channel the admin
    /// page materializes on first load) — returns the persisted rule id.
    /// </summary>
    /// <param name="name">Rule name.</param>
    /// <param name="hour">Local hour of the daily start.</param>
    /// <param name="minute">Local minute of the daily start.</param>
    /// <param name="movieId">Movie to air (<see cref="ProgramRule.MaterialId"/>).</param>
    /// <param name="priority">Rule priority — higher wins on overlapping occurrences.</param>
    /// <param name="slotDuration">Optional fixed slot duration; <see langword="null"/> lets the
    /// material length decide.</param>
    protected async Task<long> SeedMovieRuleAsync(string name, int hour, int minute, long movieId, int priority = 0, TimeSpan? slotDuration = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var channel = await new ProgramRuleService(db).GetOrCreateChannelAsync(ct);
        var rule = new ProgramRule
        {
            TVChannelId = channel.Id,
            Name = name,
            RuleType = ProgramRuleType.MoviePrimeTime,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(hour, minute),
            SlotDuration = slotDuration,
            MaterialType = ProgramRuleMaterialType.Movie,
            MaterialId = movieId,
            Priority = priority,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ProgramRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule.Id;
    }

    /// <summary>
    /// Seeds unclassified video media items (clip candidates) on the shared E2E media source in a
    /// fresh collection and releases the source for every non-admin user so the clips are
    /// schedulable — returns the media item ids in seed order.
    /// </summary>
    protected async Task<List<long>> SeedClipItemsAsync(params (string Name, TimeSpan? Duration)[] clips)
    {
        var collection = await SeedSharedCollectionAsync("clips");
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
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
