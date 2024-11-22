using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Download
{
    public interface IEnvironment
    {
        string GetRootPath();
        string GetPath(MediaItemCopyType copyType);
    }
}
