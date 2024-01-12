namespace Mediathek.Models
{
    public class CallbackBaseModelEventArgs: BaseModelEventArgs
    {

        public CallbackBaseModelEventArgs(BaseModel modelObj)
            : base(modelObj) { }

        public event EventHandler<BaseModelEventArgs> Callback;

        public void SendCallback(MediaItem mediaItem)
        {
            Callback.Invoke(this, new BaseModelEventArgs(mediaItem));
        }

    }
}
