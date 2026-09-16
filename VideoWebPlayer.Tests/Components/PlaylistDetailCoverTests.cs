using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Components.Shared.Media;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests for <see cref="PlaylistDetail"/>'s cover display, upload dialog and "Neu erzeugen" action
/// (Entwicklungsschritt 10, Playlist-Abbildungen).
/// </summary>
public class PlaylistDetailCoverTests
{
    [Fact]
    public void PlaylistDetail_WithCoverPictureId_ShowsCoverImage()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 42);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        var image = Assert.Single(cut.FindAll("#playlist-detail-cover-image"));
        Assert.Contains("/api/playlists/1/cover", image.GetAttribute("src"));
    }

    [Fact]
    public void PlaylistDetail_WithoutCoverPictureId_DoesNotRenderCoverImage()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        Assert.Empty(cut.FindAll("#playlist-detail-cover-image"));
        Assert.NotEmpty(cut.FindAll(".playlist-header-cover"));
    }

    [Fact]
    public void PlaylistDetail_RegenerateButton_Visible()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        Assert.Single(cut.FindAll("#playlist-detail-regenerate-cover-button"));
    }

    [Fact]
    public async Task PlaylistDetail_ClickRegenerateButton_CallsApiAndReloadsPlaylist()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        playlistClientMock
            .Setup(c => c.RegeneratePlaylistCoverAsync(1))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true, Message = "Cover neu erzeugt.", PictureId = 99 });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        await cut.InvokeAsync(() => cut.Find("#playlist-detail-regenerate-cover-button").Click());

        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(1), Times.Once);
        // RequestPlaylistAsync is called once for the initial load, once more after regeneration succeeds.
        playlistClientMock.Verify(c => c.RequestPlaylistAsync(1), Times.Exactly(2));
    }

    [Fact]
    public async Task PlaylistDetail_ClickRegenerateButton_NoImagesAvailable_ShowsMessage()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        playlistClientMock
            .Setup(c => c.RegeneratePlaylistCoverAsync(1))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = false, Message = "Keine Bilder verfuegbar." });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        await cut.InvokeAsync(() => cut.Find("#playlist-detail-regenerate-cover-button").Click());

        var status = Assert.Single(cut.FindAll("#playlist-cover-status"));
        Assert.Equal("Keine Bilder verfuegbar.", status.TextContent);
    }

    [Fact]
    public void PlaylistDetail_ClickUploadCoverButton_OpensUploadDialog()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        Assert.Empty(cut.FindComponents<PlaylistCoverUploadDialog>());

        cut.Find("#playlist-detail-upload-cover-button").Click();

        Assert.Single(cut.FindComponents<PlaylistCoverUploadDialog>());
    }

    [Fact]
    public async Task PlaylistDetail_UploadDialog_SelectFileAndUpload_CallsApiAndClosesDialog()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        playlistClientMock
            .Setup(c => c.UploadPlaylistCoverAsync(1, It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true, Message = "Bild erfolgreich hochgeladen.", PictureId = 7 });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        cut.Find("#playlist-detail-upload-cover-button").Click();
        var dialog = cut.FindComponent<PlaylistCoverUploadDialog>();

        var inputFile = dialog.FindComponent<InputFile>();
        // bUnit's InputFileContent has no way to set IBrowserFile.ContentType, so this exercises the same
        // "no reported content type" path a real browser that omits it would take - PlaylistCoverUploadDialog
        // must not reject the selection on that basis (see the "empty ContentType is not rejected" guard in
        // OnFileSelectedAsync), only the server-side PlaylistCoverValidator is authoritative for format checks.
        var content = InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3, 4 }, "cover.jpg");
        await dialog.InvokeAsync(() => inputFile.UploadFiles(content));

        Assert.Single(dialog.FindAll("#playlist-cover-upload-filename"));
        Assert.Empty(dialog.FindAll("#playlist-cover-upload-error"));

        await dialog.InvokeAsync(() => dialog.Find("#playlist-cover-upload-button").Click());

        playlistClientMock.Verify(c => c.UploadPlaylistCoverAsync(1, It.IsAny<byte[]>(), "cover.jpg", It.IsAny<string>()), Times.Once);
        Assert.Empty(cut.FindComponents<PlaylistCoverUploadDialog>());
    }

    private static Mock<IPlaylistApiClient> CreatePlaylistClientMock(long? coverPictureId)
    {
        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistAsync(1))
            .ReturnsAsync(new DtoPlaylist
            {
                Id = 1,
                Name = "Test-Playlist",
                SortMode = PlaylistSortModeValues.ByReleaseDate,
                CoverPictureId = coverPictureId,
                CoverPictureIsUserUploaded = coverPictureId.HasValue
            });
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        return playlistClientMock;
    }

    private static global::Bunit.BunitContext CreateTestContext(Mock<IPlaylistApiClient> playlistClientMock)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton(NullLogger<PlaylistDetail>.Instance);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
        ctx.Services.AddSingleton<IOptions<PlaylistSettings>>(Options.Create(new PlaylistSettings()));
        return ctx;
    }
}
