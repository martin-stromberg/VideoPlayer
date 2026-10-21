using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the TV channel administration page <c>/admin/tv-sender</c>: the
/// consolidated rule lifecycle (create via search dialog, edit, deactivate, delete) and the
/// admin-only access gate.
/// </summary>
public sealed class TVSenderAdminE2ETests : PlaylistsE2ETestBase
{
    private async Task OpenAdminPageAsAdminAsync()
    {
        await MakeUserAdminAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        await LoginAsync(UserAEmail);
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-name", new PageWaitForSelectorOptions { Timeout = 30_000 });
    }

    [Fact]
    public async Task Admin_CreateEditDeactivateDeleteRule()
    {
        if (SkipBrowser) return;

        var showId = await SeedTvShowWithSeasonsAsync("E2E Serie", ("Staffel 1", 2));
        await OpenAdminPageAsAdminAsync();

        // Channel settings: set schedule horizon + generation interval, save.
        await Page.FillAsync("#tvsender-horizon-hours", "72");
        await Page.FillAsync("#tvsender-interval-minutes", "30");
        await Page.ClickAsync("#tvsender-save-settings");
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("gespeichert");

        // Create a rule: open the dialog, fill fields, pick material via the search selector.
        await DriveUntilStateAsync(
            async () =>
            {
                if (await Page.Locator("#rule-name").CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#rule-name").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

        await Page.FillAsync("#rule-name", "Abendserie");
        await Page.FillAsync("#rule-episodes", "1");
        await Page.FillAsync("#rule-priority", "3");

        await DriveUntilStateAsync(
            async () =>
            {
                var sel = Page.Locator(".media-search-input");
                if (await sel.CountAsync() > 0) await sel.FillAsync("E2E Serie", new LocatorFillOptions { Timeout = 10_000 });
            },
            () => Page.Locator($".media-search-result[data-media-type='TVShow'][data-media-id='{showId}'], .media-search-error, .media-search-empty")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

        Assert.True(
            await Page.Locator($".media-search-result[data-media-id='{showId}']").CountAsync() > 0,
            "Suchergebnis fehlt. Dialogzustand: " + await Page.Locator("#rule-material-picker").InnerHTMLAsync());

        await Page.Locator($".media-search-result[data-media-type='TVShow'][data-media-id='{showId}']").ClickAsync();
        await Page.Locator("#rule-material-selected").WaitForAsync();
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Abendserie" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row).ToContainTextAsync("Serien-Sendezeit");
        await Expect(row).ToContainTextAsync("Serie: E2E Serie");

        // Edit: rename via the edit dialog.
        await row.Locator(".tvsender-rule-edit").ClickAsync();
        await Page.Locator("#rule-name").WaitForAsync();
        await Page.FillAsync("#rule-name", "Abendserie II");
        await Page.ClickAsync("#rule-save");
        await Expect(Page.Locator(".tvsender-rule-row")).ToContainTextAsync("Abendserie II");

        // Deactivate then reactivate.
        await Page.Locator(".tvsender-rule-toggle").ClickAsync();
        await Expect(Page.Locator(".tvsender-rule-row")).ToContainTextAsync("Inaktiv");

        // Delete with confirmation.
        await Page.Locator(".tvsender-rule-delete").ClickAsync();
        await Page.Locator("#rule-delete-confirm").WaitForAsync();
        await Page.ClickAsync("#rule-delete-confirm");
        await Expect(Page.Locator(".tvsender-rule-row")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#tvsender-rules-status")).ToContainTextAsync("gelöscht");
    }

    [Fact]
    public async Task NonAdmin_SeesNotAuthorized()
    {
        if (SkipBrowser) return;

        await LoginAsync(UserBEmail);
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        await Expect(Page.Locator("text=Nicht autorisiert")).ToBeVisibleAsync();
        Assert.Equal(0, await Page.Locator("#tvsender-new-rule").CountAsync());
    }
}
