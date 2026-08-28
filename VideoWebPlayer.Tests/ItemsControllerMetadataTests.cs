using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ItemsControllerMetadataTests
{
    [Fact]
    public async Task UpdateMetadata_WhenUserIsNotAdmin_ReturnsUnauthorized()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, isAdminClaim: false);

        var result = await controller.UpdateMetadata(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = 1,
            Name = "Title",
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task UpdateMetadata_WhenDateFieldDoesNotMatchType_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        db.Movies.Add(new Movie
        {
            Id = 1,
            MediaSourceId = 1,
            Name = "Old Movie",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, isAdminClaim: true);

        var result = await controller.UpdateMetadata(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = 1,
            Name = "Title",
            PremieredAt = new DateTime(2024, 1, 2),
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("PremieredAt", Assert.IsType<string>(badRequest.Value));
    }

    [Fact]
    public async Task UpdateMetadata_WhenMovieCollectionUpdateIsValid_ReturnsOk()
    {
        await using var db = CreateDb();
        db.MovieCollections.Add(new MovieCollection
        {
            Id = 2,
            MediaSourceId = 1,
            Name = "Old Collection",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, isAdminClaim: true);

        var result = await controller.UpdateMetadata(new MediaMetadataUpdateRequest
        {
            ObjectType = "moviecollection",
            Id = 2,
            Name = "New Collection",
            ReleaseDate = new DateTime(2025, 2, 3),
        });

        Assert.IsType<OkObjectResult>(result);
        var collection = await db.MovieCollections.SingleAsync(c => c.Id == 2, TestContext.Current.CancellationToken);
        Assert.Equal("New Collection", collection.Name);
        Assert.Equal(new DateTime(2025, 2, 3), collection.ReleaseDate);
        Assert.True(collection.IsManuallyEdited);
    }

    [Fact]
    public async Task UpdateMetadata_WhenIdIsInvalid_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, isAdminClaim: true);

        var result = await controller.UpdateMetadata(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = 0,
            Name = "Title",
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("ID", Assert.IsType<string>(badRequest.Value));
    }

    [Fact]
    public async Task UpdateMetadata_WhenNameIsTooLong_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, isAdminClaim: true);

        var result = await controller.UpdateMetadata(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = 1,
            Name = new string('x', 513),
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("512", Assert.IsType<string>(badRequest.Value));
    }

    private static ItemsController CreateController(ApplicationDbContext db, bool isAdminClaim)
    {
        var user = new ApplicationUser { Id = "user-1", UserName = "tester" };
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.CurrentUser).Returns(user);
        var unlockedMediaService = new Mock<IUnlockedMediaService>();
        unlockedMediaService.Setup(x => x.GetUnlockedMovieCollectionIdsForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<long>());
        unlockedMediaService.Setup(x => x.GetUnlockedTVShowIdsForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<long>());
        unlockedMediaService.Setup(x => x.IsUnlockedAsync(It.IsAny<DtoMediaEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var controller = new ItemsController(
            db,
            new SftpMediaSourceReader(),
            new MediaMetadataEditorService(db),
            new RecentEntryService(db, authService.Object, unlockedMediaService.Object),
            unlockedMediaService.Object,
            authService.Object,
            NullLogger<ItemsController>.Instance);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id) };
        if (isAdminClaim)
            claims.Add(new Claim("IsAdmin", "True"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            },
        };
        return controller;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"items-controller-metadata-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options, new EventManager());
    }
}
