using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.Downloads
{
    public class DownloadListViewModel: MediaCollectionViewModel
    {
        private readonly IEnvironment environment;
        private readonly IMediaLibrary mediaLibrary;
        private readonly IDownloadManager downloadManager;

        public DownloadListViewModel(IEnvironment environment, IMediaLibrary mediaLibrary, IDownloadManager downloadManager)
        {
            this.environment = environment;
            this.mediaLibrary = mediaLibrary;
            this.downloadManager = downloadManager;
        }
        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            Reload();
        }
        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();
            Reload();
        }

        private void Reload()
        {
            var mediaItems = mediaLibrary.GetMediaItems(MediaItemCopyType.Cache, MediaItemCopyType.Download);
            Items.Clear();
            foreach (var item in mediaItems)
            {
                var entry = mediaLibrary.GetMovieByMediaItem(item.Id) as ClassifiedEntry 
                    ?? mediaLibrary.GetTVShowEpisodeByMediaItem(item.Id);
                var vm = new DownloadListItemViewModel(environment, item, entry);
                vm.DeleteRequested += Vm_DeleteRequested;
                Items.Add(vm);
            }

            var files = downloadManager.GetOrphanedFiles();
            foreach (var file in files)
            {
                var vm = new OrphanedFileListItemViewMode(file);
                vm.DeleteRequested += Vm_DeleteRequested;
                Items.Add(vm);
            }

        }

        private void Vm_DeleteRequested(object sender, EventArgs e)
        {
            (sender as IDownloadListItem).DeleteRequested -= Vm_DeleteRequested;
            Items.Remove(sender as BaseListItem);
            var fileVM = sender as OrphanedFileListItemViewMode;
            if (fileVM is not null)
                fileVM.File.Delete();
            var listItemVM = sender as DownloadListItemViewModel;
            if (listItemVM is not null)
                downloadManager.RemoveDownloads(listItemVM.Entry as ClassifiedEntry);            
        }
    }
}
