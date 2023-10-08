#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif

namespace MyVideoPlayer.Helper.LibraryScan
{
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string message)
            : base()
        {
            Message = message;
        }

        public string Message { get; }
    }
}
