namespace VideoPlayer.Services.Database.Models
{
    public class TVShowEpisode: BaseDataModel
    {

        public string ShowName { get; set; }

        public long SeasonId { get; set; }

        public string SeasonName { get; set; }

        public string EpisodeNo { get; set; }

    }
}
