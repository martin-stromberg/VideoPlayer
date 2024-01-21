using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.TVShows
{
    [DataModelReference(typeof(Services.Database.Models.TVShowCollection))]
    public class TVShowCollection: BaseModel
    {

        public ImageSource Picture { get; set; }

    }
}
