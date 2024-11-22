using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.Database.Models
{
    public class DataGenre: BaseDataModel
    {
        public bool HasMovies { get => GetProperty<bool>(); set => SetProperty(value); }
        public bool HasTVShow { get => GetProperty<bool>(); set => SetProperty(value); }
    }
    public class DataGenreName : BaseDataModel
    {
        public long DataGenreId { get; set; }        
    }
}
