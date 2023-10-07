using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.MovieCollection))]
    public class MovieCollection: BaseModel
    {

    }
}
