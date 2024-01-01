using System;
using System.Linq;
using VideoPlayer.Models.Sources;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management.Sources
{
    public class SourceSettingsViewModel: BaseViewModel
    {

        private MediaSource source;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;

        public SourceSettingsViewModel(
            MediaSource source,
            IStatusPublisher statusPublisher,
            IMediaLibrary mediaLibrary,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            ILibraryScanner libraryScanner)
            : base(statusPublisher, navigationManager, settingsService)
        {
            _LibraryScanner = libraryScanner;
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
            IsSSH = source is SSHMediaSource;
            if (IsSSH)
            {
                Host = ((SSHMediaSource)source).ServerName;
                Username = ((SSHMediaSource)source).Username;
                Password = ((SSHMediaSource)source).Password;
                Path = ((SSHMediaSource)source).Path;
            }
            Save = new Command(async () => await DoSaveAsync());
            Rescan = new Command(() => DoRescanAsync());
            Clean = new Command(() => ExecuteClean());
            Test = new Command(() => ExecuteTest());
        }

        public bool IsSSH
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

        public Command Rescan { get; }

        public Command Clean { get; }

        public Command Test { get; }

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

                if (IsSSH)
                {
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Host an.");
                    if (string.IsNullOrWhiteSpace(Username))
                        throw new ApplicationException($"Bitte gib einen Benutzernamen an.");
                    if (string.IsNullOrWhiteSpace(Password))
                        throw new ApplicationException($"Bitte gib ein Passwort an.");
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Pfad an.");
                    ((SSHMediaSource)source).ServerName = Host;
                    ((SSHMediaSource)source).Username = Username;
                    ((SSHMediaSource)source).Password = Password;
                    ((SSHMediaSource)source).Path = Path;
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

        private void DoRescanAsync()
        {
            _LibraryScanner.Rescan(source);
            AddStatusMessage($"Die Quelle ist nun für den nächsten Scan vorgesehen.");
        }

        private void ExecuteClean()
        {
            _LibraryScanner.StartCleaning(source);
            AddStatusMessage($"Die Bereinigung der Quelle ist nun für den nächsten Scan vorgesehen.");
        }

        private void ExecuteTest()
        {
            _LibraryScanner.TestConnection(source);
        }

    }
}
