namespace VideoPlayer.Services.Database.Models
{
    public class TVShowEpisode: BaseDataModel
    {

        public string ShowName { get; set; }

        public long SeasonId { get; set; }

        public string SeasonName { get; set; }

        public string EpisodeNo { get; set; }
        public string Part { get; set; }

        public string Plot { get; set; }

        public string PicturePath { get; set; }

        public long PrimaryMediaItemId { get; set; }

        public long DownloadMediaItemId { get; set; }

    }
}
