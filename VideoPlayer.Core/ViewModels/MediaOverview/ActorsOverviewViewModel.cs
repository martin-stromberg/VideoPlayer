using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.Genres;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview
{
    public class ActorsOverviewViewModel: BaseMediaOverviewViewModel
    {
        public ActorsOverviewViewModel(
            IMediaLibrary mediaLibrary,
            GenreSelectionViewModel genreSelectionViewModel,
            INavigationManager navigationManager, 
            IProcessorCollection processorCollection,
            IResourceManager resourceManager,
            ILogger<ActorsOverviewViewModel> logger)
            : base(genreSelectionViewModel, 
                  new EntryType[] { }, 
                  mediaLibrary, 
                  navigationManager, 
                  processorCollection,
                  resourceManager, logger)
        {
        }

        protected override bool CheckViewGenre(Genre genre)
        {
            return false;
        }
        private bool _ContinueLoadActors = false;
        private object loadingBlock = new object();
        private bool _LoadingActors = false;
        protected override void LoadNextMediaAsync(int offset, int count = 10)
        {
            lock (loadingBlock)
            {
                if (_LoadingActors)
                {
                    _ContinueLoadActors = true;
                    return;
                }
                _LoadingActors = true;
            }
            var itemsFound = false;
            try
            {
                var items = MediaLibrary.GetActorOverview(offset, count);
                if (offset == 0)
                    Items.Clear();
                foreach (var item in items)
                {
                    itemsFound = true;
                    var vm = new ActorListItem(item, ResourceManager, Logger);
                    Items.Add(vm);
                }
            }
            catch (Exception ex)
            {
                OnStatusReceived(ex.Message);
            }
            lock (loadingBlock)
            {
                _LoadingActors = false;
                if (!_ContinueLoadActors)
                    return;
                _ContinueLoadActors = false;
            }
            if (!itemsFound)
                return;
            LoadNextMediaAsync(Items.Count, count);
        }

    }
}
