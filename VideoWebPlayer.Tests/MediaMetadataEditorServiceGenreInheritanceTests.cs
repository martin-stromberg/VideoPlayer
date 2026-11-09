using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests für die globale Genre-Konfigurationsvererbung im Metadaten-Edit (N7):
/// `MediaMetadataEditorService.NormalizeGenresAsync` ist der zweite Erzeugungspfad —
/// neu angelegte Genre-Zeilen erben die Konfiguration einer gleichnamigen
/// (case-insensitiv) konfigurierten Zeile anderer Quellen.
/// </summary>
public sealed class MediaMetadataEditorServiceGenreInheritanceTests
{
    [Fact]
    public async Task UpdateAsync_NewGenre_InheritsGlobalConfig()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-inherit-edit?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = new MediaSource
        {
            Name = "Quelle 1",
            Path = "/media/one",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        var source2 = new MediaSource
        {
            Name = "Quelle 2",
            Path = "/media/two",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.AddRange(source1, source2);
        await db.SaveChangesAsync(ct);

        var family1 = new Genre { Name = "Family", MediaSourceId = source1.Id, IsHidden = true };
        db.Genres.Add(family1);
        await db.SaveChangesAsync(ct);
        db.GenreNames.Add(new GenreName { Name = "Familie", GenreId = family1.Id });

        var movie = new Movie
        {
            Name = "Familienfilm",
            MediaSourceId = source2.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);

        var service = new MediaMetadataEditorService(db);
        await service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = movie.Id,
            Name = "Familienfilm",
            GenreNames = ["Family"],
        }, ct);

        var family2 = await db.Genres
            .Include(g => g.AlternateNames)
            .SingleAsync(g => g.MediaSourceId == source2.Id && g.Name == "Family", ct);
        Assert.True(family2.IsHidden);
        Assert.Contains(family2.AlternateNames, a => a.Name == "Familie");

        var reloaded = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == movie.Id, ct);
        var link = Assert.Single(reloaded.MovieGenres);
        Assert.Equal(family2.Id, link.GenreId);
    }
}
