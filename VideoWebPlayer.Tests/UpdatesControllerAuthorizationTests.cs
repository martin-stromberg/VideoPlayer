using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using msTools.Updater;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class UpdatesControllerAuthorizationTests
{
    [Fact]
    public void UpdatesController_RequiresAdminOnlyPolicy()
    {
        var authorize = typeof(UpdatesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Equal("AdminOnly", authorize.Policy);
    }

    [Theory]
    [InlineData(nameof(UpdatesController.Check), "check")]
    [InlineData(nameof(UpdatesController.Install), "install")]
    public void ActionEndpoint_IsExposedAsServerSidePost(string methodName, string template)
    {
        var method = typeof(UpdatesController).GetMethod(methodName);

        Assert.NotNull(method);
        var httpPost = method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .OfType<HttpPostAttribute>()
            .Single();
        Assert.Equal(template, httpPost.Template);
    }

    [Fact]
    public async Task Check_ValidatesAntiforgeryToken()
    {
        var httpContext = new DefaultHttpContext();
        var antiforgery = new Mock<IAntiforgery>();
        var controller = CreateController(antiforgery.Object, Status(AutoUpdateState.Idle));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.Check(TestContext.Current.CancellationToken);

        antiforgery.Verify(x => x.IsRequestValidAsync(httpContext), Times.Once);
    }

    [Fact]
    public async Task Install_ValidatesAntiforgeryToken()
    {
        var httpContext = new DefaultHttpContext();
        var antiforgery = new Mock<IAntiforgery>();
        var controller = CreateController(antiforgery.Object, Status(AutoUpdateState.Idle));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.Install(TestContext.Current.CancellationToken);

        antiforgery.Verify(x => x.IsRequestValidAsync(httpContext), Times.Once);
    }

    private static UpdatesController CreateController(IAntiforgery antiforgery, AutoUpdateStatusSnapshot status)
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler
            .Setup(x => x.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Idle, "ok", null!));

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

        var service = new UpdateAdminService(
            orchestrator.Object,
            commandHandler.Object,
            settings.Object,
            NullLogger<UpdateAdminService>.Instance);

        return new UpdatesController(service, antiforgery, NullLogger<UpdatesController>.Instance);
    }

    private static AutoUpdateStatusSnapshot Status(AutoUpdateState state)
        => new(
            state,
            InstalledVersion: "1.0.0",
            AvailableVersion: null,
            LastCheckedAt: null,
            LastCheckResult: null!,
            LastDownloadResult: null!,
            LastInstallResult: null!,
            LastError: null,
            LastErrorCode: null,
            IsLocked: false,
            LockCreatedAt: null);
}
