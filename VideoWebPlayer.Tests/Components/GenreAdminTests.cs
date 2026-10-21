using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests für <see cref="GenreAdmin"/> (N7): <c>SaveGenre</c> muss die Konfiguration auf
/// alle Zeilen der case-insensitiven Namensgruppe anwenden — gleiche Semantik wie die
/// Anzeigegruppierung in <c>ReloadGenres</c>.
/// </summary>
public sealed class GenreAdminTests
{
    [Fact]
    public async Task SaveGenre_AppliesConfigToCaseVariants()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:genre-admin-case?mode=memory&cache=shared";
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
        db.Genres.AddRange(
            new Genre { Name = "Family", MediaSourceId = source1.Id },
            new Genre { Name = "family", MediaSourceId = source2.Id });
        await db.SaveChangesAsync(ct);

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(Path.Combine(Path.GetTempPath(), "genre-admin-wwwroot"));

        using var ctx = new BunitContext();
        ctx.AddAuthorization()
            .SetAuthorized("admin")
            .SetClaims(new Claim("IsAdmin", "True"));
        ctx.Services.AddSingleton<ApplicationDbContext>(db);
        ctx.Services.AddSingleton<IGenreService>(new GenreService(db));
        ctx.Services.AddSingleton(env.Object);

        var cut = ctx.Render<GenreAdmin>();

        // Die Gruppierung zeigt „Family"/„family" als eine Zeile.
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")), TimeSpan.FromSeconds(5));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Bearbeiten")).Click();
        cut.Find("input.form-check-input").Change(true);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();

        cut.WaitForAssertion(() =>
        {
            var states = db.Genres.Select(g => g.IsHidden).ToList();
            Assert.Equal(new[] { true, true }, states);
        }, TimeSpan.FromSeconds(5));
    }
}
