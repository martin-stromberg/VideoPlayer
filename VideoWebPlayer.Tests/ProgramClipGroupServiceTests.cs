using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Service-level rules of <see cref="ProgramClipGroupService"/>: admin enforcement, reserved
/// "Trailer" name on both write paths, system-group protection, candidate filtering and CRUD.
/// </summary>
public sealed class ProgramClipGroupServiceTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static async Task<(ProgramScheduleTestDb F, ProgramClipGroupService Svc, string Admin, string User)> SetupAsync(string name, CancellationToken ct)
    {
        var f = await ProgramScheduleTestDb.CreateAsync(name, ct);
        var admin = await f.AddUserAsync("admin@t.t", isAdmin: true);
        var user = await f.AddUserAsync("user@t.t", isAdmin: false);
        return (f, new ProgramClipGroupService(f.Db), admin.Id, user.Id);
    }

    [Fact]
    public async Task Crud_Roundtrip()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-crud", ct);
        using var _ = f;
        var g = await svc.CreateGroupAsync(admin, "Maskottchen");
        Assert.Equal("Maskottchen", g.Name);
        Assert.False(g.IsSystem);
        var renamed = await svc.RenameGroupAsync(admin, g.Id, "Werbung");
        Assert.Equal("Werbung", renamed.Name);
        var groups = await svc.GetGroupsAsync(admin);
        Assert.Contains(groups, x => x.Id == g.Id);
        Assert.Contains(groups, x => x.Name == "Trailer" && x.IsSystem);
        await svc.DeleteGroupAsync(admin, g.Id);
        Assert.DoesNotContain(await svc.GetGroupsAsync(admin), x => x.Id == g.Id);
    }

    [Fact]
    public async Task ReservedName_RejectedOnCreateAndRename()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-reserved", ct);
        using var _ = f;
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateGroupAsync(admin, "Trailer"));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateGroupAsync(admin, " trailer "));
        var g = await svc.CreateGroupAsync(admin, "Eigene");
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RenameGroupAsync(admin, g.Id, "trailer"));
    }

    [Fact]
    public async Task SystemGroup_NotModifiable()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-system", ct);
        using var _ = f;
        var item = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RenameGroupAsync(admin, ProgramClipGroup.TrailerGroupId, "X"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteGroupAsync(admin, ProgramClipGroup.TrailerGroupId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddClipsToGroupAsync(admin, ProgramClipGroup.TrailerGroupId, [item.Id]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RemoveClipFromGroupAsync(admin, ProgramClipGroup.TrailerGroupId, item.Id));
    }

    [Fact]
    public async Task NonAdmin_IsRejected()
    {
        var ct = CT;
        var (f, svc, _, user) = await SetupAsync("cgs-noadmin", ct);
        using var _ = f;
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CreateGroupAsync(user, "X"));
        await Assert.ThrowsAnyAsync<Exception>(() => svc.GetGroupsAsync(user));
    }

    [Fact]
    public async Task Candidates_OnlyUnclassifiedVideos()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-candidates", ct);
        using var _ = f;
        var clip = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        var noDur = await f.CreateMediaItemAsync(1, null);
        var movie = await f.CreateMovieAsync("Film", 1, TimeSpan.FromMinutes(90));
        var movieItem = await f.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).FirstAsync(ct);
        var (_, _, episodes) = await f.CreateShowAsync("Serie", 1, 1);
        var episodeItem = await f.Db.TVShowEpisodeMediaItems
            .Where(l => l.TVShowEpisodeId == episodes[0].Id).Select(l => l.MediaItemId).FirstAsync(ct);

        var candidates = await svc.GetClipCandidatesAsync(admin, null, null, 0, 200);

        Assert.Contains(candidates, c => c.MediaItemId == clip.Id);
        Assert.Contains(candidates, c => c.MediaItemId == noDur.Id);
        Assert.DoesNotContain(candidates, c => c.MediaItemId == movieItem);
        Assert.DoesNotContain(candidates, c => c.MediaItemId == episodeItem);
    }

    [Fact]
    public async Task AddClips_UnknownItem_Rejected()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-addmissing", ct);
        using var _ = f;
        var g = await svc.CreateGroupAsync(admin, "G");
        var item = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));

        // Unbekannte Ids — einschließlich der Id 0, die ein Default-Sentinel früher als
        // „vorhanden" durchwinkte und erst beim Speichern als FK-Verletzung scheiterte —
        // werden als sauberer Fachfehler abgelehnt, bevor irgendetwas geschrieben wird.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.AddClipsToGroupAsync(admin, g.Id, [0]));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.AddClipsToGroupAsync(admin, g.Id, [item.Id, item.Id + 1000]));
        Assert.Empty(await svc.GetGroupClipsAsync(admin, g.Id));
    }

    [Fact]
    public async Task AddClips_ClassifiedItem_Rejected()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-addclassified", ct);
        using var _ = f;
        var g = await svc.CreateGroupAsync(admin, "G");
        var movie = await f.CreateMovieAsync("Film", 1, TimeSpan.FromMinutes(90));
        var movieItem = await f.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).FirstAsync(ct);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.AddClipsToGroupAsync(admin, g.Id, [movieItem]));
    }

    [Fact]
    public async Task AddAndRemoveClips_Workflow()
    {
        var ct = CT;
        var (f, svc, admin, _) = await SetupAsync("cgs-members", ct);
        using var _ = f;
        var g = await svc.CreateGroupAsync(admin, "G");
        var item = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        await svc.AddClipsToGroupAsync(admin, g.Id, [item.Id]);
        var members = await svc.GetGroupClipsAsync(admin, g.Id);
        Assert.Single(members);
        Assert.Equal(item.Id, members[0].MediaItemId);
        await svc.RemoveClipFromGroupAsync(admin, g.Id, item.Id);
        Assert.Empty(await svc.GetGroupClipsAsync(admin, g.Id));
    }

    [Fact]
    public async Task MissingTrailerSeed_Tolerated()
    {
        var ct = CT;
        var (f, svc, admin, user) = await SetupAsync("cgs-notrailer", ct);
        using var _ = f;
        // Der Nicht-Admin braucht Quell-Freigabe, sonst liefert die Planung ohnehin keine Slots.
        await f.GrantSourceAccessAsync(user);
        f.Db.ProgramClipGroups.RemoveRange(f.Db.ProgramClipGroups);
        await f.Db.SaveChangesAsync(ct);
        var groups = await svc.GetGroupsAsync(admin);
        Assert.Empty(groups);
        var g = await svc.CreateGroupAsync(admin, "Eigene");
        Assert.NotNull(g);

        // CreateRuleAsync mit der (fehlenden) Trailer-Gruppe schlägt als unbekannte Gruppe fehl.
        var channel = await f.GetChannelAsync(ct);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var ruleSvc = new ProgramRuleService(f.Db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ruleSvc.CreateRuleAsync(admin, channel.Id,
            new ProgramRuleInput("R", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily, null, null,
                new TimeOnly(20, 0), 1, TimeSpan.FromHours(4), ProgramRuleMaterialType.Movie, movie.Id, 0, true,
                null, null, [ProgramClipGroup.TrailerGroupId], null), ct));

        // Alt-Backup-Toleranz: eine Link-Zeile ohne Gruppen-Zeile (nur per deaktivierten
        // FK-Prüfungen erzeugbar, wie beim Restore) erzeugt in BuildClipGroupStates keinen
        // Gruppen-Zustand — die Planung läuft fehlerfrei weiter.
        var rule = await f.CreateRuleAsync(channel.Id, "R2", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(4));
        await f.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await f.Db.Database.ExecuteSqlRawAsync(
            "INSERT INTO ProgramRuleClipGroups (ProgramRuleId, ProgramClipGroupId) VALUES ({0}, {1})",
            rule.Id, ProgramClipGroup.TrailerGroupId);
        await f.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        var summary = await f.CreateService().EnsureCoverageAsync(DateTimeOffset.UtcNow, ct);
        Assert.True(summary.SlotsCreated > 0);
    }
}
