using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.Downloads
{
    public class DownloadListItemViewModel : FileListItemViewModel, IDownloadListItem
    {
        public DownloadListItemViewModel(IEnvironment environment, MediaItem element, ClassifiedEntry entry, ILogger logger) 
            : base(new FileInfo(PathTools.Combine(environment.GetRootPath(), element.Path)), logger)
        {            
            Item = element;
            Entry = entry;
            UpdateMediaInformation();
        }

        protected MediaItem Item { get; set; }
        public ClassifiedEntry Entry { get; set; }
        protected IPicturedEntry PicturedEntry { get => Entry as IPicturedEntry; }

        protected void LoadImage(string path)
        {
            Picture = ImageSource.FromFile(path);
        }
        protected override void UpdateMediaInformation()
        {
            base.UpdateMediaInformation();
            Title = Entry?.Name ?? Item?.Name ?? Title;
            Subtitle = FileSizeDescription;
            if (!string.IsNullOrWhiteSpace(PicturedEntry?.PicturePath))
                LoadImage(PathTools.Combine(Microsoft.Maui.Storage.FileSystem.Current.AppDataDirectory, PicturedEntry?.PicturePath));
            DueDate = Item is null ? DateTime.MinValue : Item.DueDate;
        }
    }
}
