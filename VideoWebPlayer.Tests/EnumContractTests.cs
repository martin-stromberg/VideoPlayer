using System.Reflection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Pins the members of enums that are part of a contract (mapped between assemblies or reported to API clients),
/// so a rename, removal or reordering is a deliberate change that has to touch these tests as well.
/// </summary>
public class EnumContractTests
{
    private static readonly string[] ExpectedRecentEntryTypes = ["Movie", "MovieCollection", "TVShow", "TVShowSeason", "TVShowEpisode"];

    private static readonly string[] ExpectedJobStatuses = ["Idle", "Queued", "Running", "Succeeded", "Failed"];

    /// <summary>
    /// <c>RecentEntryType</c> exists twice in the global namespace, in the client assembly (used by
    /// <see cref="DtoRecentEntry"/>) and in the server assembly (used by <see cref="RecentEntry"/>); the two are
    /// mapped between each other, so their members and numeric values must be identical.
    /// </summary>
    [Fact]
    public void RecentEntryType_ClientAndServerDefinitions_HaveTheSameMembersAndValues()
    {
        var clientType = typeof(DtoRecentEntry).Assembly.GetType("RecentEntryType");
        var serverType = typeof(RecentEntry).Assembly.GetType("RecentEntryType");

        Assert.NotNull(clientType);
        Assert.NotNull(serverType);
        Assert.NotSame(clientType, serverType);
        Assert.Equal(ExpectedRecentEntryTypes, Members(clientType!).Select(m => m.Name));
        Assert.Equal(Members(clientType!), Members(serverType!));
    }

    /// <summary>
    /// The states of a manual backup job (<see cref="ManualBackupJobStatus"/>) are reported to the administration
    /// page; the members, their order and their numeric values are part of that contract.
    /// </summary>
    [Fact]
    public void ManualBackupJobStatus_HasTheDocumentedStatesInOrder()
    {
        Assert.Equal(ExpectedJobStatuses, Enum.GetNames<ManualBackupJobStatus>());
        Assert.Equal(0, (int)ManualBackupJobStatus.Idle);
        Assert.Equal(1, (int)ManualBackupJobStatus.Queued);
        Assert.Equal(2, (int)ManualBackupJobStatus.Running);
        Assert.Equal(3, (int)ManualBackupJobStatus.Succeeded);
        Assert.Equal(4, (int)ManualBackupJobStatus.Failed);
    }

    /// <summary>
    /// The states of a restore job (<see cref="RestoreBackupJobStatus"/>) mirror those of a manual backup job.
    /// </summary>
    [Fact]
    public void RestoreBackupJobStatus_HasTheDocumentedStatesInOrder_AndMirrorsTheManualBackupStates()
    {
        Assert.Equal(ExpectedJobStatuses, Enum.GetNames<RestoreBackupJobStatus>());
        Assert.Equal(0, (int)RestoreBackupJobStatus.Idle);
        Assert.Equal(1, (int)RestoreBackupJobStatus.Queued);
        Assert.Equal(2, (int)RestoreBackupJobStatus.Running);
        Assert.Equal(3, (int)RestoreBackupJobStatus.Succeeded);
        Assert.Equal(4, (int)RestoreBackupJobStatus.Failed);
        Assert.Equal(
            Enum.GetValues<ManualBackupJobStatus>().Select(s => (int)s),
            Enum.GetValues<RestoreBackupJobStatus>().Select(s => (int)s));
    }

    /// <summary>
    /// <see cref="PairingCodeKind"/> is stored as a number in the <c>PairingCodes.Kind</c> column, so the members and their
    /// numeric values must not change silently. <see cref="PairingCodeKind.DeviceInitiated"/> is reserved for a
    /// direction that is not implemented yet.
    /// </summary>
    [Fact]
    public void PairingCodeKind_HasTheStoredNumericValues()
    {
        Assert.Equal(new[] { "AdminCode", "BootstrapTicket", "DeviceInitiated" }, Enum.GetNames<PairingCodeKind>());
        Assert.Equal(0, (int)PairingCodeKind.AdminCode);
        Assert.Equal(1, (int)PairingCodeKind.BootstrapTicket);
        Assert.Equal(2, (int)PairingCodeKind.DeviceInitiated);
    }

    private static (string Name, object Value)[] Members(Type enumType)
        => enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (f.Name, Convert.ChangeType(f.GetRawConstantValue()!, typeof(long)) ?? 0L))
            .ToArray();
}
