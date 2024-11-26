namespace VideoPlayer.Service.Download
{
    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception error)
            :base()
        {
            Error = error;
        }

        public Exception Error { get; }
    }
}
