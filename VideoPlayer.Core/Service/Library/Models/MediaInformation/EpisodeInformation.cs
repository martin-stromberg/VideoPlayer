
namespace VideoPlayer.Service.Library.Models.MediaInformation
{
    public class EpisodeInformation: MediaInformation
    {

        public string ShowName { get; set; }

        public int Season { get; set; }

        public int Episode { get; set; }

        public string Plot { get; set; }

        public string Part { get; set; }
        public DateTime AiredAt { get; set; }
    }
}
