namespace VideoPlayer.StatusManagement
{
    public class StatusEventArgs: EventArgs
    {

        public StatusEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; }

    }
}
