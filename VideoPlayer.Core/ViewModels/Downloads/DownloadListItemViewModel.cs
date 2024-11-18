using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.Downloads
{
    public interface IDownloadListItem
    {
        void ExecuteDelete();
        event EventHandler DeleteRequested;
    }
    public class OrphanedFileListItemViewMode : BaseListItem, IDownloadListItem
    {
        public OrphanedFileListItemViewMode(FileInfo file) 
            : base(new MediaItem())
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = file.Length;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            Title = file.Name;
            Subtitle = String.Format("{0:0.##} {1}", len, sizes[order]);
            File = file;
        }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public bool HasDownload { get => true; }
        public bool IsCollection { get => false; }
        public FileInfo File { get; }

        public void ExecuteDelete()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler DeleteRequested;

    }
    public class DownloadListItemViewModel : BaseListItem, IDownloadListItem
    {
        public DownloadListItemViewModel(MediaItem element, ClassifiedEntry entry) : base(element)
        {
            Entry = entry;
            UpdateMediaInformation(element, entry);
        }

        protected MediaItem Item { get => Element as MediaItem; }
        protected ClassifiedEntry Entry { get; set; }
        protected IPicturedEntry PicturedEntry { get => Entry as IPicturedEntry; }

        protected string GetDateTimeInfo(params DateTime[] dates)
        {
            var actualDate = dates.FirstOrDefault(d => d != DateTime.MinValue);
            if (actualDate != DateTime.MinValue)
                return actualDate.ToString("dd.MM.yyyy");
            else
                return string.Empty;
        }
        protected void LoadImage(string path)
        {
            Picture = ImageSource.FromFile(path);
        }
        protected virtual void UpdateMediaInformation(MediaItem item, ClassifiedEntry entry)
        {
            Title = item.Name;
            Subtitle = (entry is not null) ? GetDateTimeInfo(entry.ReleaseDate, entry.PremieredAt) : "";
            if (!string.IsNullOrWhiteSpace(PicturedEntry?.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, PicturedEntry?.PicturePath));
        }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public bool HasDownload { get => true; }

        public void ExecuteDelete()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler DeleteRequested;
    }
}
