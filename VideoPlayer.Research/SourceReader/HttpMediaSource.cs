using Newtonsoft.Json;

namespace VideoPlayer.Service.Library.Models
{
    public class HttpMediaSource: MediaSource
    {

        public HttpMediaSource()
            : base()
        {
            
        }

        public string Uri
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
            }
        }


    }
}
