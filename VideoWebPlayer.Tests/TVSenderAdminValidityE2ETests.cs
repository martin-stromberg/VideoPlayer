using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the validity window ("Gültigkeitszeitraum") of program rules in the
/// TV channel administration <c>/admin/tv-sender</c>: the editor section with its conditional
/// inputs per mode, the meta label and the "Derzeit ungültig" chip in the rule list, the
/// end-sorting of currently invalid rules and the automatic purge via "Sendeplan jetzt erzeugen".
/// </summary>
public sealed class TVSenderAdminValidityE2ETests : TVSenderAdminE2ETestBase
{
    private async Task OpenRuleEditorAsync()
    {
        await DriveUntilStateAsync(
            async () =>
            {
                if (await Page.Locator("#rule-name").CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#rule-name").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));
    }

    private async Task PickMovieMaterialAsync(long movieId, string search)
    {
        await DriveUntilStateAsync(
            async () =>
            {
                var sel = Page.Locator(".media-search-input");
                if (await sel.CountAsync() > 0) await sel.FillAsync(search, new LocatorFillOptions { Timeout = 10_000 });
            },
            () => Page.Locator($".media-search-result[data-media-id='{movieId}'], .media-search-error, .media-search-empty")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));
        Assert.True(
            await Page.Locator($".media-search-result[data-media-id='{movieId}']").CountAsync() > 0,
            "Suchergebnis fehlt. Dialogzustand: " + await Page.Locator("#rule-material-picker").InnerHTMLAsync());
        await Page.Locator($".media-search-result[data-media-id='{movieId}']").ClickAsync();
        await Page.Locator("#rule-material-selected").WaitForAsync();
    }

    private async Task CreateMovieRuleViaDialogAsync(string ruleName, string movieName, long movieId)
    {
        await OpenRuleEditorAsync();
        await Page.FillAsync("#rule-name", ruleName);
        await Page.FillAsync("#rule-episodes", "1");
        await Page.SelectOptionAsync("#rule-type", "MoviePrimeTime");
        await PickMovieMaterialAsync(movieId, movieName);
    }

    private async Task SeedRuleWithValidityAsync(string name, long movieId, ProgramRuleValidityInput validity)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var channel = await new ProgramRuleService(db).GetOrCreateChannelAsync(ct);
        var mode = validity.Mode;
        db.ProgramRules.Add(new ProgramRule
        {
            TVChannelId = channel.Id,
            Name = name,
            RuleType = ProgramRuleType.MoviePrimeTime,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(20, 0),
            EpisodeCount = 1,
            MaterialType = ProgramRuleMaterialType.Movie,
            MaterialId = movieId,
            IsActive = true,
            ValidityMode = mode,
            ValidFrom = mode is ProgramRuleValidityMode.OneTime or ProgramRuleValidityMode.AnnualWindow ? validity.From : null,
            ValidTo = mode is ProgramRuleValidityMode.OneTime or ProgramRuleValidityMode.AnnualWindow ? validity.To : null,
            ValidFromDay = mode == ProgramRuleValidityMode.MonthlyWindow ? validity.FromDay : null,
            ValidToDay = mode == ProgramRuleValidityMode.MonthlyWindow ? validity.ToDay : null,
            ValidWeekdays = mode == ProgramRuleValidityMode.WeeklyWindow ? validity.Weekdays : null,
            ValidHolidayAnchor = mode == ProgramRuleValidityMode.HolidayWindow ? validity.HolidayAnchor : null,
            ValidHolidayBeforeDays = mode == ProgramRuleValidityMode.HolidayWindow ? validity.HolidayBeforeDays : null,
            ValidHolidayAfterDays = mode == ProgramRuleValidityMode.HolidayWindow ? validity.HolidayAfterDays : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Admin_OneTimeRule_CreateViaDialog_ShowsWindowLabel()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Zeitraumfilm", TimeSpan.FromMinutes(90));
        await OpenAdminPageAsAdminAsync();

        // Beidseitiger Zeitraum.
        await CreateMovieRuleViaDialogAsync("Zeitraum-Regel", "E2E Zeitraumfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "OneTime");
        await Page.Locator("#rule-valid-from").WaitForAsync();
        await Page.FillAsync("#rule-valid-from", "2026-12-01");
        await Page.FillAsync("#rule-valid-to", "2026-12-24");
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Zeitraum-Regel" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("01.12.2026");
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("24.12.2026");

        // Einseitig offenes Fenster (nur Von).
        await CreateMovieRuleViaDialogAsync("Offene-Regel", "E2E Zeitraumfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "OneTime");
        await Page.Locator("#rule-valid-from").WaitForAsync();
        await Page.FillAsync("#rule-valid-from", "2026-12-01");
        await Page.ClickAsync("#rule-save");

        var openRow = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Offene-Regel" });
        await openRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(openRow.Locator(".rule-panel-validity")).ToContainTextAsync("Gültig ab 01.12.2026");
    }

    [Fact]
    public async Task Admin_OneTimeDialog_WithoutBoundsOrReversedBounds_ShowsEditError()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Fehlerfilm", TimeSpan.FromMinutes(90));
        await OpenAdminPageAsAdminAsync();

        // Ohne jede Grenze.
        await CreateMovieRuleViaDialogAsync("Fehler-Regel", "E2E Fehlerfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "OneTime");
        await Page.Locator("#rule-valid-from").WaitForAsync();
        await Page.ClickAsync("#rule-save");
        await Expect(Page.Locator("#rule-edit-error")).ToContainTextAsync("mindestens eine Grenze");

        // Bis vor Von.
        await Page.FillAsync("#rule-valid-from", "2026-12-24");
        await Page.FillAsync("#rule-valid-to", "2026-12-01");
        await Page.ClickAsync("#rule-save");
        await Expect(Page.Locator("#rule-edit-error")).ToContainTextAsync("nicht vor dem Von-Datum");
    }

    [Fact]
    public async Task Admin_InvalidRecurringRule_SortsLastWithWarningChip()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Sortierfilm", TimeSpan.FromMinutes(90));

        // "A-Ungueltig": Jahresfenster nur am Monat-Tag des morgigen Tages — heute außerhalb.
        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
        var tomorrowMonthDay = new DateOnly(2000, tomorrow.Month, tomorrow.Day);
        await SeedRuleWithValidityAsync("A-Ungueltig", movieId,
            new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow,
                From: tomorrowMonthDay, To: tomorrowMonthDay));
        await SeedRuleWithValidityAsync("Z-Gueltig", movieId,
            new ProgramRuleValidityInput(ProgramRuleValidityMode.Unrestricted));

        await OpenAdminPageAsAdminAsync();

        var rows = Page.Locator(".tvsender-rule-row");
        await Expect(rows).ToHaveCountAsync(2, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
        // Gültige Regeln zuerst (nach Name), aktuell ungültige ans Ende.
        await Expect(rows.First).ToContainTextAsync("Z-Gueltig");
        await Expect(rows.Last).ToContainTextAsync("A-Ungueltig");
        await Expect(rows.Last).ToContainTextAsync("Derzeit ungültig");
        await Expect(rows.Last.Locator(".rule-panel-validity")).ToContainTextAsync("Jährlich");
        await Expect(rows.First).Not.ToContainTextAsync("Derzeit ungültig");
    }

    [Fact]
    public async Task Admin_ExpiredOneTimeRule_PurgedByGenerateNow()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Ablauffilm", TimeSpan.FromMinutes(90));
        await SeedRuleWithValidityAsync("Abgelaufene-Regel", movieId,
            new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2020, 1, 1), To: DateOnly.FromDateTime(DateTime.Now.AddDays(-1))));

        await OpenAdminPageAsAdminAsync();
        await Expect(Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Abgelaufene-Regel" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        await SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");
        await Page.ClickAsync("#tvsender-generate-now");

        // Der Worker-Lauf hinter dem Signal löscht die abgelaufene Regel (Purge vor Reconcile).
        var deadline = DateTime.UtcNow.AddSeconds(40);
        var gone = false;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            gone = !await db.ProgramRules.AnyAsync(r => r.Name == "Abgelaufene-Regel", TestContext.Current.CancellationToken);
            if (gone) break;
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        Assert.True(gone, "Die abgelaufene Regel wurde nicht automatisch gelöscht.");

        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-tab-regeln", new PageWaitForSelectorOptions { Timeout = 30_000 });
        await Expect(Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Abgelaufene-Regel" }))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Admin_WeeklyValidity_CreateViaDialog()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Wochenfilm", TimeSpan.FromMinutes(90));
        await OpenAdminPageAsAdminAsync();

        await CreateMovieRuleViaDialogAsync("Wochentags-Regel", "E2E Wochenfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "WeeklyWindow");
        await Page.Locator("#rule-valid-weekday-1").WaitForAsync();

        // Ohne Auswahl muss der Dialog den Fehler melden.
        await Page.ClickAsync("#rule-save");
        await Expect(Page.Locator("#rule-edit-error")).ToContainTextAsync("mindestens ein Wochentag");

        await Page.CheckAsync("#rule-valid-weekday-1"); // Montag
        await Page.CheckAsync("#rule-valid-weekday-3"); // Mittwoch
        await Page.CheckAsync("#rule-valid-weekday-5"); // Freitag
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Wochentags-Regel" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("Wöchentlich Mo, Mi, Fr");
    }

    [Fact]
    public async Task Admin_HolidayWindow_CreateViaDialog()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Feiertagsfilm", TimeSpan.FromMinutes(90));
        await OpenAdminPageAsAdminAsync();

        await CreateMovieRuleViaDialogAsync("Oster-Regel", "E2E Feiertagsfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "HolidayWindow");
        await Page.Locator("#rule-valid-holiday-anchor").WaitForAsync();
        await Page.SelectOptionAsync("#rule-valid-holiday-anchor", "EasterSunday");
        await Page.FillAsync("#rule-valid-holiday-before", "3");
        await Page.FillAsync("#rule-valid-holiday-after", "0");
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Oster-Regel" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("Ostersonntag");
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("Tage");
    }

    [Fact]
    public async Task Admin_AnnualWindow_CreateAndEditRoundtrip()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Jahresfilm", TimeSpan.FromMinutes(90));
        await OpenAdminPageAsAdminAsync();

        // Jahreswechsel-Fenster 20.12.–06.01. über Monats-Select + Tagesfeld je Grenze.
        await CreateMovieRuleViaDialogAsync("Jahres-Regel", "E2E Jahresfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "AnnualWindow");
        await Page.Locator("#rule-valid-from-month").WaitForAsync();
        await Page.SelectOptionAsync("#rule-valid-from-month", "12");
        await Page.FillAsync("#rule-valid-from-day", "20");
        await Page.SelectOptionAsync("#rule-valid-to-month", "1");
        await Page.FillAsync("#rule-valid-to-day", "6");
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Jahres-Regel" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("Jährlich 20.12.–06.01.");

        // Edit-Roundtrip: StartEdit verteilt die gespeicherten Monat-Tag-Werte auf die Felder.
        await row.Locator(".tvsender-rule-edit").ClickAsync();
        await Page.Locator("#rule-validity").WaitForAsync();
        Assert.Equal("AnnualWindow", await Page.Locator("#rule-validity").InputValueAsync());
        Assert.Equal("12", await Page.Locator("#rule-valid-from-month").InputValueAsync());
        Assert.Equal("20", await Page.Locator("#rule-valid-from-day").InputValueAsync());
        Assert.Equal("1", await Page.Locator("#rule-valid-to-month").InputValueAsync());
        Assert.Equal("6", await Page.Locator("#rule-valid-to-day").InputValueAsync());
        await Page.ClickAsync("text=Abbrechen");
    }

    [Fact]
    public async Task Admin_MonthlyWindow_CreateAndEditRoundtrip()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var movieId = await SeedMovieWithItemAsync("E2E Monatsfilm", TimeSpan.FromMinutes(90));
        await OpenAdminPageAsAdminAsync();

        // Wrap über den Monatswechsel: 25.–5.
        await CreateMovieRuleViaDialogAsync("Monats-Regel", "E2E Monatsfilm", movieId);
        await Page.SelectOptionAsync("#rule-validity", "MonthlyWindow");
        await Page.Locator("#rule-valid-monthly-from").WaitForAsync();
        await Page.FillAsync("#rule-valid-monthly-from", "25");
        await Page.FillAsync("#rule-valid-monthly-to", "5");
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Monats-Regel" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row.Locator(".rule-panel-validity")).ToContainTextAsync("Monatlich 25.–5.");

        await row.Locator(".tvsender-rule-edit").ClickAsync();
        await Page.Locator("#rule-validity").WaitForAsync();
        Assert.Equal("MonthlyWindow", await Page.Locator("#rule-validity").InputValueAsync());
        Assert.Equal("25", await Page.Locator("#rule-valid-monthly-from").InputValueAsync());
        Assert.Equal("5", await Page.Locator("#rule-valid-monthly-to").InputValueAsync());
        await Page.ClickAsync("text=Abbrechen");
    }
}
