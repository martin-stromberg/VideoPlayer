namespace VideoPlayer.Service.Library.Models.MediaInformation
{
    public class MovieInformation: MediaInformation
    {
        public string Director { get; set; }        
        public string[] Genres { get; set; }

        public string Plot { get; set; }

        public DateTime ReleaseDate { get; set; }

        public DateTime PremieredAt { get; set; }

        public int Year { get; set; }        
    }
}
