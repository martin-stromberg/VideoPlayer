using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataGenre))]
    public class Genre : BaseServiceModel
    {
        public Genre(DataGenre dataModel) 
            : base(dataModel)
        {
            AlternateNames = new GenreName[0];
            if (DataModel is not null)
            {
                HasMovies = ((DataGenre)DataModel).HasMovies;
                HasTVShow = ((DataGenre)DataModel).HasTVShow;
            }
        }

        public GenreName[] AlternateNames { get => GetProperty<GenreName[]>(); set => SetProperty(value); }
        public bool HasMovies { get => GetProperty<bool>(); set => SetProperty(value); }
        public bool HasTVShow { get => GetProperty<bool>(); set => SetProperty(value); }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataGenre)DataModel).HasMovies = HasMovies;
                ((DataGenre)DataModel).HasTVShow = HasTVShow;
            }
        }
        public override string ToString()
        {
            return $"{Name}";
        }
        internal static Genre Create(string name)
        {
            return new Genre(null)
            {
                Name = name
            };
        }
    }
}
