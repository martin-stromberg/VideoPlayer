using VideoPlayer.Service.BaseServices;

namespace VideoPlayer.Service.Library.Scanner
{
    public interface ILibraryScanner : ITimerService
    {
        void ForceScanAll();
    }
}
