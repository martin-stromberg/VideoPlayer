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
        }

        public GenreName[] AlternateNames { get => GetProperty<GenreName[]>(); set => SetProperty(value); }

        internal static Genre Create(string name)
        {
            return new Genre(null)
            {
                Name = name
            };
        }
    }
}
