#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif

namespace VideoPlayer.Services.MediaLibrary.Scanner
{
    public interface ILibraryScanner
    {

        void Start();

        void Stop();

        Task WaitForFinish();

    }
}
