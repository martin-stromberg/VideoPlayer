using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Tenants;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.Genres;

namespace VideoPlayer.ViewModels.MediaOverview
{
    public class TVShowOverviewViewModel : BaseMediaOverviewViewModel
    {
        public TVShowOverviewViewModel(IMediaLibrary mediaLibrary,
            GenreSelectionViewModel genreSelectionViewModel, 
            INavigationManager navigationManager,
            IProcessorCollection processorCollection,
            IResourceManager resourceManager,
            ITenantSelection tenantSelection,
            ILogger<TVShowOverviewViewModel> logger) 
            : base(genreSelectionViewModel, 
                  new EntryType[] { EntryType.TVShow, EntryType.TVShowCollection }, 
                  mediaLibrary, 
                  navigationManager, 
                  processorCollection,
                  resourceManager, tenantSelection, logger)
        {
        }
        protected override bool CheckViewGenre(Genre genre)
        {
            return genre.HasTVShow && base.CheckViewGenre(genre);
        }
    }
}
