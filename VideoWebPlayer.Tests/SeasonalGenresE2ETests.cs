using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using VideoWebPlayer.Client;
using VideoWebPlayer.Data;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace VideoWebPlayer.Tests;

/// <summary>Exercises the real seasonal API, authenticated web home, navigation and expiry.</summary>
[Trait("Category", "E2E")]
public sealed class SeasonalGenresE2ETests : IAsyncLifetime
{
    private const string Email = "seasonal@example.com";
    private const string Password = "P@ssw0rd123!";
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"vwp-seasonal-{Guid.NewGuid()}.db");
    private readonly SeasonalClock clock = new();
    private readonly SeasonalRequests requests = new();
    private WebApplicationFactory<global::Program> factory = null!;
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IBrowserContext context = null!;
    private IPage page = null!;
    private string url = null!;
    private string userId = null!;
    private long genreId;
    private long filmId;
    private long singleId;
    private long collectionId;
    private long showId;

    public async ValueTask InitializeAsync()
    {
        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing").UseUrls("http://127.0.0.1:0").UseStaticWebAssets();
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath}");
            builder.UseSetting("Jwt:Key", jwtKey);
            builder.UseSetting("Jwt:ApiToken", "seasonal-test-api-token");
            builder.UseSetting("Jwt:ApiToken:Maui", "seasonal-test-api-token");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IStartupFilter>(requests);
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
            });
        });
        factory.UseKestrel();
        factory.StartServer();
        url = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        await SeedAsync();
        playwright = await Playwright.CreateAsync();
        // A missing browser is a failed test, never a silent successful skip.
        browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        context = await browser.NewContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
    }

    public async ValueTask DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
        try { File.Delete(dbPath); } catch (IOException) { }
    }

    [Fact]
    public async Task Login_ShowsOnlyActiveAuthorizedSectionsInOrder_WithWatchedMark()
    {
        await LoginAsync();
        var sections = page.Locator("section:has(> h3[id^='genre-'])");
        await Expect(sections).ToHaveCountAsync(2);
        await Expect(sections.Locator("h3")).ToHaveTextAsync(["Halloween", "Autumn"]);
        await Expect(page.GetByText("Secret Film", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Inactive", Exact = true })).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Empty", Exact = true })).ToHaveCountAsync(0);
        var film = page.Locator($"#genre-{genreId}").Locator("..").Locator($"a[href='/movie/{filmId}']");
        await Expect(film).ToBeVisibleAsync();
        await Expect(film.Locator(".watched-indicator")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SeasonalCards_OpenMatchingMovieCollectionShowAndSingletonTargets()
    {
        await LoginAsync();
        foreach (var target in new[] { $"/movie/{filmId}", $"/moviecollection/{collectionId}", $"/tvshow/{showId}", $"/movie/{singleId}" })
        {
            await page.Locator($"#genre-{genreId}").Locator("..").Locator($"a[href='{target}']").ClickAsync();
            await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == target);
            await Expect(page.Locator("h1,h2").First).ToBeVisibleAsync();
            await page.GotoAsync(url + "/");
            await Expect(page.Locator($"#genre-{genreId}")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task WebUsesAuthenticatedHttpContract_AndAnonymousApiIsUnauthorized()
    {
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        using var unauthorized = await http.GetAsync("/api/home/seasonal-genres", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await LoginAsync();
        Assert.Contains(requests.SuccessfulUsers, x => x == userId);
        var client = new VideoWebPlayerClient(http, Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoWebPlayerClient>.Instance);
        http.DefaultRequestHeaders.Add("X-API-Key", "seasonal-test-api-token");
        await client.AuthenticateAsync(Email, Password);
        var sections = await client.GetSeasonalGenresAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, sections.Count);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), sections[0].ExpiresAt);
        Assert.Contains(sections[0].Entries, x => x is DtoMovieCollection && x.Id == collectionId);
        Assert.DoesNotContain(sections.SelectMany(x => x.Entries), x => x.Name == "Secret Film");
        clock.SetUtc(new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Empty(await client.GetSeasonalGenresAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenHome_RemovesExpiredSectionsWithoutNavigation_AndReloadStaysEmpty()
    {
        await LoginAsync();
        clock.SetUtc(new(2026, 10, 31, 23, 59, 59, TimeSpan.Zero));
        await Expect(page.Locator($"#genre-{genreId}")).ToBeVisibleAsync();
        clock.SetUtc(new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        await Expect(page.Locator("h3[id^='genre-']")).ToHaveCountAsync(0);
        await page.ReloadAsync();
        await Expect(page.Locator("h3[id^='genre-']")).ToHaveCountAsync(0);
    }

    private async Task LoginAsync()
    {
        await page.GotoAsync(url + "/Account/Login?ReturnUrl=%2F");
        await page.FillAsync("#email", Email);
        await page.FillAsync("#password", Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Expect(page.Locator($"#genre-{genreId}")).ToBeVisibleAsync();
        // The first paint may be prerendered; wait for an interactive circuit before timer/navigation assertions.
        await page.WaitForFunctionAsync("() => document.querySelector('blazor-error-ui') === null && typeof Blazor !== 'undefined'");
        await page.WaitForTimeoutAsync(1000);
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = Email, Email = Email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(x => x.Description)));
        userId = user.Id;
        var allowed = new MediaSource { Name = "Allowed", Path = "/test", Host = "127.0.0.1", Port = 22 };
        var denied = new MediaSource { Name = "Denied", Path = "/secret", Host = "127.0.0.1", Port = 22 };
        db.AddRange(allowed, denied);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = allowed.Id });
        var halloween = new Genre { Name = "Halloween", MediaSourceId = allowed.Id, StartDate = new(2025, 10, 25), EndDate = new(2025, 10, 31) };
        var autumn = new Genre { Name = "Autumn", MediaSourceId = allowed.Id, StartDate = halloween.StartDate, EndDate = halloween.EndDate };
        var empty = new Genre { Name = "Empty", MediaSourceId = allowed.Id, StartDate = halloween.StartDate, EndDate = halloween.EndDate };
        var inactive = new Genre { Name = "Inactive", MediaSourceId = allowed.Id, StartDate = new(2025, 12, 1), EndDate = new(2025, 12, 31) };
        var multi = new MovieCollection { Name = "Season Collection", MediaSourceId = allowed.Id };
        var singleton = new MovieCollection { Name = "Singleton collection", MediaSourceId = allowed.Id };
        db.AddRange(halloween, autumn, empty, inactive, multi, singleton);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        genreId = halloween.Id;
        collectionId = multi.Id;
        var film = new Movie { Name = "Season Film", MediaSourceId = allowed.Id };
        var single = new Movie { Name = "Singleton Film", MediaSourceId = allowed.Id, MovieCollectionId = singleton.Id };
        var member1 = new Movie { Name = "Part One", MediaSourceId = allowed.Id, MovieCollectionId = multi.Id };
        var member2 = new Movie { Name = "Part Two", MediaSourceId = allowed.Id, MovieCollectionId = multi.Id };
        var secret = new Movie { Name = "Secret Film", MediaSourceId = denied.Id };
        var show = new TVShow { Name = "Season Show", MediaSourceId = allowed.Id };
        db.AddRange(film, single, member1, member2, secret, show);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        filmId = film.Id;
        singleId = single.Id;
        showId = show.Id;
        foreach (var movie in new[] { film, single, member1, member2, secret })
            db.MovieGenres.Add(new MovieGenre { GenreId = halloween.Id, MovieId = movie.Id });
        db.MovieGenres.Add(new MovieGenre { GenreId = autumn.Id, MovieId = film.Id });
        db.TVShowGenres.Add(new TVShowGenre { GenreId = halloween.Id, TVShowId = show.Id });
        db.WatchedEntries.Add(new WatchedEntry { UserId = user.Id, MovieId = film.Id, WatchedAt = new(2026, 10, 25) });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class SeasonalRequests : IStartupFilter
    {
        public ConcurrentBag<string> SuccessfulUsers { get; } = [];
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                await continuation();
                if (context.Request.Path == "/api/home/seasonal-genres" && context.Response.StatusCode == 200)
                    SuccessfulUsers.Add(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
            });
            next(app);
        };
    }

    private sealed class SeasonalClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        private readonly List<SeasonalTimer> timers = [];
        public override DateTimeOffset GetUtcNow() { lock (timers) return now; }
        public void SetUtc(DateTimeOffset value)
        {
            SeasonalTimer[] copy;
            lock (timers) { now = value; copy = timers.ToArray(); }
            foreach (var timer in copy) timer.FireIfDue(value);
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (timers)
            {
                var timer = new SeasonalTimer(callback, state, now + dueTime);
                timers.Add(timer);
                return timer;
            }
        }
        private sealed class SeasonalTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            private bool disposed;
            public void FireIfDue(DateTimeOffset now) { if (!disposed && now >= due) { disposed = true; callback(state); } }
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
