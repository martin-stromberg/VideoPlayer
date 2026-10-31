using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Genre material in <see cref="ProgramRuleService"/>: multi-genre validation, join-row CRUD and
/// the sender-specific genre option source (hidden/alternate-name/season visibility, source
/// differentiation, admin gate).
/// </summary>
public sealed class ProgramRuleServiceTests_GenreMaterial
{
    private static async Task<(ProgramRuleService Service, string AdminId, string UserId, long ChannelId)> SetupAsync(
        PairingTestDb fixture, CancellationToken ct)
    {
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var user = await fixture.CreateUserAsync("user@test.com", isAdmin: false);
        var service = new ProgramRuleService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        return (service, admin.Id, user.Id, channel.Id);
    }

    // Repeat accepts every material type, so tests can also switch a rule away from genre.
    private static ProgramRuleInput GenreInput(IReadOnlyList<long>? genreIds, IReadOnlyList<long>? sourceIds = null)
        => new("Genre-Regel", ProgramRuleType.Repeat, ProgramRuleRecurrence.Daily, null, null,
            new TimeOnly(20, 0), 1, null, ProgramRuleMaterialType.Genre, genreIds?.FirstOrDefault() ?? 0, 0, true,
            GenreIds: genreIds, MediaSourceIds: sourceIds);

    private static async Task<Genre> AddGenreAsync(PairingTestDb fixture, string name, CancellationToken ct, long sourceId = 1,
        bool isHidden = false, IEnumerable<string>? alternates = null, DateTime? start = null, DateTime? end = null)
    {
        if (!await fixture.Db.MediaSources.AnyAsync(s => s.Id == sourceId, ct))
        {
            fixture.Db.MediaSources.Add(new MediaSource { Id = sourceId, Name = $"Quelle {sourceId}", Path = $"/s{sourceId}", Host = "h", Port = 22 });
            await fixture.Db.SaveChangesAsync(ct);
        }
        var genre = new Genre { Name = name, MediaSourceId = sourceId, IsHidden = isHidden, StartDate = start, EndDate = end };
        fixture.Db.Genres.Add(genre);
        await fixture.Db.SaveChangesAsync(ct);
        if (alternates is not null)
        {
            foreach (var a in alternates)
                fixture.Db.GenreNames.Add(new GenreName { Name = a, GenreId = genre.Id });
            await fixture.Db.SaveChangesAsync(ct);
        }
        return genre;
    }

    [Fact]
    public async Task CreateRule_GenreWithoutGenreIds_RotatesAllGenres()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-genre-empty", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);

        // „Keine Auswahl = alle Genres": Genre-Material ohne Auswahl ist zulässig und wird mit
        // MaterialId 0 ohne Join-Rows gespeichert — der Marker verhindert, dass der Planer in
        // den Einzel-Genre-Fallback für Altbestände läuft.
        var rule = await service.CreateRuleAsync(adminId, channelId, GenreInput(null), ct);

        Assert.Equal(ProgramRuleMaterialType.Genre, rule.MaterialType);
        Assert.Equal(0, rule.MaterialId);
        Assert.Equal(0, await fixture.Db.ProgramRuleGenres.CountAsync(x => x.ProgramRuleId == rule.Id, ct));
    }

    [Fact]
    public async Task CreateRule_GenreRotationWithoutGenreIds_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-genrerotation-empty", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);

        // Der aus Issue #307 gemeldete Fall: Genre-/Formatrotation ohne explizite
        // Genre-Auswahl speichern — die Regel rotiert anschließend über alle Genres.
        var rule = await service.CreateRuleAsync(adminId, channelId,
            GenreInput(null) with { RuleType = ProgramRuleType.GenreRotation }, ct);

        Assert.Equal(ProgramRuleType.GenreRotation, rule.RuleType);
        Assert.Equal(ProgramRuleMaterialType.Genre, rule.MaterialType);
        Assert.Equal(0, rule.MaterialId);
    }

    [Fact]
    public async Task UpdateRule_GenreSelectionCleared_RotatesAllGenres()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-genre-cleared", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);
        var g1 = await AddGenreAsync(fixture, "Action", ct);
        var g2 = await AddGenreAsync(fixture, "Komödie", ct);

        var rule = await service.CreateRuleAsync(adminId, channelId, GenreInput([g1.Id, g2.Id]), ct);

        // Entfernt der Benutzer alle Häkchen, bleibt die Regel gültig — sie rotiert danach
        // über alle Genres und bleibt nicht auf der letzten Auswahl hängen.
        await service.UpdateRuleAsync(adminId, rule.Id, GenreInput(null), ct);

        var stored = await fixture.Db.ProgramRules.AsNoTracking().SingleAsync(r => r.Id == rule.Id, ct);
        Assert.Equal(0, stored.MaterialId);
        Assert.Equal(0, await fixture.Db.ProgramRuleGenres.CountAsync(x => x.ProgramRuleId == rule.Id, ct));
    }

    [Fact]
    public async Task CreateRule_GenreIdsOnNonGenreMaterial_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-genre-wrongtype", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);
        var genre = await AddGenreAsync(fixture, "Action", ct);
        var show = new TVShow { Name = "S", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        var input = new ProgramRuleInput("R", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily, null, null,
            new TimeOnly(20, 0), 1, null, ProgramRuleMaterialType.TVShow, show.Id, 0, true, GenreIds: [genre.Id]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_UnknownGenreId_ThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-genre-unknown", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateRuleAsync(adminId, channelId, GenreInput([999L]), ct));
    }

    [Fact]
    public async Task CreateRule_UnknownMediaSourceId_ThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-source-unknown", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);
        var genre = await AddGenreAsync(fixture, "Action", ct);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateRuleAsync(adminId, channelId, GenreInput([genre.Id], [999L]), ct));
    }

    [Fact]
    public async Task CreateAndUpdateRule_GenreLinksRoundTrippedAndReplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-genre-roundtrip", ct);
        var (service, adminId, _, channelId) = await SetupAsync(fixture, ct);
        var g1 = await AddGenreAsync(fixture, "Action", ct);
        var g2 = await AddGenreAsync(fixture, "Komödie", ct);
        await fixture.Db.MediaSources.AddAsync(new MediaSource { Id = 7, Name = "S7", Path = "/7", Host = "h", Port = 22 }, ct);
        await fixture.Db.SaveChangesAsync(ct);

        var rule = await service.CreateRuleAsync(adminId, channelId, GenreInput([g1.Id, g2.Id], [7L]), ct);
        Assert.Equal(g1.Id, rule.MaterialId); // first genre stays in the legacy column
        Assert.Equal(2, await fixture.Db.ProgramRuleGenres.CountAsync(x => x.ProgramRuleId == rule.Id, ct));
        Assert.Single(await fixture.Db.ProgramRuleMediaSources.Where(x => x.ProgramRuleId == rule.Id).ToListAsync(ct));

        // Update replaces the whole link sets (down to one genre, no source filter).
        await service.UpdateRuleAsync(adminId, rule.Id, GenreInput([g2.Id]), ct);
        var links = await fixture.Db.ProgramRuleGenres.Where(x => x.ProgramRuleId == rule.Id).ToListAsync(ct);
        Assert.Single(links);
        Assert.Equal(g2.Id, links[0].GenreId);
        Assert.Equal(0, await fixture.Db.ProgramRuleMediaSources.CountAsync(x => x.ProgramRuleId == rule.Id, ct));

        // Switching to non-genre material clears the links.
        var movie = new Movie { Name = "M", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.Movies.Add(movie);
        await fixture.Db.SaveChangesAsync(ct);
        var nonGenre = new ProgramRuleInput("R", ProgramRuleType.Repeat, ProgramRuleRecurrence.Daily, null, null,
            new TimeOnly(20, 0), 1, null, ProgramRuleMaterialType.Movie, movie.Id, 0, true);
        await service.UpdateRuleAsync(adminId, rule.Id, nonGenre, ct);
        Assert.Equal(0, await fixture.Db.ProgramRuleGenres.CountAsync(x => x.ProgramRuleId == rule.Id, ct));
    }

    [Fact]
    public async Task GetGenreMaterialOptions_HiddenWithAlternate_OnlyCanonicalListed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-options-alt", ct);
        var (service, adminId, _, _) = await SetupAsync(fixture, ct);
        await AddGenreAsync(fixture, "Familie", ct);
        await AddGenreAsync(fixture, "Family", ct, isHidden: true, alternates: ["Familie"]);

        var options = await service.GetGenreMaterialOptionsAsync(adminId, null, ct);

        Assert.Equal(["Familie"], options.Select(o => o.Name).ToArray());
    }

    [Fact]
    public async Task GetGenreMaterialOptions_HiddenWithoutAlternate_NotListed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-options-hidden", ct);
        var (service, adminId, _, _) = await SetupAsync(fixture, ct);
        await AddGenreAsync(fixture, "Sichtbar", ct);
        await AddGenreAsync(fixture, "Versteckt", ct, isHidden: true);

        var options = await service.GetGenreMaterialOptionsAsync(adminId, null, ct);

        Assert.Equal(["Sichtbar"], options.Select(o => o.Name).ToArray());
    }

    [Fact]
    public async Task GetGenreMaterialOptions_DifferentiatesPerSource()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-options-sources", ct);
        var (service, adminId, _, _) = await SetupAsync(fixture, ct);
        // Live finding: „Family" hidden→„Familie" on source 1, still visible on source 2.
        await AddGenreAsync(fixture, "Familie", ct, sourceId: 1);
        await AddGenreAsync(fixture, "Family", ct, sourceId: 1, isHidden: true, alternates: ["Familie"]);
        await AddGenreAsync(fixture, "Family", ct, sourceId: 2);

        var all = await service.GetGenreMaterialOptionsAsync(adminId, null, ct);
        Assert.Equal(new HashSet<string>(["Familie", "Family"]), all.Select(o => o.Name).ToHashSet());

        var onlySource1 = await service.GetGenreMaterialOptionsAsync(adminId, [1L], ct);
        Assert.Equal(["Familie"], onlySource1.Select(o => o.Name).ToArray());

        var onlySource2 = await service.GetGenreMaterialOptionsAsync(adminId, [2L], ct);
        Assert.Equal(["Family"], onlySource2.Select(o => o.Name).ToArray());
    }

    [Fact]
    public async Task GetGenreMaterialOptions_OutOfSeasonGenre_ListedWithWindowLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-options-season", ct);
        var (service, adminId, _, _) = await SetupAsync(fixture, ct);
        await AddGenreAsync(fixture, "Weihnachten", ct, start: new DateTime(2025, 12, 1), end: new DateTime(2025, 12, 26));
        await AddGenreAsync(fixture, "Immer", ct);

        var options = await service.GetGenreMaterialOptionsAsync(adminId, null, ct);

        Assert.Equal(["Immer", "Weihnachten (01.12.–26.12.)"], options.Select(o => o.Name).ToArray());
    }

    [Fact]
    public async Task GetGenreMaterialOptions_RequiresAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("rule-options-auth", ct);
        var (service, _, userId, _) = await SetupAsync(fixture, ct);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.GetGenreMaterialOptionsAsync(userId, null, ct));
    }
}
