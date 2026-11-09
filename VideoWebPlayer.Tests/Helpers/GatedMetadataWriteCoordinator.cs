using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// <see cref="IMediaMetadataWriteCoordinator"/>, das den Eintritt in die Schreibsperre zurückhält, bis der
/// Test <see cref="Release"/> ruft. Damit lässt sich ein laufender <see cref="MediaSourceClassifier"/>-Aufruf
/// (und damit die statische Klassifizierungssperre, die er belegt) deterministisch über den gewünschten
/// Zeitraum offen halten, statt sich auf zeitliche Zufälle zu verlassen.
/// </summary>
internal sealed class GatedMetadataWriteCoordinator : IMediaMetadataWriteCoordinator
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _hold;

    /// <summary>
    /// Hält künftige Eintritte zurück, bis <see cref="Release"/> gerufen wird.
    /// </summary>
    public void Hold()
    {
        Volatile.Write(ref _hold, true);
    }

    /// <summary>
    /// Gibt einen zurückgehaltenen Eintritt frei.
    /// </summary>
    public void Release() => _release.TrySetResult();

    /// <summary>
    /// Wartet, bis ein Aufrufer in die (zurückgehaltene) Schreibsperre eingetreten ist.
    /// </summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Eine Aufgabe, die abgeschlossen ist, sobald der Eintritt erfolgt ist.</returns>
    public Task WaitUntilEnteredAsync(CancellationToken cancellationToken) => _entered.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _hold))
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }

        return new NoopLease();
    }

    private sealed class NoopLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
