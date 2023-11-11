#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif

using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.MediaLibrary.Scanner
{
    public interface ILibraryScanner
    {

        void Start();

        void Stop();

        Task WaitForFinish();

        void Rescan(MediaItem item);

    }
}
