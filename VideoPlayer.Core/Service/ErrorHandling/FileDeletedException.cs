namespace VideoPlayer.Service.ErrorHandling
{
    public class FileDeletedException : ApplicationException
    {
        public FileDeletedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
