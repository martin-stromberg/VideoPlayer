using System.Text.RegularExpressions;
using VideoWebPlayer.Components.Shared.Media;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

public class MediaBoxContextMenuInteractionTests
{
    [Fact]
    public void LongPressDelay_IsExactlyOneSecond()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), MediaContextMenuInteractionState.LongPressDelay);
    }

    [Fact]
    public void PointerPress_DoesNotOpenMenuBeforeDelayCompletes()
    {
        var state = new MediaContextMenuInteractionState();

        var cancellationToken = state.BeginPointerPress(
            hasActions: true,
            pointerType: "touch",
            button: 0,
            clientX: 10,
            clientY: 10);

        Assert.NotNull(cancellationToken);
        Assert.False(state.IsMenuOpen);
        Assert.False(cancellationToken.Value.IsCancellationRequested);
    }

    [Fact]
    public void PointerReleaseBeforeDelay_CancelsLongPressWithoutOpeningMenu()
    {
        var state = new MediaContextMenuInteractionState();
        var cancellationToken = state.BeginPointerPress(true, "touch", 0, 10, 10);

        state.EndPointerPress();

        Assert.False(state.IsMenuOpen);
        Assert.True(cancellationToken?.IsCancellationRequested);
        Assert.False(state.SuppressNextClick);
    }

    [Fact]
    public void PointerCancel_ClosesOpenMenuAndClearsClickSuppression()
    {
        var state = new MediaContextMenuInteractionState();
        state.OpenMenu(hasActions: true);

        state.CloseMenu();

        Assert.False(state.IsMenuOpen);
        Assert.False(state.SuppressNextClick);
    }

    [Fact]
    public void MovementOverTolerance_CancelsPendingLongPress()
    {
        var state = new MediaContextMenuInteractionState();
        var cancellationToken = state.BeginPointerPress(true, "touch", 0, 0, 0);

        state.CancelIfPointerMoved(
            MediaContextMenuInteractionState.MovementTolerancePx + 1,
            0);

        Assert.True(cancellationToken?.IsCancellationRequested);
        Assert.False(state.IsMenuOpen);
    }

    [Fact]
    public void EscapeClosePath_ReturnsMenuToClosedState()
    {
        var state = new MediaContextMenuInteractionState();
        state.OpenMenu(hasActions: true);

        var closed = state.CloseMenu();

        Assert.True(closed);
        Assert.False(state.IsMenuOpen);
    }

    [Fact]
    public void ContextMenuHandler_SuppressesNativeMenuButDoesNotOpenActions()
    {
        var mediaBoxSource = ReadRepoFile("VideoWebPlayer", "Components", "Shared", "Media", "MediaBox.razor");
        var handler = Regex.Match(
            mediaBoxSource,
            @"private void HandleContextMenu\(MouseEventArgs args\)(?<body>[\s\S]*?)\n    \}",
            RegexOptions.CultureInvariant);

        Assert.True(handler.Success, "HandleContextMenu method was not found.");
        Assert.Contains("interaction.CancelLongPress();", handler.Groups["body"].Value);
        Assert.DoesNotContain("OpenMenuAsync", handler.Groups["body"].Value);
        Assert.DoesNotContain("OpenMenu(", handler.Groups["body"].Value);
    }

    [Fact]
    public void MediaBox_MenuHasEscapeHandlerAndFocusTargets()
    {
        var mediaBoxSource = ReadRepoFile("VideoWebPlayer", "Components", "Shared", "Media", "MediaBox.razor");

        Assert.Contains("@onkeydown=\"HandleMenuKeyDown\"", mediaBoxSource);
        Assert.Contains("@ref=\"firstActionRef\"", mediaBoxSource);
        Assert.Contains("@ref=\"linkRef\"", mediaBoxSource);
        Assert.Contains("await firstActionRef.FocusAsync();", mediaBoxSource);
        Assert.Contains("await linkRef.FocusAsync();", mediaBoxSource);
    }

    [Fact]
    public void RecentEntriesList_DoesNotPassContextActionsToMediaBox()
    {
        var source = ReadRepoFile("VideoWebPlayer", "Components", "Shared", "Home", "RecentEntriesList.razor");
        var mediaBoxTags = Regex.Matches(source, @"<MediaBox\b[^>]*>", RegexOptions.CultureInvariant);

        Assert.NotEmpty(mediaBoxTags);
        Assert.All(mediaBoxTags.Select(m => m.Value), tag =>
        {
            Assert.DoesNotContain("Actions=", tag);
            Assert.DoesNotContain("OnActionSelected=", tag);
        });
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{Path.Combine(segments)}'.");
    }
}
