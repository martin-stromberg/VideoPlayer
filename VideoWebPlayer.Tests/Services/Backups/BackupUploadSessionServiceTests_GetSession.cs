using VideoWebPlayer.Tests.Helpers;
using Xunit;
using static VideoWebPlayer.Tests.Helpers.BackupUploadSessionServiceTestSupport;

namespace VideoWebPlayer.Tests.Services.Backups;

public sealed class BackupUploadSessionServiceTests_GetSession
{
    [Fact]
    public void GetSession_WithUnknownId_ReturnsNull()
    {
        using var provider = CreateProvider();
        var service = CreateService(provider);

        Assert.Null(service.GetSession(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSession_WithActiveSession_ReturnsSession()
    {
        using var provider = CreateProvider();
        var service = CreateService(provider);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            Assert.Same(session, service.GetSession(session.Id));
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }

    [Fact]
    public async Task GetSession_WhenSessionExpired_ReturnsNull()
    {
        using var provider = CreateProvider();
        var time = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        var service = CreateService(provider, time);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            time.Advance(TimeSpan.FromHours(25));

            Assert.Null(service.GetSession(session.Id));
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }
}
