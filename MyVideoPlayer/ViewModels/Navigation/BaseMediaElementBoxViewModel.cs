using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Linq;
using VideoPlayerLib;

namespace MyVideoPlayer.ViewModels.Navigation
{
    public class BaseMediaElementBoxViewModel : BaseViewModel
    {
        private readonly LibraryScannerSettings settings;

        public BaseMediaElementBoxViewModel(LibraryScannerSettings settings)
            : base()
        {
            IsPlayable = false;
            IsDownloadable = false;
            this.settings = settings;
        }

        public void ProcessTapped()
        {
            OnTapped();
        }

        public event EventHandler Tapped;
        protected virtual void OnTapped()
        {
            Tapped?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler DownloadRequested;
        internal void ProcessDownload()
        {
            DownloadRequested?.Invoke(this, EventArgs.Empty);
        }

        public virtual bool IsPlayable
        {
            get { return GetProperty<bool>(); }
            set { SetProperty<bool>(value); }
        }
        public virtual bool IsDownloadable
        {
            get { return GetProperty<bool>(); }
            set { SetProperty<bool>(value); }
        }
        public ImageSource Picture
        {
            get { return GetProperty<ImageSource>(); }
            set
            {
                if (value is StreamImageSource)
                {
                    var sourceStream = (value as StreamImageSource).Stream(CancellationToken.None).Wait<Stream>();
                    string tempPath = Path.Combine(settings.TempFolderPath, $"{Guid.NewGuid()}.jpg");
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        sourceStream.CopyTo(memStream);
                        using (StreamReader reader = new StreamReader(memStream))
                        using (StreamWriter writer = new StreamWriter(tempPath))
                            writer.Write(reader.ReadToEnd());
                    }
                    value = FileImageSource.FromFile(tempPath);
                }
                SetProperty<ImageSource>(value);
            }
        }
    }
}
