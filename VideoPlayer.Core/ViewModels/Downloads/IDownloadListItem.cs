namespace VideoPlayer.ViewModels.Downloads
{
    public interface IDownloadListItem
    {
        void ExecuteDelete();
        event EventHandler DeleteRequested;
    }
}
