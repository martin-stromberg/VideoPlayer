
namespace WebPlayerApi.Service.Data.SFtp
{
    [Serializable]
    internal class FileDeletedException : Exception
    {
        public FileDeletedException()
        {
        }

        public FileDeletedException(string? message) : base(message)
        {
        }

        public FileDeletedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}