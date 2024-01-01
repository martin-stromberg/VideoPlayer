#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif

using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Models.Sources;

namespace VideoPlayer.Services.MediaLibrary.Scanner
{
    public interface ILibraryScanner
    {

        void Start();

        void Stop();

        Task WaitForFinish();

        void Rescan(MediaItem item);

        void Rescan(MediaSource mediaSource);

        void StartCleaning(MediaSource mediaSource);

        void SaveMetaInformation(MediaItem item, MediaInformation metaInfo);

        void TestConnection(MediaSource mediaSource);

    }
}
