
namespace Mediathek.Services.MediaLibrary.Scanner
{
    public interface ILibraryScanner
    {

        void Start();

        void Stop();

        Task WaitForFinish();

        void Rescan(MediaItem item);

        void Rescan(TVShow item);

        void Rescan(MediaElementSource mediaSource, bool all);

        void StartCleaning(MediaElementSource mediaSource);

        void SaveMetaInformation(MediaItem item, MediaInformation metaInfo);

        void TestConnection(MediaElementSource mediaSource);

    }
}
