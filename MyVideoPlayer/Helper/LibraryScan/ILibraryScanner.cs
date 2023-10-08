#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif
namespace MyVideoPlayer.Helper.LibraryScan
{
    public interface ILibraryScanner
    {
        event EventHandler<MessageEventArgs> StatusChanged;
        void Start();
        void Stop();
    }
}
