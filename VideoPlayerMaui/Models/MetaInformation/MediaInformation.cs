using Newtonsoft.Json;

namespace VideoPlayer.Models.MetaInformation
{
    public class MediaInformation
    {

        public string Title { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
}
