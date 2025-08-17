using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

public class DataUpgradeManager
{
    private readonly ApplicationDbContext _db;
    private readonly MediaSourceClassifier _classifier;
    private readonly ILogger<DataUpgradeManager> logger;
    public const int CurrentVersion = 6; // Version erhöhen

    public DataUpgradeManager(ApplicationDbContext db, MediaSourceClassifier classifier, ILogger<DataUpgradeManager> logger)
    {
        _db = db;
        _classifier = classifier;
        this.logger = logger;
    }

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

    private async Task MarkAllMediaItemsAsChangedAsync(CancellationToken cancellationToken)
    {
        // Alle MediaItems als geändert markieren
        await _db.MediaItems.ExecuteUpdateAsync(
            s => s.SetProperty(mi => mi.Changed, true),
            cancellationToken
        );
    }
}