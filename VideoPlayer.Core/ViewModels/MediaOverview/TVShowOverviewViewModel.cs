using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.ViewModels.MediaOverview.Genres;

namespace VideoPlayer.ViewModels.MediaOverview
{
    public class TVShowOverviewViewModel : BaseMediaOverviewViewModel
    {
        public TVShowOverviewViewModel(IMediaLibrary mediaLibrary, GenreSelectionViewModel genreSelectionViewModel, INavigationManager navigationManager) 
            : base(genreSelectionViewModel, new EntryType[] { EntryType.TVShow, EntryType.TVShowCollection }, mediaLibrary, navigationManager)
        {
        }
        protected override bool CheckViewGenre(Genre genre)
        {
            return genre.HasTVShow && base.CheckViewGenre(genre);
        }
    }
}
