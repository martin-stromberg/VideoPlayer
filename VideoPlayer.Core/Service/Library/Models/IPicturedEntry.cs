namespace VideoPlayer.Service.Library.Models
{
    public interface IPicturedEntry
    {
        string PicturePath { get; }
        string PictureBackgroundColor { get; }
        string BannerPath { get; }
        string BannerBackgroundColor { get; }
    }
}
