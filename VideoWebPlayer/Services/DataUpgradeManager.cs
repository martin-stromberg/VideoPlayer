using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

/// <summary>
/// Handles data schema upgrades and migrations for the application.
/// </summary>
public class DataUpgradeManager
{
    private readonly ApplicationDbContext _db;
    private readonly MediaSourceClassifier _classifier;
    private readonly IUserStore<ApplicationUser> userStore;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IUserEmailStore<ApplicationUser> emailStore;
    private readonly ILogger<DataUpgradeManager> logger;
    /// <summary>
    /// Gets the current data version.
    /// </summary>
    public const int CurrentVersion = 8; // Version erhöhen

    /// <summary>
    /// Initializes a new instance of the <see cref="DataUpgradeManager"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="classifier">Media source classifier.</param>
    /// <param name="UserStore">User store for identity operations.</param>
    /// <param name="UserManager">User manager for identity operations.</param>
    /// <param name="logger">Logger instance.</param>
    public DataUpgradeManager(ApplicationDbContext db, MediaSourceClassifier classifier, IUserStore<ApplicationUser> UserStore, UserManager<ApplicationUser> UserManager, ILogger<DataUpgradeManager> logger)
    {
        _db = db;
        _classifier = classifier;
        userStore = UserStore;
        userManager = UserManager;
        emailStore = (IUserEmailStore<ApplicationUser>)UserStore;
        this.logger = logger;
    }

    /// <summary>
    /// Runs pending data upgrades to bring the database to the current version.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task EnsureUpToDateAsync(CancellationToken cancellationToken)
    {
        var setup = await _db.Setups.FirstOrDefaultAsync(cancellationToken);
        if (setup == null)
        {
            setup = new Setup { DataVersion = 0 };
            _db.Setups.Add(setup);
            await _db.SaveChangesAsync(cancellationToken);
        }

        while (setup.DataVersion < CurrentVersion)
        {
            var nextVersion = setup.DataVersion + 1;
            logger.LogInformation($"Führe Datenupgrade {nextVersion} aus.");
            switch (nextVersion)
            {
                case 1:
                    await Upgrade_1(cancellationToken);
                    break;
                case 2:
                    await Upgrade_2(cancellationToken);
                    break;
                case 3:
                    await Upgrade_3(cancellationToken);
                    break;
                case 4:
                    await Upgrade_4(cancellationToken);
                    break;
                case 5:
                    await Upgrade_5(cancellationToken);
                    break;
                case 6:
                    await Upgrade_6(cancellationToken);
                    break;
                case 7:
                    await Upgrade_7(cancellationToken);
                    break;
                case 8:
                    await Upgrade_8(cancellationToken);
                    break;
            }
            setup.DataVersion = nextVersion;
            await _db.SaveChangesAsync(cancellationToken);
            logger.LogInformation($"Das Datenupgrade {nextVersion} wurde ausgeführt.");
        }
    }

    private async Task Upgrade_1(CancellationToken cancellationToken)
    {
        await MarkAllMediaItemsAsChangedAsync(cancellationToken);
    }
    private async Task Upgrade_2(CancellationToken cancellationToken)
    {
        await MarkAllMediaItemsAsChangedAsync(cancellationToken);
    }
    private async Task Upgrade_3(CancellationToken cancellationToken)
    {
        await MarkAllMediaItemsAsChangedAsync(cancellationToken);
    }
    private async Task Upgrade_4(CancellationToken cancellationToken)
    {
        await MarkAllMediaItemsAsChangedAsync(cancellationToken);
    }
    private async Task Upgrade_5(CancellationToken cancellationToken)
    {
        await _classifier.ReloadGenres(cancellationToken);
    }
    private async Task Upgrade_6(CancellationToken cancellationToken)
    {
        await _classifier.ReloadGenres(cancellationToken);
    }
    private async Task Upgrade_7(CancellationToken cancellationToken)
    {
        await MarkAllMediaItemsAsChangedAsync(cancellationToken);
    }
    private async Task Upgrade_8(CancellationToken cancellationToken)
    {
        var newUser = Activator.CreateInstance<ApplicationUser>();
        await userStore.SetUserNameAsync(newUser, "system", cancellationToken);
        await emailStore.SetEmailAsync(newUser, "system@example.com", cancellationToken);
        newUser.IsAdmin = true;
        var result = await userManager.CreateAsync(newUser, GeneratePassword());
        if (!result.Succeeded)
            throw new Exception($"Fehler beim Erstellen des Systembenutzers: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        var code = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
        var result2 = await userManager.ConfirmEmailAsync(newUser, code);
    }

    private string GeneratePassword()
    {
        return $"{new Guid()}dÜK${new Guid()}";
    }

    private async Task MarkAllMediaItemsAsChangedAsync(CancellationToken cancellationToken)
    {
        // Alle MediaItems als geändert markieren
        await _db.MediaItems
            .Where(s => s.Path.EndsWith("nfo"))
            .ExecuteUpdateAsync(
                s => s.SetProperty(mi => mi.Changed, true),
                cancellationToken);
    }
}