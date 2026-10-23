using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Controller-level tests for <see cref="ProgramGuideController"/>: authentication, input
/// validation and result mapping. The token check itself (<see cref="BearerTokenCheckAttribute"/>)
/// is covered by the HTTP-level <c>ProgramGuideE2ETests</c>.
/// </summary>
public sealed class ProgramGuideControllerTests : IAsyncLifetime
{
    private ProgramScheduleTestDb _fixture = null!;
    private FakeAuthService _fakeAuth = null!;
    private ProgramGuideController _controller = null!;
    private TVChannel _channel = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _fixture = await ProgramScheduleTestDb.CreateAsync($"guide-controller-{Guid.NewGuid():N}", ct);
        _fakeAuth = new FakeAuthService { CurrentUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "guide-user@test.com" } };
        _fixture.Db.Users.Add(_fakeAuth.CurrentUser);
        await _fixture.Db.SaveChangesAsync(ct);
        _channel = await new ProgramRuleService(_fixture.Db).GetOrCreateChannelAsync(ct);
        _controller = CreateController(_fakeAuth);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        return ValueTask.CompletedTask;
    }

    private ProgramGuideController CreateController(FakeAuthService auth)
        => new(new ProgramGuideService(_fixture.Db),
            new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
            auth, NullLogger<ProgramGuideController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public async Task GetNow_FeatureNotReleased_Returns404()
    {
        (await _fixture.Db.Setups.FirstAsync(TestContext.Current.CancellationToken)).TvSenderEnabled = false;
        await _fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _controller.GetNow();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetUpcoming_FeatureNotReleased_Returns404()
    {
        (await _fixture.Db.Setups.FirstAsync(TestContext.Current.CancellationToken)).TvSenderEnabled = false;
        await _fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _controller.GetUpcoming();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetNow_LoggedIn_ReturnsDtoWithRunningBroadcast()
    {
        var item = await _fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(60));
        var now = DateTime.UtcNow;
        await _fixture.CreateSlotAsync(_channel.Id, now.AddMinutes(-10), now.AddMinutes(50), item.Id);

        var result = await _controller.GetNow();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoProgramGuideNow>(ok.Value);
        Assert.NotNull(dto.Now);
    }

    [Fact]
    public async Task GetNow_Anonymous_Returns401()
    {
        var controller = CreateController(new FakeAuthService { CurrentUser = null });

        var result = await controller.GetNow();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetUpcoming_Anonymous_Returns401()
    {
        var controller = CreateController(new FakeAuthService { CurrentUser = null });

        var result = await controller.GetUpcoming();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(13)]
    [InlineData(48)]
    [InlineData(int.MaxValue)]
    public async Task GetUpcoming_HoursOutOfRange_Returns400(int hours)
    {
        var result = await _controller.GetUpcoming(hours);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetUpcoming_OmittedHours_UsesDefaultAndReturns200()
    {
        var item = await _fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(60));
        var now = DateTime.UtcNow;
        var slot = await _fixture.CreateSlotAsync(_channel.Id, now.AddHours(1), now.AddHours(2), item.Id);

        var result = await _controller.GetUpcoming();

        var ok = Assert.IsType<OkObjectResult>(result);
        var entries = Assert.IsType<DtoProgramGuideEntry[]>(ok.Value);
        Assert.Equal([slot.Id], entries.Select(e => e.SlotId).ToArray());
    }
}
