namespace Mediathek.StatusManagement
{
    public interface IStatusPublisher
    {

        long AddStatus(string message, bool direct);

        void Clear(long id);

    }
}
