using System;
using System.Diagnostics;
using System.Linq;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Export;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Playlists;

namespace VideoPlayer.ViewModels.Setup
{
    public class SettingsViewModel: BaseViewModel
    {

        private readonly IDataExporter _DataExporter;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;
        private readonly IMediaClassifier _MediaClassifier;
        private readonly IPlaylistManager _PlaylistManager;
        private readonly IDeviceDisplayManager _DeviceDisplayManager;
        private bool _Exporting;

        public SettingsViewModel(
            IDataExporter dataExporter,
            IMediaLibrary mediaLibrary,
            ILibraryScanner libraryScanner,
            IMediaClassifier mediaClassifier,
            IPlaylistManager playlistManager,
            IDeviceDisplayManager deviceDisplayManager)
            : base()
        {
            _DataExporter = dataExporter;
            _MediaLibrary = mediaLibrary;
            _LibraryScanner = libraryScanner;
            _MediaClassifier = mediaClassifier;
            _PlaylistManager = playlistManager;
            _DeviceDisplayManager = deviceDisplayManager;
            Title = "Einstellungen";
            Navigate = new Command((args) => { ExecuteNavigate(args?.ToString()); });
            Action = new Command((args) => { ExecuteAction(args?.ToString()); });
            AdminTasksVisible = true;
        }

        public Command Navigate { get; }

        public Command Action { get; }

        private void ExecuteNavigate(string args)
        {
            try
            {
                AdminTasksVisible = args == "AdminTasks";
            }
            catch (Exception ex) { }
        }

        public bool AdminTasksVisible
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

        public string StatusMessage
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

        protected override void OnStatusReceived(string statusMessage)
        {
            base.OnStatusReceived(statusMessage);
            StatusMessage = statusMessage;
        }

        private async void ExecuteAction(string str)
        {
            try
            {
                switch (str)
                {
                    case "Export":
                        ExportData();
                        break;
                    case "Backup":
                        BackupDatabase();
                        break;
                    case "ResetApp":
                        await ResetAppAsync();
                        break;
                }
                OnStatusReceived($"");
            }
            catch(Exception ex)
            {
                OnStatusReceived(ex.Message);
                Debug.WriteLine(ex);
            }
        }

        private async void BackupDatabase()
        {
            try
            {
                string filePath = await _DataExporter.CreateBackupFile();
                await Share.Default
                               .RequestAsync(new ShareFileRequest
                               {
                                   Title = "Exportdatei speichern",
                                   File = new ShareFile(filePath)
                               });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async void ExportData()
        {
            if (_Exporting)
                return;
            _Exporting = true;
            try
            {
                string filePath = await _DataExporter.CreateExportFile();
                await Share.Default
                           .RequestAsync(new ShareFileRequest
                           {
                               Title = "Exportdatei speichern",
                               File = new ShareFile(filePath)
                           });
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                _Exporting = false;
            }
        }

        private async Task ResetAppAsync()
        {
            OnStatusReceived($"Beende Hintergrundaktivitäten.");
            _LibraryScanner.Stop();
            _MediaClassifier.Stop();
            await _DeviceDisplayManager.WaitForIdle(TimeSpan.FromSeconds(30));
            
            OnStatusReceived($"Setzen Anwendung zurück.");
            _MediaLibrary.CreateDemoData();
            _PlaylistManager.Reset();

            OnStatusReceived($"Starte Hintergrundaktivitäten.");
            _LibraryScanner.Start();
            _MediaClassifier.Start();
        }
    }
}
