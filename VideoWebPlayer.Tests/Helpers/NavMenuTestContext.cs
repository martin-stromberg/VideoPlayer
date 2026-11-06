using System.Security.Claims;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Layout;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Gemeinsames bUnit-Setup der NavMenu-Tests: InMemory-<see cref="ApplicationDbContext"/>, lockeres
/// JSInterop, Autorisierung (optional Admin-Claim bzw. NameIdentifier) sowie die vom
/// <see cref="NavMenu"/> injizierten Services (<see cref="ProgramSettingsService"/>,
/// <see cref="NoOpVideoWebPlayerClient"/>, ILoginIpBlockService-Mock).
/// <see cref="Dispose"/> gibt BunitContext und DbContext frei — letzteren würde der DI-Container als
/// extern erzeugte Singleton-Instanz nicht selbst freigeben.
/// </summary>
public sealed class NavMenuTestContext : IDisposable
{
    private readonly BunitContext _bunitContext;

    private NavMenuTestContext(BunitContext bunitContext, ApplicationDbContext db)
    {
        _bunitContext = bunitContext;
        Db = db;
    }

    /// <summary>Der registrierte InMemory-<see cref="ApplicationDbContext"/> — zum Seeden vor dem Rendern.</summary>
    public ApplicationDbContext Db { get; }

    /// <param name="isAdmin">Setzt den Claim <c>IsAdmin=True</c> und meldet als „admin-user" an.</param>
    /// <param name="userId">Wenn gesetzt, wird ein NameIdentifier-Claim mit dieser Id ergänzt.</param>
    /// <param name="sources">Quellen, die der <see cref="NoOpVideoWebPlayerClient"/> ausliefern soll.</param>
    public static NavMenuTestContext Create(bool isAdmin = false, string? userId = null, params DtoMediaSource[] sources)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, new EventManager());

        var bunitContext = new BunitContext();
        bunitContext.JSInterop.Mode = JSRuntimeMode.Loose;
        var auth = bunitContext.AddAuthorization().SetAuthorized(isAdmin ? "admin-user" : "test-user");
        var claims = new List<Claim>();
        if (isAdmin)
            claims.Add(new Claim("IsAdmin", "True"));
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (claims.Count > 0)
            auth.SetClaims(claims.ToArray());

        bunitContext.Services.AddSingleton(db);
        bunitContext.Services.AddSingleton(new ProgramSettingsService(db, NullLogger<ProgramSettingsService>.Instance));
        bunitContext.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient(sources));
        bunitContext.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);

        return new NavMenuTestContext(bunitContext, db);
    }

    /// <summary>Rendert das <see cref="NavMenu"/> mit den in <see cref="Create"/> registrierten Services.</summary>
    public IRenderedComponent<NavMenu> RenderNavMenu() => _bunitContext.Render<NavMenu>();

    public void Dispose()
    {
        _bunitContext.Dispose();
        Db.Dispose();
    }
}
