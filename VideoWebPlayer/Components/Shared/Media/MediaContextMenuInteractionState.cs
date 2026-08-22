namespace VideoWebPlayer.Components.Shared.Media;

internal sealed class MediaContextMenuInteractionState
{
    public static readonly TimeSpan LongPressDelay = TimeSpan.FromSeconds(3);
    public const double MovementTolerancePx = 10;

    private CancellationTokenSource? longPressCts;
    private double pointerStartX;
    private double pointerStartY;

    public bool IsMenuOpen { get; private set; }
    public bool SuppressNextClick { get; private set; }

    public CancellationToken? BeginPointerPress(bool hasActions, string pointerType, long button, double clientX, double clientY)
    {
        if (!hasActions || (pointerType == "mouse" && button != 0))
            return null;

        CancelLongPress();
        pointerStartX = clientX;
        pointerStartY = clientY;
        longPressCts = new CancellationTokenSource();
        return longPressCts.Token;
    }

    public void CancelIfPointerMoved(double clientX, double clientY)
    {
        if (longPressCts is null || longPressCts.IsCancellationRequested)
            return;

        var deltaX = clientX - pointerStartX;
        var deltaY = clientY - pointerStartY;
        if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) > MovementTolerancePx)
            CancelLongPress();
    }

    public void EndPointerPress()
    {
        if (IsMenuOpen)
            SuppressNextClick = true;

        CancelLongPress();
    }

    public bool OpenMenu(bool hasActions)
    {
        if (!hasActions)
            return false;

        CancelLongPress();
        SuppressNextClick = true;
        IsMenuOpen = true;
        return true;
    }

    public bool CloseMenu()
    {
        CancelLongPress();
        SuppressNextClick = false;

        if (!IsMenuOpen)
            return false;

        IsMenuOpen = false;
        return true;
    }

    public void ConsumeClickSuppression()
    {
        if (SuppressNextClick)
            SuppressNextClick = false;
    }

    public void CancelLongPress()
    {
        if (longPressCts is null)
            return;

        longPressCts.Cancel();
        longPressCts.Dispose();
        longPressCts = null;
    }
}
