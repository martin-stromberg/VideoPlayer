namespace VideoPlayer.Service.Library.Models.Classified
{
    public interface IPlayableEntry
    {
        string[] Genres { get; }
        string Plot { get; }
        DateTime ReleaseDate { get; }
        DateTime PremieredAt { get; }
    }
}
