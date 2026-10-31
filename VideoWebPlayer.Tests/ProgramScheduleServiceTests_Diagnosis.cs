using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Integration tests for <see cref="IProgramScheduleService.GetRuleDiagnosesAsync"/>: the
/// per-rule planning diagnosis counts schedulable content and reports why nothing is
/// schedulable (missing material, no media file, probe pending/exhausted, not released).
/// </summary>
public sealed class ProgramScheduleServiceTests_Diagnosis
{
    private static Task<ApplicationUser> AddAdminAsync(ProgramScheduleTestDb fixture)
        => fixture.AddUserAsync("admin@test.com", isAdmin: true);

    private static async Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>> DiagnoseAsync(
        ProgramScheduleTestDb fixture, ApplicationUser admin, long channelId, CancellationToken ct)
        => await fixture.CreateService().GetRuleDiagnosesAsync(admin.Id, channelId, ct);

    private static async Task<MediaItem> LinkedItemAsync(ProgramScheduleTestDb fixture, long movieId, CancellationToken ct)
        => await fixture.Db.MovieMediaItems
            .Where(l => l.MovieId == movieId)
            .Select(l => l.MediaItem)
            .SingleAsync(ct);

    [Fact]
    public async Task Diagnosis_SchedulableMovie_CountsSchedulable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-schedulable", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var diagnoses = await DiagnoseAsync(fixture, admin, channel.Id, ct);
        var d = diagnoses[rule.Id];

        Assert.False(d.MaterialMissing);
        Assert.Equal(1, d.CandidateCount);
        Assert.Equal(1, d.SchedulableCount);
        Assert.Equal(0, d.NoMediaItemCount);
        Assert.Equal(0, d.ProbePendingCount);
        Assert.Equal(0, d.ProbeExhaustedCount);
        Assert.Equal(0, d.NoAccessCount);
    }

    [Fact]
    public async Task Diagnosis_MovieWithoutMediaItem_CountsNoMediaItem()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-nomedia", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        await fixture.EnsureSourceAsync(1);
        var movie = new Movie { Name = "Ohne Datei", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.Movies.Add(movie);
        await fixture.SaveDiagAsync();
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var d = (await DiagnoseAsync(fixture, admin, channel.Id, ct))[rule.Id];

        Assert.False(d.MaterialMissing);
        Assert.Equal(1, d.CandidateCount);
        Assert.Equal(0, d.SchedulableCount);
        Assert.Equal(1, d.NoMediaItemCount);
        Assert.Equal(0, d.ProbePendingCount);
        Assert.Equal(0, d.ProbeExhaustedCount);
    }

    [Fact]
    public async Task Diagnosis_UnprobedItem_CountsProbePending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-unprobed", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var movie = await fixture.CreateMovieAsync("Ungeprobt", duration: null);
        var item = await LinkedItemAsync(fixture, movie.Id, ct);
        item.DurationProbedAt = null; // never probed
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var d = (await DiagnoseAsync(fixture, admin, channel.Id, ct))[rule.Id];

        Assert.Equal(0, d.SchedulableCount);
        Assert.Equal(1, d.ProbePendingCount);
        Assert.Equal(0, d.ProbeExhaustedCount);
        Assert.Equal(0, d.NoMediaItemCount);
    }

    [Fact]
    public async Task Diagnosis_FailedProbeBelowMax_CountsProbePending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-retry", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var movie = await fixture.CreateMovieAsync("Retry", duration: null);
        var item = await LinkedItemAsync(fixture, movie.Id, ct);
        item.DurationProbeAttempts = 2; // below the default maximum of 10 → retry pending
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var d = (await DiagnoseAsync(fixture, admin, channel.Id, ct))[rule.Id];

        Assert.Equal(1, d.ProbePendingCount);
        Assert.Equal(0, d.ProbeExhaustedCount);
    }

    [Fact]
    public async Task Diagnosis_ExhaustedProbe_CountsProbeExhausted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-exhausted", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var movie = await fixture.CreateMovieAsync("Gescheitert", duration: null);
        var item = await LinkedItemAsync(fixture, movie.Id, ct);
        item.DurationProbeAttempts = new Configuration.MediaProbeOptions().EffectiveMaxAttempts;
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var d = (await DiagnoseAsync(fixture, admin, channel.Id, ct))[rule.Id];

        Assert.Equal(0, d.SchedulableCount);
        Assert.Equal(1, d.ProbeExhaustedCount);
        Assert.Equal(0, d.ProbePendingCount);
    }

    [Fact]
    public async Task Diagnosis_UnsharedContent_CountsNoAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-access", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        // One non-admin viewer: source 1 is fully shared, source 2 is not.
        var viewer = await fixture.AddUserAsync("viewer@test.com");
        await fixture.GrantSourceAccessAsync(viewer.Id, 1);
        var released = await fixture.CreateMovieAsync("Frei", sourceId: 1, duration: TimeSpan.FromMinutes(30));
        var blocked = await fixture.CreateMovieAsync("Gesperrt", sourceId: 2, duration: TimeSpan.FromMinutes(90));

        var releasedRule = await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, released.Id, episodeCount: 1);
        var blockedRule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, blocked.Id);

        var diagnoses = await DiagnoseAsync(fixture, admin, channel.Id, ct);

        Assert.Equal(1, diagnoses[blockedRule.Id].NoAccessCount);
        Assert.Equal(0, diagnoses[blockedRule.Id].SchedulableCount);
        Assert.Equal(1, diagnoses[releasedRule.Id].SchedulableCount);
        Assert.Equal(0, diagnoses[releasedRule.Id].NoAccessCount);
    }

    [Fact]
    public async Task Diagnosis_DeletedMaterial_MarksMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-orphan", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Verwaist", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, 9999, episodeCount: 1);

        var d = (await DiagnoseAsync(fixture, admin, channel.Id, ct))[rule.Id];

        Assert.True(d.MaterialMissing);
        Assert.Equal(0, d.SchedulableCount);
        Assert.Equal(0, d.CandidateCount);
    }

    [Fact]
    public async Task Diagnosis_GenreResolvingToNothing_MarksMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-genre", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var hidden = await fixture.CreateGenreAsync("Versteckt", sourceId: 1, isHidden: true);
        var missing = await fixture.CreateRuleAsync(channel.Id, "Versteckte Rotation", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, hidden.Id,
            slotDuration: TimeSpan.FromMinutes(60), genreIds: [hidden.Id]);
        // Legacy row without ProgramRuleGenre links: MaterialId alone no longer resolves.
        var deleted = await fixture.CreateRuleAsync(channel.Id, "Gelöschte Rotation", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(21, 0), ProgramRuleMaterialType.Genre, 9999,
            slotDuration: TimeSpan.FromMinutes(60));

        var diagnoses = await DiagnoseAsync(fixture, admin, channel.Id, ct);

        // Hidden genre without usable alternate names resolves to no canonical names.
        Assert.True(diagnoses[missing.Id].MaterialMissing);
        // Stored genre row does not exist at all.
        Assert.True(diagnoses[deleted.Id].MaterialMissing);
    }

    [Fact]
    public async Task Diagnosis_InactiveRule_StillDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-inactive", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await AddAdminAsync(fixture);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Pausiert", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            isActive: false);

        var diagnoses = await DiagnoseAsync(fixture, admin, channel.Id, ct);

        Assert.True(diagnoses.ContainsKey(rule.Id));
        Assert.Equal(1, diagnoses[rule.Id].SchedulableCount);
    }

    [Fact]
    public async Task Diagnosis_NonAdmin_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("diag-auth", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var viewer = await fixture.AddUserAsync("viewer@test.com");
        var service = fixture.CreateService();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.GetRuleDiagnosesAsync(viewer.Id, channel.Id, ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetRuleDiagnosesAsync("no-such-user", channel.Id, ct));
    }
}
