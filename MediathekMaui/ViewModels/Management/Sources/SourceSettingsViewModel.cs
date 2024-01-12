using Mediathek.Extensions;
using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.Management.Sources
{
    public class SourceSettingsViewModel: BaseViewModel
    {

        private MediaElementSource source;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;

        public SourceSettingsViewModel(
            MediaElementSource source,
            IStatusPublisher statusPublisher,
            IMediaLibrary mediaLibrary,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            ILibraryScanner libraryScanner)
            : base(statusPublisher, navigationManager, settingsService)
        {
            _LibraryScanner = libraryScanner;
            _MediaLibrary = mediaLibrary;

            Host = source.GetType().GetProperty(nameof(FtpMediaSource.ServerName))?.GetValue(source, null) as string 
                ?? source.GetType().GetProperty(nameof(SSHMediaSource.ServerName))?.GetValue(source, null) as string
                ?? source.GetType().GetProperty(nameof(HttpMediaSource.Uri))?.GetValue(source, null) as string
                ?? string.Empty;
            Password = source.GetType().GetProperty(nameof(Password))?.GetValue(source, null) as string ?? string.Empty;
            Username = source.GetType().GetProperty(nameof(Username))?.GetValue(source, null) as string ?? string.Empty;
            Path = source.GetType().GetProperty(nameof(Path))?.GetValue(source, null) as string ?? string.Empty;

            Source = source;
            Title = source?.Name;
            Save = new Command(async () => await DoSaveAsync());
            Rescan = new Command(() => DoRescanAsync());
            Clean = new Command(() => ExecuteClean());
            Test = new Command(() => ExecuteTest());
            Delete = new Command(() => ExecuteDelete());
        }

        private void ExecuteDelete()
        {
            _MediaLibrary.RemoveMediaSourceAsync(Source);
        }

        protected MediaElementSource Source
        {
            get
            {
                return source;
            }
            set
            {
                source = value;
                int offset = SourceTypes.IndexOf(source.GetType());
                if (offset == (-1))
                    offset = SourceTypeNames.GetLowerBound(0);
                SelectedType = SourceTypeNames[offset];
            }
        }

        private Type[] SourceTypes = new Type[]
        {
            typeof(FtpMediaSource),
            typeof(SSHMediaSource),
            typeof(HttpMediaSource)
        };

        public string[] SourceTypeNames => SourceTypes.Select(t => t.Name.Replace("MediaSource", string.Empty))
                                                      .ToArray();

        public string SelectedType
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
                UpdateFields();
            }
        }

        private void UpdateFields()
        {
            IsFTP = SourceTypeNames.IndexOf(SelectedType) == SourceTypes.IndexOf(typeof(FtpMediaSource));
            IsSSH = SourceTypeNames.IndexOf(SelectedType) == SourceTypes.IndexOf(typeof(SSHMediaSource));
            IsHttp = SourceTypeNames.IndexOf(SelectedType) == SourceTypes.IndexOf(typeof(HttpMediaSource));
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

        public bool IsHttp
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

        public bool IsNew
        {
            get
            {
                return source.Id == 0;
            }
            set
            {
                SetProperty<bool>(value);
                IsStored = !value;
            }
        }

        public bool IsStored
        {
            get
            {
                return source.Id != 0;
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public Command Delete { get; set; }

        private async Task DoSaveAsync()
        {
            try
            {
                if (IsNew)
                {
                    if (string.IsNullOrWhiteSpace(Title))
                        throw new ApplicationException($"Bitte gib einen Namen an.");
                }

                if (IsFTP)
                {
                    if (string.IsNullOrWhiteSpace(Host))
                        throw new ApplicationException($"Bitte gib einen Host an.");
                    if (string.IsNullOrWhiteSpace(Username))
                        throw new ApplicationException($"Bitte gib einen Benutzernamen an.");
                    if (string.IsNullOrWhiteSpace(Password))
                        throw new ApplicationException($"Bitte gib ein Passwort an.");
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Pfad an.");

                    if (Source.GetType() != typeof(FtpMediaSource))
                        Source = new FtpMediaSource()
                        {
                            Id = (Source != null) ? Source.Id : 0,
                            ServerName = Host,
                            Username = Username,
                            Password = Password,
                            Path = Path,
                            LastScan = DateTime.MinValue,
                            Name = Title,
                            LastScanStart = DateTime.MinValue,
                            LatestScanPath = string.Empty,
                            Inactive = false
                        };
                    else
                    {
                        ((FtpMediaSource)Source).ServerName = Host;
                        ((FtpMediaSource)Source).Username = Username;
                        ((FtpMediaSource)Source).Password = Password;
                        ((FtpMediaSource)Source).Path = Path;
                    }
                }

                if (IsSSH)
                {
                    if (string.IsNullOrWhiteSpace(Host))
                        throw new ApplicationException($"Bitte gib einen Host an.");
                    if (string.IsNullOrWhiteSpace(Username))
                        throw new ApplicationException($"Bitte gib einen Benutzernamen an.");
                    if (string.IsNullOrWhiteSpace(Password))
                        throw new ApplicationException($"Bitte gib ein Passwort an.");
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Pfad an.");

                    if (Source.GetType() != typeof(SSHMediaSource))
                        Source = new SSHMediaSource()
                        {
                            Id = (Source != null) ? Source.Id : 0,
                            ServerName = Host,
                            Username = Username,
                            Password = Password,
                            Path = Path,
                            LastScan = DateTime.MinValue,
                            Name = Title,
                            LastScanStart = DateTime.MinValue,
                            LatestScanPath = string.Empty,
                            Inactive = false
                        };
                    else
                    {
                        ((SSHMediaSource)source).ServerName = Host;
                        ((SSHMediaSource)source).Username = Username;
                        ((SSHMediaSource)source).Password = Password;
                        ((SSHMediaSource)source).Path = Path;
                    }
                }
                if (IsHttp)
                {
                    if (string.IsNullOrWhiteSpace(Host))
                        throw new ApplicationException($"Bitte gib eine Basisadresse an.");
                    if (string.IsNullOrWhiteSpace(Path))
                        throw new ApplicationException($"Bitte gib einen Pfad an.");

                    if (Source.GetType() != typeof(HttpMediaSource))
                        Source = new HttpMediaSource()
                        {
                            Id = (Source != null) ? Source.Id : 0,
                            Uri = Host,
                            Path = Path,
                            LastScan = DateTime.MinValue,
                            Name = Title,
                            LastScanStart = DateTime.MinValue,
                            LatestScanPath = string.Empty,
                            Inactive = false
                        };
                    else
                    {
                        ((HttpMediaSource)source).Uri = Host;
                        ((HttpMediaSource)source).Path = Path;
                    }
                }
                await _MediaLibrary.AddSourceAsync(Source);
                IsNew = false;
                AddStatusMessage($"Die Änderungen wurden gespeichert.");
            }
            catch (Exception ex)
            {
                AddStatusMessage(ex.Message);
            }
        }

        public bool ContainsSource(MediaElementSource source)
        {
            return this.source.Id == source.Id;
        }

        private void DoRescanAsync()
        {
            _LibraryScanner.Rescan(source, true);
            AddStatusMessage($"Die Quelle ist nun für den nächsten Scan vorgesehen.");
        }

        private void ExecuteClean()
        {
            _LibraryScanner.StartCleaning(source);
            AddStatusMessage($"Die Bereinigung der Quelle ist nun für den nächsten Scan vorgesehen.");
        }

        private void ExecuteTest()
        {
            try
            {
                _LibraryScanner.TestConnection(source);
                AddStatusMessage($"Der Test der Verbindung war erfolgreich", true);
            }
            catch (Exception ex)
            {
                AddStatusMessage(ex.Message, true);
            }
        }

        public bool IsSource(MediaElementSource source)
        {
            return source.Id == Source.Id;
        }

    }
}
