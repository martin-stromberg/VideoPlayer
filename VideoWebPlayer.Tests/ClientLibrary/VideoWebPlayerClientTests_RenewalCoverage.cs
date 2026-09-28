using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// The library promises the automatic session renewal for <em>every</em> call
/// (<c>docs/help/geraete/client-bibliothek.md</c>, Release Notes). Covered here are the calls that used
/// to go past it: the two picture calls, deleting a media source and reporting playback progress. Only
/// the health check is exempt, because it needs no sign-in at all.
/// </summary>
public sealed class VideoWebPlayerClientTests_RenewalCoverage : DeviceClientTestBase
{
    private const string RefreshRoute = "/api/auth/refresh";

    [Fact]
    public async Task PictureFetch_SurvivesExpiredSession()
    {
        await CreateUserAndPairDeviceAsync($"renewal-picture-{Guid.NewGuid():N}@test.com");
        var pictureId = await SeedPictureAsync();

        ExpireSession();
        var bytes = await Client.GetPictureAsync(pictureId);

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task SourceIconFetch_SurvivesExpiredSession()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"renewal-icon-{Guid.NewGuid():N}@test.com");
        var iconId = await SeedSourceIconAsync(user.Id);

        ExpireSession();
        var bytes = await Client.GetSourcePictureAsync(iconId);

        Assert.Equal(new byte[] { 4, 5, 6 }, bytes);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task DeleteSource_SurvivesExpiredSession()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"renewal-delete-{Guid.NewGuid():N}@test.com", isAdmin: true);
        var sourceId = await SeedMediaSourceAsync(user.Id);

        ExpireSession();
        await Client.DeleteSourceAsync(sourceId);

        Assert.Equal(1, Requests.CountTo(RefreshRoute));
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await db.MediaSources.FindAsync([sourceId], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProgressReport_SurvivesExpiredSession()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"renewal-progress-{Guid.NewGuid():N}@test.com");
        var (movieId, _) = await SeedTwoAccessibleMoviesAsync(user.Id);

        ExpireSession();
        await Client.ReportPlaybackProgressAsync(MediaTypeValues.Movie.ToLowerInvariant(), movieId, 120, 3600);

        Assert.Equal(1, Requests.CountTo(RefreshRoute));
        Assert.Equal(2, Requests.CountTo("/api/continue-watching/progress"));
    }

    /// <summary>
    /// A progress report that cannot be renewed keeps failing with 401 and now carries the status code,
    /// so a caller can tell it apart from any other failure.
    /// </summary>
    [Fact]
    public async Task ProgressReport_WithoutRenewal_FailsWithStatusCode()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"renewal-progress-fail-{Guid.NewGuid():N}@test.com");
        var (movieId, _) = await SeedTwoAccessibleMoviesAsync(user.Id);

        Client.DeviceRefreshToken = null;
        ExpireSession();
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client.ReportPlaybackProgressAsync(MediaTypeValues.Movie.ToLowerInvariant(), movieId, 120, 3600));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(0, Requests.CountTo(RefreshRoute));
    }

    /// <summary>
    /// Seeds a picture with content, so a successful fetch is distinguishable from the silent empty
    /// answer the client gives on failure.
    /// </summary>
    /// <returns>The id of the created picture.</returns>
    private async Task<long> SeedPictureAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var picture = new Picture { Type = "poster", Data = [1, 2, 3], ContentType = "image/png" };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync();
        return picture.Id;
    }

    /// <summary>
    /// Seeds a media source the given user may read, together with its icon.
    /// </summary>
    /// <param name="userId">The id of the user to grant read access to.</param>
    /// <returns>The id of the created icon.</returns>
    private async Task<long> SeedSourceIconAsync(string userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var icon = new MediaSourceIcon { Data = [4, 5, 6], ContentType = "image/png" };
        db.MediaSourceIcons.Add(icon);
        await db.SaveChangesAsync();

        var source = new MediaSource
        {
            Name = $"Icon-Quelle {Guid.NewGuid()}",
            Path = "/icon",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow,
            IconPictureId = icon.Id
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = userId });
        await db.SaveChangesAsync();
        return icon.Id;
    }

    /// <summary>
    /// Seeds a media source the given user may read, for the deletion test.
    /// </summary>
    /// <param name="userId">The id of the user to grant read access to.</param>
    /// <returns>The id of the created media source.</returns>
    private async Task<long> SeedMediaSourceAsync(string userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource
        {
            Name = $"Loesch-Quelle {Guid.NewGuid()}",
            Path = "/loeschen",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = userId });
        await db.SaveChangesAsync();
        return source.Id;
    }
}
