namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    public class BaseModelEventArgs: EventArgs
    {
        public BaseModelEventArgs(BaseModel modelObj) 
        {
            Element = modelObj;
        }

        public BaseModel Element { get; }
    }

    public class CallbackBaseModelEventArgs : BaseModelEventArgs
    {
        public CallbackBaseModelEventArgs(BaseModel modelObj) 
            : base(modelObj)
        {
        }

        public event EventHandler<BaseModelEventArgs> Callback;

        public void SendCallback(MediaItem mediaItem)
        {
            Callback.Invoke(this, new BaseModelEventArgs(mediaItem));
        }
    }
}
