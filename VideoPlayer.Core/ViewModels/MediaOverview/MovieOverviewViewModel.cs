using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.Genres;

namespace VideoPlayer.ViewModels.MediaOverview
{
    
    public class MovieOverviewViewModel
        : BaseMediaOverviewViewModel
    {
        public MovieOverviewViewModel(
            IMediaLibrary mediaLibrary, 
            GenreSelectionViewModel genreSelectionViewModel, 
            INavigationManager navigationManager, IResourceManager resourceManager) 
            : base(genreSelectionViewModel, new EntryType[] { EntryType.Movie, EntryType.MovieCollection }, mediaLibrary, navigationManager, resourceManager)
        {
        }
        protected override bool CheckViewGenre(Genre genre)
        {
            return genre.HasMovies && base.CheckViewGenre(genre);
        }

    }
}
