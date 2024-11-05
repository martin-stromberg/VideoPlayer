
namespace VideoPlayer.Service.Library.Models.MediaInformation
{
    public class TVShowInformation : MediaInformation
    {
        public string Plot { get; set; }
        public DateTime PremieredAt { get; set; }
        public string[] Genres { get; set; }
    }
}
