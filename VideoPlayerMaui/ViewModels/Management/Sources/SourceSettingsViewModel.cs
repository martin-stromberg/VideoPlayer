using System;
using System.Linq;
using VideoPlayer.Models.Sources;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management.Sources
{
    public class SourceSettingsViewModel: BaseViewModel
    {

        private MediaSource source;
        private readonly IMediaLibrary _MediaLibrary;

        public SourceSettingsViewModel(MediaSource source, IStatusPublisher statusPublisher, IMediaLibrary mediaLibrary)
            : base(statusPublisher)
        {
            _MediaLibrary = mediaLibrary;
            this.source = source;
            Title = source?.Name;
            IsFTP = source is FtpMediaSource;
            if (IsFTP)
            {
                Host = ((FtpMediaSource)source).ServerName;
                Username = ((FtpMediaSource)source).Username;
                Password = ((FtpMediaSource)source).Password;
                Path = ((FtpMediaSource)source).Path;
            }
            Save = new Command(async () => await DoSaveAsync());
        }

        public bool IsFTP
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public string Host
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string Username
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string Password
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string Path
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public Command Save { get; }

        private async Task DoSaveAsync()
        {
            try
            {
                if (IsFTP)
                {
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Host an.");
                    if (string.IsNullOrWhiteSpace(Username))
                        throw new ApplicationException($"Bitte gib einen Benutzernamen an.");
                    if (string.IsNullOrWhiteSpace(Password))
                        throw new ApplicationException($"Bitte gib ein Passwort an.");
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Pfad an.");
                    ((FtpMediaSource)source).ServerName = Host;
                    ((FtpMediaSource)source).Username = Username;
                    ((FtpMediaSource)source).Password = Password;
                    ((FtpMediaSource)source).Path = Path;
                }
                await _MediaLibrary.AddSourceAsync(source);
                AddStatusMessage($"Die Änderungen wurden gespeichert.");
            }
            catch (Exception ex)
            {
                AddStatusMessage(ex.Message);
            }
        }

        public bool ContainsSource(MediaSource source)
        {
            return this.source.Id == source.Id;
        }

    }
}
