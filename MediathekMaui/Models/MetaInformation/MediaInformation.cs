using Newtonsoft.Json;

namespace Mediathek.Models.MetaInformation
{
    public class MediaInformation
    {

        public string Title { get; set; }

        public DateTime LastUpdate { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
}
