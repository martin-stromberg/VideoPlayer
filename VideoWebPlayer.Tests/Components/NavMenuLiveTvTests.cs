using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Components.Layout;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Der Live-TV-Eintrag der Hauptnavigation (Schritt 7 des TV-Senders): genau ein Link auf
/// <c>/live-tv</c> mit verständlicher Beschriftung für angemeldete Benutzer.
/// </summary>
public class NavMenuLiveTvTests
{
    [Fact]
    public void NavMenu_ContainsExactlyOneLiveTvEntry_ForAuthorizedUser()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options, new EventManager());
        db.Setups.Add(new Setup { TvSenderEnabled = true });
        db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = "test-user-id" });
        db.SaveChanges();

        using var ctx = new global::Bunit.BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization().SetAuthorized("test-user").SetClaims(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id"));
        ctx.Services.AddSingleton(db);
        ctx.Services.AddSingleton(new ProgramSettingsService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramSettingsService>.Instance));
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);

        var cut = ctx.Render<NavMenu>();

        var links = cut.FindAll("a.nav-link").Where(a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var link = Assert.Single(links);
        Assert.Equal("Live-TV", link.TextContent.Trim());
    }

    [Fact]
    public void NavMenu_HidesLiveTvEntry_WhenFeatureNotReleased()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options, new EventManager());
        db.Setups.Add(new Setup { TvSenderEnabled = false });
        db.SaveChanges();

        using var ctx = new global::Bunit.BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization().SetAuthorized("test-user").SetClaims(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id"));
        ctx.Services.AddSingleton(db);
        ctx.Services.AddSingleton(new ProgramSettingsService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramSettingsService>.Instance));
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);

        var cut = ctx.Render<NavMenu>();

        Assert.DoesNotContain(cut.FindAll("a.nav-link"), a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void NavMenu_HidesLiveTvEntry_WhenUserNotReleased()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options, new EventManager());
        db.Setups.Add(new Setup { TvSenderEnabled = true });
        db.SaveChanges();

        using var ctx = new global::Bunit.BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization().SetAuthorized("test-user").SetClaims(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user-id"));
        ctx.Services.AddSingleton(db);
        ctx.Services.AddSingleton(new ProgramSettingsService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramSettingsService>.Instance));
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);

        var cut = ctx.Render<NavMenu>();

        Assert.DoesNotContain(cut.FindAll("a.nav-link"), a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void NavMenu_ShowsLiveTvEntry_ForAdminWithoutAccessRow()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options, new EventManager());
        db.Setups.Add(new Setup { TvSenderEnabled = true });
        db.SaveChanges();

        using var ctx = new global::Bunit.BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization().SetAuthorized("admin-user").SetClaims(new System.Security.Claims.Claim("IsAdmin", "True"));
        ctx.Services.AddSingleton(db);
        ctx.Services.AddSingleton(new ProgramSettingsService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramSettingsService>.Instance));
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);

        var cut = ctx.Render<NavMenu>();

        Assert.Contains(cut.FindAll("a.nav-link"), a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true);
    }
}
