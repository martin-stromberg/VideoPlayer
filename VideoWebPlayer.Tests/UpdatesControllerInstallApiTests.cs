using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using msTools.Updater;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <c>POST /api/updates/install</c>, the fire-and-forget endpoint called by the static
/// update progress page before the update shuts the process down.
/// </summary>
public sealed class UpdatesControllerInstallApiTests
{
    [Fact]
    public void InstallFromProgressPage_IsExposedAtApiUpdatesInstall()
    {
        var method = typeof(UpdatesController).GetMethod(nameof(UpdatesController.InstallFromProgressPage));

        Assert.NotNull(method);
        var httpPost = method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .OfType<HttpPostAttribute>()
            .Single();
        Assert.Equal("~/api/updates/install", httpPost.Template);
    }

    [Fact]
    public async Task InstallFromProgressPage_WithoutAjaxHeader_ReturnsBadRequest()
    {
        var controller = CreateController(InstallableStatus(), out _);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.InstallFromProgressPage(TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(AutoUpdateState.Checking)]
    [InlineData(AutoUpdateState.Downloading)]
    [InlineData(AutoUpdateState.Installing)]
    public async Task InstallFromProgressPage_WhenBusy_ReturnsConflictWithoutInstall(AutoUpdateState state)
    {
        var status = new StatusBuilder(state, availableVersion: "2.0.0");
        var controller = CreateController(status, out var backend);
        controller.ControllerContext = AjaxContext();

        var result = await controller.InstallFromProgressPage(TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        await AssertNoInstallStartedAsync(backend);
    }

    [Fact]
    public async Task InstallFromProgressPage_WithoutInstallableVersion_ReturnsConflictWithoutInstall()
    {
        var controller = CreateController(new StatusBuilder(AutoUpdateState.Idle), out var backend);
        controller.ControllerContext = AjaxContext();

        var result = await controller.InstallFromProgressPage(TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        await AssertNoInstallStartedAsync(backend);
    }

    [Fact]
    public async Task InstallFromProgressPage_WhenLocked_ReturnsConflictWithoutInstall()
    {
        var controller = CreateController(
            new StatusBuilder(AutoUpdateState.UpdateAvailable, availableVersion: "2.0.0", isLocked: true),
            out var backend);
        controller.ControllerContext = AjaxContext();

        var result = await controller.InstallFromProgressPage(TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
        await AssertNoInstallStartedAsync(backend);
    }

    [Theory]
    [InlineData(AutoUpdateState.UpdateAvailable)]
    [InlineData(AutoUpdateState.ReadyToInstall)]
    public async Task InstallFromProgressPage_WhenInstallable_ReturnsAcceptedAndStartsInstall(AutoUpdateState state)
    {
        var controller = CreateController(new StatusBuilder(state, availableVersion: "2.0.0"), out var backend);
        controller.ControllerContext = AjaxContext();

        var result = await controller.InstallFromProgressPage(TestContext.Current.CancellationToken);

        Assert.IsType<AcceptedResult>(result);
        await WaitForAsync(() => backend.InstallCount > 0);
        Assert.Equal(1, backend.InstallCount);
    }

    [Fact]
    public async Task InstallFromProgressPage_RunsInstallWithUncancellableToken()
    {
        var controller = CreateController(new StatusBuilder(AutoUpdateState.ReadyToInstall, availableVersion: "2.0.0"), out var backend);
        controller.ControllerContext = AjaxContext();

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = await controller.InstallFromProgressPage(cancelled.Token);

        Assert.IsType<AcceptedResult>(result);
        await WaitForAsync(() => backend.InstallCount > 0);
        Assert.False(backend.ObservedInstallToken.CanBeCanceled);
    }

    private static async Task AssertNoInstallStartedAsync(FakeBackend backend)
    {
        // Gibt dem Hintergrundtask Gelegenheit, versehentlich doch zu starten.
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Equal(0, backend.InstallCount);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Der Hintergrundtask wurde nicht rechtzeitig ausgeführt.");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    private static StatusBuilder InstallableStatus()
        => new(AutoUpdateState.ReadyToInstall, availableVersion: "2.0.0");

    private static ControllerContext AjaxContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        return new ControllerContext { HttpContext = httpContext };
    }

    private static UpdatesController CreateController(StatusBuilder status, out FakeBackend backend)
    {
        backend = new FakeBackend(status.Build());

        var service = new UpdateAdminService(
            backend,
            backend,
            backend,
            NullLogger<UpdateAdminService>.Instance);

        var provider = new Mock<IServiceProvider>();
        provider
            .Setup(x => x.GetService(typeof(UpdateAdminService)))
            .Returns(service);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new UpdatesController(
            service,
            Mock.Of<IAntiforgery>(),
            scopeFactory.Object,
            NullLogger<UpdatesController>.Instance);
    }

    /// <summary>
    /// Fluent builder for <see cref="AutoUpdateStatusSnapshot"/>, matching the shape used by the
    /// existing controller tests.
    /// </summary>
    private sealed class StatusBuilder(
        AutoUpdateState state,
        string? availableVersion = null,
        bool isLocked = false)
    {
        public AutoUpdateStatusSnapshot Build()
            => new(
                state,
                InstalledVersion: "1.0.0",
                AvailableVersion: availableVersion,
                LastCheckedAt: null,
                LastCheckResult: null!,
                LastDownloadResult: null!,
                LastInstallResult: null!,
                LastError: null,
                LastErrorCode: null,
                IsLocked: isLocked,
                LockCreatedAt: null);
    }

    private sealed class FakeBackend : IUpdateSettingsService, IAutoUpdateOrchestrator, IAutoUpdateCommandHandler
    {
        private readonly AutoUpdateStatusSnapshot _status;

        public FakeBackend(AutoUpdateStatusSnapshot status) => _status = status;

        public int InstallCount { get; private set; }

        public CancellationToken ObservedInstallToken { get; private set; }

        public Task<UpdateSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UpdateSettings());

        public UpdateSettings GetDefaultSettings() => new();

        public Task<UpdateSettings> UpdateAsync(UpdateSettingsUpdate update, CancellationToken cancellationToken = default)
            => Task.FromResult(new UpdateSettings());

        public Task ApplyToRuntimeOptionsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<UpdateBackupOptions> GetBackupOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UpdateBackupOptions());

        public Task<AutoUpdateStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_status);

        public Task<AutoUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
            => CheckAsync(cancellationToken);

        public Task<AutoUpdateResult> RunUpdateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "ok", null!));

        public Task<AutoUpdateResult> CheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Idle, "ok", null!));

        public Task<AutoUpdateResult> DownloadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "ok", null!));

        public Task<AutoUpdateResult> InstallAsync(bool restartService, bool force, CancellationToken cancellationToken = default)
        {
            InstallCount++;
            ObservedInstallToken = cancellationToken;
            return Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, "ok", null!));
        }
    }
}
