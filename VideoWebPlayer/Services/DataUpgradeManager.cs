using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

public class DataUpgradeManager
{
    private readonly ApplicationDbContext _db;
    public const int CurrentVersion = 4; // Hier die aktuelle Version anpassen

    public DataUpgradeManager(ApplicationDbContext db)
    {
        _db = db;
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
                    // Weitere Upgrades hier ergänzen
            }
            setup.DataVersion = nextVersion;
            await _db.SaveChangesAsync(cancellationToken);
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

    private async Task MarkAllMediaItemsAsChangedAsync(CancellationToken cancellationToken)
    {
        // Alle MediaItems als geändert markieren
        await _db.MediaItems.ExecuteUpdateAsync(
            s => s.SetProperty(mi => mi.Changed, true),
            cancellationToken
        );
    }
}