using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests für die globale Genre-Konfigurationsvererbung (N7): neue Genre-Zeilen erben
/// IsHidden/Saisonfenster/Alternativnamen einer gleichnamigen (case-insensitiv)
/// konfigurierten Zeile anderer Quellen; unveränderte Default-Zeilen werden geheilt;
/// geerbte Alternativnamen lenken Inhalte auf das kanonische Genre derselben Quelle um.
/// </summary>
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceClassifierGenreInheritanceTests
{
    [Fact]
    public async Task ReloadGenres_NewGenreOnOtherSource_InheritsHiddenAndAlternates_RedirectsToCanonical()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-inherit-redirect?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        var source2 = CreateSource("Quelle 2", "/media/two");
        db.MediaSources.AddRange(source1, source2);
        await db.SaveChangesAsync(ct);

        // „Family" ist auf Quelle 1 ausgeblendet und wird auf „Familie" umgelenkt.
        var family1 = new Genre { Name = "Family", MediaSourceId = source1.Id, IsHidden = true };
        var familie1 = new Genre { Name = "Familie", MediaSourceId = source1.Id };
        db.Genres.AddRange(family1, familie1);
        await db.SaveChangesAsync(ct);
        db.GenreNames.Add(new GenreName { Name = "Familie", GenreId = family1.Id });

        var movie = new Movie
        {
            Name = "Familienfilm",
            MediaSourceId = source2.Id,
            GenreNames = "Family",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct);

        // Neue „Family"-Zeile auf Quelle 2 ist ausgeblendet und trägt den Alternativnamen.
        var family2 = await db.Genres
            .Include(g => g.AlternateNames)
            .SingleAsync(g => g.MediaSourceId == source2.Id && g.Name == "Family", ct);
        Assert.True(family2.IsHidden);
        Assert.Contains(family2.AlternateNames, a => a.Name == "Familie");

        // Der Inhalt landet beim kanonischen Genre derselben Quelle.
        var familie2 = await db.Genres
            .SingleAsync(g => g.MediaSourceId == source2.Id && g.Name == "Familie", ct);
        Assert.False(familie2.IsHidden);

        var reloaded = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == movie.Id, ct);
        Assert.Equal("Familie", reloaded.GenreNames);
        var link = Assert.Single(reloaded.MovieGenres);
        Assert.Equal(familie2.Id, link.GenreId);
    }

    [Fact]
    public async Task ReloadGenres_NewGenre_InheritsSeasonalWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-inherit-window?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        var source2 = CreateSource("Quelle 2", "/media/two");
        db.MediaSources.AddRange(source1, source2);
        await db.SaveChangesAsync(ct);

        var start = new DateTime(2025, 12, 1);
        var end = new DateTime(2025, 12, 26);
        db.Genres.Add(new Genre
        {
            Name = "Weihnachten",
            MediaSourceId = source1.Id,
            StartDate = start,
            EndDate = end
        });

        var movie = new Movie
        {
            Name = "Weihnachtsfilm",
            MediaSourceId = source2.Id,
            GenreNames = "Weihnachten",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct);

        var weihnachten2 = await db.Genres
            .Include(g => g.AlternateNames)
            .SingleAsync(g => g.MediaSourceId == source2.Id && g.Name == "Weihnachten", ct);
        Assert.False(weihnachten2.IsHidden);
        Assert.Equal(start, weihnachten2.StartDate);
        Assert.Equal(end, weihnachten2.EndDate);
        Assert.Empty(weihnachten2.AlternateNames);

        var reloaded = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == movie.Id, ct);
        var link = Assert.Single(reloaded.MovieGenres);
        Assert.Equal(weihnachten2.Id, link.GenreId);
    }

    [Fact]
    public async Task ReloadGenres_CaseVariant_InheritsFromCaseInsensitiveMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-inherit-case?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        var source2 = CreateSource("Quelle 2", "/media/two");
        db.MediaSources.AddRange(source1, source2);
        await db.SaveChangesAsync(ct);

        db.Genres.Add(new Genre { Name = "FAMILY", MediaSourceId = source1.Id, IsHidden = true });

        var movie = new Movie
        {
            Name = "Familienfilm",
            MediaSourceId = source2.Id,
            GenreNames = "family",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct);

        var family2 = await db.Genres
            .SingleAsync(g => g.MediaSourceId == source2.Id && g.Name == "family", ct);
        Assert.True(family2.IsHidden);
    }

    [Fact]
    public async Task ReloadGenres_ExistingDefaultRow_HealsFromTemplate()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-heal-default?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        var source2 = CreateSource("Quelle 2", "/media/two");
        db.MediaSources.AddRange(source1, source2);
        await db.SaveChangesAsync(ct);

        var family1 = new Genre { Name = "Family", MediaSourceId = source1.Id, IsHidden = true };
        var familie1 = new Genre { Name = "Familie", MediaSourceId = source1.Id };
        db.Genres.AddRange(family1, familie1);
        await db.SaveChangesAsync(ct);
        db.GenreNames.Add(new GenreName { Name = "Familie", GenreId = family1.Id });

        // Bestandszeile auf Quelle 2: vor der Admin-Ausblendung angelegt, ohne Konfiguration.
        var family2 = new Genre { Name = "Family", MediaSourceId = source2.Id };
        db.Genres.Add(family2);

        var movie = new Movie
        {
            Name = "Familienfilm",
            MediaSourceId = source2.Id,
            GenreNames = "Family",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct);

        var healed = await db.Genres
            .Include(g => g.AlternateNames)
            .SingleAsync(g => g.Id == family2.Id, ct);
        Assert.True(healed.IsHidden);
        Assert.Contains(healed.AlternateNames, a => a.Name == "Familie");

        var familie2 = await db.Genres
            .SingleAsync(g => g.MediaSourceId == source2.Id && g.Name == "Familie", ct);
        var reloaded = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == movie.Id, ct);
        var link = Assert.Single(reloaded.MovieGenres);
        Assert.Equal(familie2.Id, link.GenreId);
    }

    [Fact]
    public async Task ReloadGenres_ExistingConfiguredRow_IsNotOverwritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-keep-config?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        var source2 = CreateSource("Quelle 2", "/media/two");
        db.MediaSources.AddRange(source1, source2);
        await db.SaveChangesAsync(ct);

        db.Genres.Add(new Genre { Name = "Family", MediaSourceId = source1.Id, IsHidden = true });

        // Quelle-2-Zeile trägt bereits eigene Konfiguration (Saisonfenster, sichtbar).
        var family2 = new Genre
        {
            Name = "Family",
            MediaSourceId = source2.Id,
            StartDate = new DateTime(2025, 6, 1),
            EndDate = new DateTime(2025, 8, 31)
        };
        db.Genres.Add(family2);

        var movie = new Movie
        {
            Name = "Familienfilm",
            MediaSourceId = source2.Id,
            GenreNames = "Family",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct);

        var reloaded = await db.Genres
            .Include(g => g.AlternateNames)
            .SingleAsync(g => g.Id == family2.Id, ct);
        Assert.False(reloaded.IsHidden);
        Assert.Equal(new DateTime(2025, 6, 1), reloaded.StartDate);
        Assert.Equal(new DateTime(2025, 8, 31), reloaded.EndDate);
        Assert.Empty(reloaded.AlternateNames);
    }

    [Fact]
    public async Task ReloadGenres_NoConfiguredTemplate_CreatesDefaultGenre()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-no-template?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        db.MediaSources.Add(source1);
        await db.SaveChangesAsync(ct);

        var movie = new Movie
        {
            Name = "Dokumentation",
            MediaSourceId = source1.Id,
            GenreNames = "Dokumentation",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct);

        var genre = await db.Genres
            .Include(g => g.AlternateNames)
            .SingleAsync(g => g.MediaSourceId == source1.Id && g.Name == "Dokumentation", ct);
        Assert.False(genre.IsHidden);
        Assert.Null(genre.StartDate);
        Assert.Null(genre.EndDate);
        Assert.Empty(genre.AlternateNames);

        var reloaded = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == movie.Id, ct);
        var link = Assert.Single(reloaded.MovieGenres);
        Assert.Equal(genre.Id, link.GenreId);
    }

    [Fact]
    public async Task ReloadGenres_AlternateNameCycle_Terminates()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-alt-cycle?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source1 = CreateSource("Quelle 1", "/media/one");
        db.MediaSources.Add(source1);
        await db.SaveChangesAsync(ct);

        var genreA = new Genre { Name = "A", MediaSourceId = source1.Id };
        var genreB = new Genre { Name = "B", MediaSourceId = source1.Id };
        db.Genres.AddRange(genreA, genreB);
        await db.SaveChangesAsync(ct);
        db.GenreNames.AddRange(
            new GenreName { Name = "B", GenreId = genreA.Id },
            new GenreName { Name = "A", GenreId = genreB.Id });

        var movie = new Movie
        {
            Name = "Zyklus",
            MediaSourceId = source1.Id,
            GenreNames = "A",
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var classifier = CreateClassifier(db);
        await classifier.ReloadGenres(ct); // darf nicht endlos rekursieren

        var reloaded = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == movie.Id, ct);
        Assert.Empty(reloaded.MovieGenres);
    }

    private static MediaSource CreateSource(string name, string path)
        => new()
        {
            Name = name,
            Path = path,
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };

    private static MediaSourceClassifier CreateClassifier(ApplicationDbContext db)
        => new(
            db,
            null!,
            null!,
            new EventManager(),
            null!,
            NullLogger<MediaSourceClassifier>.Instance,
            new HttpClient());
}
