namespace Mediathek.Services.Playlists
{
    public class ProgressEventArgs: EventArgs
    {

        public ProgressEventArgs(float progress)
        {
            Progress = progress;
        }

        public float Progress { get; }

    }
}
