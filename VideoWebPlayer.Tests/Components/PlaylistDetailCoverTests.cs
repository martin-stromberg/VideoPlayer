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
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests for the cover display of <see cref="PlaylistDetail"/> and its cover panel
/// (<see cref="PlaylistCoverPanel"/>): one image button replaces "Bild hochladen" and "Cover neu erzeugen";
/// the panel offers file upload and generation, shows both as a not-yet-saved preview, confirms with
/// "Hochladen" (file) or "Anwenden" (generated image) and can remove the existing image (Kundenrückmeldung zur
/// Detailansicht, D3).
/// </summary>
public class PlaylistDetailCoverTests
{
    [Fact]
    public void PlaylistDetail_WithCoverPictureId_ShowsCoverImage()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 42);

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);

        var image = Assert.Single(cut.FindAll("#playlist-detail-cover-image"));
        Assert.Contains("/api/playlists/1/cover", image.GetAttribute("src"));
    }

    [Fact]
    public void PlaylistDetail_WithoutCoverPictureId_DoesNotRenderCoverImage()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);

        Assert.Empty(cut.FindAll("#playlist-detail-cover-image"));
        Assert.NotEmpty(cut.FindAll(".playlist-header-cover"));
    }

    [Fact]
    public void PlaylistDetail_HasOneImageButton_ReplacingTheUploadAndRegenerateButtons()
    {
        using var ctx = CreateTestContext(CreatePlaylistClientMock(coverPictureId: null));
        var cut = RenderDetail(ctx);

        var button = Assert.Single(cut.FindAll("#playlist-detail-cover-button"));
        Assert.False(string.IsNullOrWhiteSpace(button.GetAttribute("title")));
        Assert.Equal(button.GetAttribute("title"), button.GetAttribute("aria-label"));
        Assert.NotNull(button.QuerySelector("svg"));
        Assert.Empty(cut.FindAll("#playlist-detail-upload-cover-button"));
        Assert.Empty(cut.FindAll("#playlist-detail-regenerate-cover-button"));
    }

    [Fact]
    public void PlaylistDetail_ClickImageButton_OpensCoverPanel()
    {
        using var ctx = CreateTestContext(CreatePlaylistClientMock(coverPictureId: null));
        var cut = RenderDetail(ctx);
        Assert.Empty(cut.FindComponents<PlaylistCoverPanel>());

        cut.Find("#playlist-detail-cover-button").Click();

        var panel = Assert.Single(cut.FindComponents<PlaylistCoverPanel>());
        Assert.NotEmpty(panel.FindAll("#playlist-cover-upload-input"));
        Assert.NotEmpty(panel.FindAll("#playlist-cover-generate-button"));
        Assert.NotEmpty(panel.FindAll("#playlist-cover-empty"));
        // Framed overlay panel (the frame itself comes from the shared .admin-dialog CSS).
        Assert.Contains("admin-dialog", panel.Find("#playlist-cover-panel").ClassList);
        // Nothing staged yet: the confirm button cannot be used, and there is no "remove" without an image.
        Assert.True(panel.Find("#playlist-cover-apply-button").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll("#playlist-cover-remove-button"));
    }

    [Fact]
    public async Task CoverPanel_SelectFile_ShowsPreview_ButtonSaysHochladen_UploadCallsApiAndClosesPanel()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        playlistClientMock
            .Setup(c => c.UploadPlaylistCoverAsync(1, It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true, Message = "Bild erfolgreich hochgeladen.", PictureId = 7 });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        // bUnit has no way to set the ContentType of an InputFileContent, so this exercises the same
        // "no reported content type" path a real browser that omits it would take - the panel must not reject
        // the selection on that basis, only the server-side PlaylistCoverValidator is authoritative.
        var content = InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3, 4 }, "cover.jpg");
        await panel.InvokeAsync(() => panel.FindComponent<InputFile>().UploadFiles(content));

        Assert.Single(panel.FindAll("#playlist-cover-preview"));
        Assert.Single(panel.FindAll("#playlist-cover-upload-filename"));
        Assert.Empty(panel.FindAll("#playlist-cover-error"));
        Assert.Equal("Hochladen", panel.Find("#playlist-cover-apply-button").TextContent.Trim());
        Assert.Contains("noch nicht gespeichert", panel.Find("#playlist-cover-preview-caption").TextContent);

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-apply-button").Click());

        playlistClientMock.Verify(c => c.UploadPlaylistCoverAsync(1, It.IsAny<byte[]>(), "cover.jpg", It.IsAny<string>()), Times.Once);
        Assert.Empty(cut.FindComponents<PlaylistCoverPanel>());
    }

    [Fact]
    public async Task CoverPanel_Upload_ServerValidationError_IsShownInPanelAndPanelStaysOpen()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        playlistClientMock
            .Setup(c => c.UploadPlaylistCoverAsync(1, It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Die Datei ist kein gültiges Bild."));

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();
        await panel.InvokeAsync(() => panel.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3 }, "kaputt.jpg")));

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-apply-button").Click());

        Assert.Contains("kein gültiges Bild", panel.Find("#playlist-cover-error").TextContent);
        Assert.Single(cut.FindComponents<PlaylistCoverPanel>());
    }

    [Fact]
    public async Task CoverPanel_Generate_ShowsPreview_ButtonSaysAnwenden_AndSavesNothing()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        ArrangePreview(playlistClientMock);

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());

        var preview = Assert.Single(panel.FindAll("#playlist-cover-preview"));
        Assert.StartsWith("data:image/jpeg;base64,", preview.GetAttribute("src"));
        Assert.Equal("Anwenden", panel.Find("#playlist-cover-apply-button").TextContent.Trim());
        Assert.Contains("noch nicht gespeichert", panel.Find("#playlist-cover-preview-caption").TextContent);
        playlistClientMock.Verify(c => c.PreviewPlaylistCoverAsync(1), Times.Once);
        // The preview is NOT saved: neither regenerate nor upload was called.
        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(It.IsAny<long>(), It.IsAny<bool>()), Times.Never);
        playlistClientMock.Verify(c => c.UploadPlaylistCoverAsync(It.IsAny<long>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CoverPanel_Generate_NoImagesAvailable_ShowsMessageInPanel()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        playlistClientMock
            .Setup(c => c.PreviewPlaylistCoverAsync(1))
            .ReturnsAsync(new DtoPlaylistCoverPreview { Success = false, Message = "Keine Bilder verfügbar." });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());

        Assert.Contains("Keine Bilder verfügbar", panel.Find("#playlist-cover-error").TextContent);
        Assert.Empty(panel.FindAll("#playlist-cover-preview"));
        Assert.True(panel.Find("#playlist-cover-apply-button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task CoverPanel_Anwenden_AppliesPreviewViaRegenerate_WithoutConfirmationWhenCoverNotUploaded_AndClosesPanel()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 5, uploaded: false);
        ArrangePreview(playlistClientMock);
        playlistClientMock
            .Setup(c => c.RegeneratePlaylistCoverAsync(1, false))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true, PictureId = 99 });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();
        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());
        Assert.Empty(panel.FindAll("#playlist-cover-replace-warning"));

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-apply-button").Click());

        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(1, false), Times.Once);
        Assert.Empty(cut.FindComponents<PlaylistCoverPanel>());
        // The playlist is refreshed quietly (initial load + one refresh) so the header picks up the new cover.
        playlistClientMock.Verify(c => c.RequestPlaylistAsync(1), Times.Exactly(2));
    }

    /// <summary>
    /// The 409 protection of Schritt 10 stays: replacing an UPLOADED image is announced in the panel before, and
    /// the explicit click on "Anwenden" is the confirmation the server requires.
    /// </summary>
    [Fact]
    public async Task CoverPanel_Anwenden_ReplacingUploadedCover_ShowsWarningFirst_AndSendsConfirmation()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 5, uploaded: true);
        ArrangePreview(playlistClientMock);
        playlistClientMock
            .Setup(c => c.RegeneratePlaylistCoverAsync(1, true))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true, PictureId = 99 });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();
        Assert.Empty(panel.FindAll("#playlist-cover-replace-warning"));

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());

        Assert.Contains("hochgeladene Bild wird ersetzt", panel.Find("#playlist-cover-replace-warning").TextContent);
        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(It.IsAny<long>(), It.IsAny<bool>()), Times.Never);

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-apply-button").Click());

        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(1, true), Times.Once);
        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(1, false), Times.Never);
    }

    [Fact]
    public async Task CoverPanel_Anwenden_ServerStillAsksForConfirmation_ShowsHintAndSecondClickConfirms()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 5, uploaded: false);
        ArrangePreview(playlistClientMock);
        playlistClientMock
            .Setup(c => c.RegeneratePlaylistCoverAsync(1, false))
            .ThrowsAsync(new HttpRequestException("Conflict", null, System.Net.HttpStatusCode.Conflict));
        playlistClientMock
            .Setup(c => c.RegeneratePlaylistCoverAsync(1, true))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true, PictureId = 99 });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();
        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-apply-button").Click());

        Assert.Contains("hochgeladenes Bild", panel.Find("#playlist-cover-error").TextContent);
        Assert.Single(cut.FindComponents<PlaylistCoverPanel>());
        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(1, true), Times.Never);

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-apply-button").Click());

        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(1, true), Times.Once);
    }

    [Fact]
    public async Task CoverPanel_GeneratedPreview_And_FileSelection_ReplaceEachOther()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);
        ArrangePreview(playlistClientMock);

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());
        Assert.Equal("Anwenden", panel.Find("#playlist-cover-apply-button").TextContent.Trim());

        await panel.InvokeAsync(() => panel.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3 }, "a.jpg")));
        Assert.Equal("Hochladen", panel.Find("#playlist-cover-apply-button").TextContent.Trim());
        Assert.Single(panel.FindAll("#playlist-cover-upload-filename"));

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-generate-button").Click());
        Assert.Equal("Anwenden", panel.Find("#playlist-cover-apply-button").TextContent.Trim());
        Assert.Empty(panel.FindAll("#playlist-cover-upload-filename"));
    }

    [Fact]
    public void CoverPanel_ExistingCover_IsShownAsCurrentImage_WithRemoveButton()
    {
        using var ctx = CreateTestContext(CreatePlaylistClientMock(coverPictureId: 42, uploaded: true));
        var cut = RenderDetail(ctx);

        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        Assert.Contains("/api/playlists/1/cover", panel.Find("#playlist-cover-current").GetAttribute("src"));
        Assert.Single(panel.FindAll("#playlist-cover-remove-button"));
        Assert.Contains("Aktuelles Bild", panel.Find("#playlist-cover-preview-caption").TextContent);
    }

    [Fact]
    public async Task CoverPanel_RemoveGeneratedCover_DeletesWithoutConfirmation()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 42, uploaded: false);
        playlistClientMock
            .Setup(c => c.DeletePlaylistCoverAsync(1))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-remove-button").Click());

        // A merely generated image can be re-created at any time: no question asked.
        Assert.Empty(cut.FindComponents<PlaylistCoverRemoveConfirmationDialog>());
        playlistClientMock.Verify(c => c.DeletePlaylistCoverAsync(1), Times.Once);
        Assert.Empty(cut.FindComponents<PlaylistCoverPanel>());
    }

    [Fact]
    public async Task CoverPanel_RemoveUploadedCover_AsksFirst_ConfirmDeletes()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 42, uploaded: true);
        playlistClientMock
            .Setup(c => c.DeletePlaylistCoverAsync(1))
            .ReturnsAsync(new DtoPlaylistCoverResult { Success = true });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-remove-button").Click());

        Assert.Single(cut.FindComponents<PlaylistCoverRemoveConfirmationDialog>());
        playlistClientMock.Verify(c => c.DeletePlaylistCoverAsync(It.IsAny<long>()), Times.Never);

        await cut.InvokeAsync(() => cut.Find("#confirm-remove-cover-button").Click());

        playlistClientMock.Verify(c => c.DeletePlaylistCoverAsync(1), Times.Once);
        Assert.Empty(cut.FindComponents<PlaylistCoverPanel>());
    }

    [Fact]
    public async Task CoverPanel_RemoveUploadedCover_CancelKeepsTheImage()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: 42, uploaded: true);

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();
        await panel.InvokeAsync(() => panel.Find("#playlist-cover-remove-button").Click());

        await cut.InvokeAsync(() => cut.Find("#cancel-remove-cover-button").Click());

        playlistClientMock.Verify(c => c.DeletePlaylistCoverAsync(It.IsAny<long>()), Times.Never);
        Assert.Empty(cut.FindComponents<PlaylistCoverRemoveConfirmationDialog>());
        Assert.Single(cut.FindComponents<PlaylistCoverPanel>());
    }

    [Fact]
    public async Task CoverPanel_Cancel_ClosesWithoutAnyApiCall()
    {
        var playlistClientMock = CreatePlaylistClientMock(coverPictureId: null);

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = RenderDetail(ctx);
        cut.Find("#playlist-detail-cover-button").Click();
        var panel = cut.FindComponent<PlaylistCoverPanel>();

        await panel.InvokeAsync(() => panel.Find("#playlist-cover-cancel-button").Click());

        Assert.Empty(cut.FindComponents<PlaylistCoverPanel>());
        playlistClientMock.Verify(c => c.PreviewPlaylistCoverAsync(It.IsAny<long>()), Times.Never);
        playlistClientMock.Verify(c => c.UploadPlaylistCoverAsync(It.IsAny<long>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        playlistClientMock.Verify(c => c.RegeneratePlaylistCoverAsync(It.IsAny<long>(), It.IsAny<bool>()), Times.Never);
        playlistClientMock.Verify(c => c.DeletePlaylistCoverAsync(It.IsAny<long>()), Times.Never);
    }

    private static void ArrangePreview(Mock<IPlaylistApiClient> playlistClientMock)
        => playlistClientMock
            .Setup(c => c.PreviewPlaylistCoverAsync(1))
            .ReturnsAsync(new DtoPlaylistCoverPreview { Success = true, ContentType = "image/jpeg", ImageData = new byte[] { 1, 2, 3, 4 } });

    private static IRenderedComponent<PlaylistDetail> RenderDetail(global::Bunit.BunitContext ctx)
    {
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        return ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));
    }

    private static Mock<IPlaylistApiClient> CreatePlaylistClientMock(long? coverPictureId, bool? uploaded = null)
    {
        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistAsync(1))
            .ReturnsAsync(new DtoPlaylist
            {
                Id = 1,
                Name = "Test-Playlist",
                IsOwner = true,
                SortMode = PlaylistSortModeValues.ByReleaseDate,
                CoverPictureId = coverPictureId,
                CoverPictureIsUserUploaded = uploaded ?? coverPictureId.HasValue
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
