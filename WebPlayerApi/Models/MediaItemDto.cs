
namespace WebPlayerApi.Models
{
    public class MediaItemDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public MediaType Type { get; set; }
        public string FilePath { get; set; }
        public List<string> ImagePaths { get; set; }
        public string PictureBase64 { get; set; }
        public string Plot { get; set; }
        public DateTime ReleaseDate { get; set; }
    }
    public class MediaItemDetailsDto : MediaItemDto
    {
        public MediaItemDto[] Children { get; set; }
    }

}
