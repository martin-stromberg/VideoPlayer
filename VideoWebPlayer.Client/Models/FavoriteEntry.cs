using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    public class DtoFavoriteEntry
    {
        public long Id { get; set; }
        public DtoMediaEntry Entry { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

    }
}