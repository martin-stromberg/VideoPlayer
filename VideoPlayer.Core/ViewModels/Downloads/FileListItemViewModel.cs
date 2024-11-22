using VideoPlayer.Service.Library.Models;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.Downloads
{
    public class FileListItemViewModel: BaseListItem, IDownloadListItem
    {
        public FileListItemViewModel(FileInfo file)
            : base(new MediaItem())
        {
            File = file;
            UpdateMediaInformation();
        }
        public FileInfo File { get => GetProperty<FileInfo>(); 
            set
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = value.Length;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }

                SetProperty(value);
                FileSizeDescription = String.Format("{0:0.##} {1}", len, sizes[order]);
            }
        }
        public string FileSizeDescription { get => GetProperty<string>(); set => SetProperty(value); }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public bool IsCollection { get => GetProperty<bool>(); private set => SetProperty(value); }
        public DateTime DueDate { get => GetProperty<DateTime>(); set
            {
                SetProperty(value);
                DueDateSet = value != DateTime.MinValue;
            } }
        public bool DueDateSet { get => GetProperty<bool>(); set => SetProperty(value); }
        public bool HasDownload { get => true; }

        public event EventHandler DeleteRequested;
        public void ExecuteDelete()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void UpdateMediaInformation()
        {
            Title = File.Name;
            Subtitle = FileSizeDescription;
            Picture = null;
            IsCollection = false;
            DueDate = DateTime.MinValue;
        }
    }
}
