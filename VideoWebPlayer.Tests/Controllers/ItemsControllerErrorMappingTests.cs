using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Covers the status codes the media endpoints answer besides the access decision itself
/// (see <see cref="ItemsControllerAccessTests"/> for 403): 404 for an unknown entry or a title
/// without a video file, and 401 for a call without a credential.
/// </summary>
public class ItemsControllerErrorMappingTests
{
    [Fact]
    public async Task Get_UnknownId_Returns_NotFound()
    {
        var (_, controller, _) = await CreateControllerAsync();

        var result = await controller.Get("moviecollection", 999999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Get_UnknownType_Returns_NotFound()
    {
        var (_, controller, _) = await CreateControllerAsync();

        var result = await controller.Get("keintyp", 1);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Get_WithoutCredential_Returns_Unauthorized()
    {
        var (_, controller, _) = await CreateControllerAsync(authenticated: false);

        var result = await controller.Get("moviecollection", 1);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Stream_UnknownId_Returns_NotFound()
    {
        var (_, controller, _) = await CreateControllerAsync();

        var result = await controller.StreamMediaItem("movie", 999999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Stream_MediaItemWithoutVideoFile_Returns_NotFound()
    {
        var (_, controller, movie) = await CreateControllerAsync();

        var result = await controller.StreamMediaItem("movie", movie.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Stream_WithoutCredential_Returns_Unauthorized()
    {
        var (_, controller, _) = await CreateControllerAsync(authenticated: false);

        var result = await controller.StreamMediaItem("movie", 1);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Download_UnknownId_Returns_NotFound()
    {
        var (_, controller, _) = await CreateControllerAsync();

        var result = await controller.Download("movie", 999999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Download_MediaItemWithoutVideoFile_Returns_NotFound()
    {
        var (_, controller, movie) = await CreateControllerAsync();

        var result = await controller.Download("movie", movie.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Builds a controller with one media source the user has access to and one movie in it that has no
    /// media item (and therefore no video file) attached.
    /// </summary>
    /// <param name="authenticated">Whether the controller runs with a signed-in user.</param>
    /// <returns>The database context, the controller and the movie without a video file.</returns>
    private static async Task<(ApplicationDbContext Db, ItemsController Controller, Movie Movie)> CreateControllerAsync(bool authenticated = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=file:items-error-mapping-{Guid.NewGuid()}?mode=memory&cache=shared";
        var (db, controller, user) = await ItemsControllerTestFactory.CreateAsync(
            connectionString, "error-mapping@test.com", cancellationToken: ct, authenticated: authenticated);

        var source = new MediaSource { Name = "Quelle", Path = "/m", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = source.Id });

        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var movie = new Movie { Name = "Film ohne Datei", MovieCollectionId = collection.Id, MediaSourceId = source.Id };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);

        return (db, controller, movie);
    }
}
