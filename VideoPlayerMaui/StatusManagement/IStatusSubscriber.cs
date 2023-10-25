namespace VideoPlayer.StatusManagement
{
    public interface IStatusSubscriber
    {

        event EventHandler<StatusEventArgs> StatusChanged;

    }
}
