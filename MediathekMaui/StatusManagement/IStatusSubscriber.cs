namespace Mediathek.StatusManagement
{
    public interface IStatusSubscriber
    {

        event EventHandler<StatusEventArgs> StatusChanged;

    }
}
