namespace VideoPlayer.Service.ErrorHandling
{
    public interface IErrorLogManager
    {
        bool HasErrors { get; }

        IEnumerable<string> ReadErrors();
    }
}
