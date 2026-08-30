using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using msTools.Updater;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class UpdateAdminServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_IncludesSettingsStatusAndDefaultSettings()
    {
        var status = Status(AutoUpdateState.Idle);
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator
            .Setup(x => x.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var settings = new Mock<IUpdateSettingsService>();
        settings
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettings { CheckIntervalMinutes = 30, RetainedUpdateBackupCount = 3 });
        settings
            .Setup(x => x.GetDefaultSettings())
            .Returns(new UpdateSettings { CheckIntervalMinutes = 360, RetainedUpdateBackupCount = 5 });

        var service = new UpdateAdminService(
            orchestrator.Object,
            Mock.Of<IAutoUpdateCommandHandler>(),
            settings.Object,
            NullLogger<UpdateAdminService>.Instance);

        var snapshot = await service.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Same(status, snapshot.Status);
        Assert.Equal(30, snapshot.Settings.CheckIntervalMinutes);
        Assert.Equal(360, snapshot.DefaultSettings.CheckIntervalMinutes);
        Assert.Equal(5, snapshot.DefaultSettings.RetainedUpdateBackupCount);
    }

    [Fact]
    public async Task CheckAsync_WhenIdle_CallsCommandHandler()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler
            .Setup(x => x.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Idle, "ok", null!));

        var service = CreateService(
            Status(AutoUpdateState.Idle),
            commandHandler.Object);

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        commandHandler.Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_WhenNoInstallableVersion_Blocks()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>(MockBehavior.Strict);
        var service = CreateService(Status(AutoUpdateState.Idle), commandHandler.Object);

        var result = await service.InstallAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.IsBlocked);
    }

    [Fact]
    public async Task InstallAsync_WhenUpdateAvailable_DownloadsThenInstalls()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler
            .Setup(x => x.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "downloaded", null!));
        commandHandler
            .Setup(x => x.InstallAsync(true, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, "installing", null!));

        var service = CreateService(Status(AutoUpdateState.UpdateAvailable, availableVersion: "1.2.3"), commandHandler.Object);

        var result = await service.InstallAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        commandHandler.Verify(x => x.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        commandHandler.Verify(x => x.InstallAsync(true, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_WhenDownloadIsSkipped_DoesNotInstall()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler
            .Setup(x => x.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Skipped, AutoUpdateState.UpdateAvailable, "skipped", null!));

        var service = CreateService(Status(AutoUpdateState.UpdateAvailable, availableVersion: "1.2.3"), commandHandler.Object);

        var result = await service.InstallAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.IsBlocked);
        Assert.Equal("skipped", result.Message);
        commandHandler.Verify(x => x.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        commandHandler.Verify(x => x.InstallAsync(true, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InstallAsync_WhenDownloadIsCanceled_DoesNotInstall()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler
            .Setup(x => x.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Canceled, AutoUpdateState.UpdateAvailable, "canceled", null!));

        var service = CreateService(Status(AutoUpdateState.UpdateAvailable, availableVersion: "1.2.3"), commandHandler.Object);

        var result = await service.InstallAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.IsBlocked);
        Assert.Equal("canceled", result.Message);
        commandHandler.Verify(x => x.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        commandHandler.Verify(x => x.InstallAsync(true, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManualActions_WhenStatusLocked_AreBlocked()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>(MockBehavior.Strict);
        var service = CreateService(Status(AutoUpdateState.Idle, isLocked: true), commandHandler.Object);

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsBlocked);
        Mock.Get(commandHandler.Object).Verify(x => x.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UpdateAdminService CreateService(
        AutoUpdateStatusSnapshot status,
        IAutoUpdateCommandHandler commandHandler)
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator
            .Setup(x => x.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var settings = new Mock<IUpdateSettingsService>();
        settings
            .Setup(x => x.ApplyToRuntimeOptionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        settings
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettings());
        settings
            .Setup(x => x.GetDefaultSettings())
            .Returns(new UpdateSettings());

        return new UpdateAdminService(
            orchestrator.Object,
            commandHandler,
            settings.Object,
            NullLogger<UpdateAdminService>.Instance);
    }

    private static AutoUpdateStatusSnapshot Status(
        AutoUpdateState state,
        string? availableVersion = null,
        bool isLocked = false)
        => new(
            state,
            InstalledVersion: "1.0.0",
            AvailableVersion: availableVersion,
            LastCheckedAt: null,
            LastCheckResult: availableVersion is null
                ? null!
                : new AutoUpdateCheckResult(availableVersion, null!, null, null, false),
            LastDownloadResult: null!,
            LastInstallResult: null!,
            LastError: null,
            LastErrorCode: null,
            IsLocked: isLocked,
            LockCreatedAt: isLocked ? DateTimeOffset.UtcNow : null);
}
