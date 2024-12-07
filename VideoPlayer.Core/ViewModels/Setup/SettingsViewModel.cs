using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Export;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Settings;

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
        private readonly IMediaClassifierSettings settings;
        private readonly ILibraryScannerSettings libraryScannerSettings;
        private readonly IApplicationSettings applicationSettings;
        private bool _Exporting;
        private bool _ExportingMemory;

        public SettingsViewModel(
            IDataExporter dataExporter,
            IMediaLibrary mediaLibrary,
            ILibraryScanner libraryScanner,
            IMediaClassifier mediaClassifier,
            IPlaylistManager playlistManager,
            IDeviceDisplayManager deviceDisplayManager,
            IMediaClassifierSettings settings,
            IApplicationSettings applicationSettings)
            : base()
        {
            _DataExporter = dataExporter;
            _MediaLibrary = mediaLibrary;
            _LibraryScanner = libraryScanner;
            _MediaClassifier = mediaClassifier;
            _PlaylistManager = playlistManager;
            _DeviceDisplayManager = deviceDisplayManager;
            this.settings = settings;
            this.applicationSettings = applicationSettings;
            Title = "Einstellungen";
            Navigate = new Command((args) => { ExecuteNavigate(args?.ToString()); });
            Action = new Command((args) => { ExecuteAction(args?.ToString()); });
            SettingsVisible = true;
            MediaSources.CollectionChanged += MediaSources_CollectionChanged;
        }        

        public Command Navigate { get; }

        public Command Action { get; }

        private void ExecuteNavigate(string args)
        {
            try
            {
                AdminTasksVisible = args == "AdminTasks";
                MediaSourcesVisible = args == "MediaSources";
                SettingsVisible = args == "Settings";
            }
            catch (Exception ex) { }
        }

        
        public bool ScansEnabled
        {
            get
            {
                return applicationSettings.ScanningEnabled;
            }
            set
            {
                applicationSettings.ScanningEnabled = value;
                SetProperty<bool>(value);                
                Notify(this, new NotificationEventArgs("Scan", null));
            }
        }
        public bool ClassificationsEnabled
        {
            get
            {
                return applicationSettings.ClassificationEnabled;
            }
            set
            {
                applicationSettings.ClassificationEnabled = value;
                SetProperty<bool>(value);                
                Notify(this, new NotificationEventArgs("ScanCompleted", null));
            }
        }
        public bool ImageScrapsEnabled
        {
            get
            {
                return applicationSettings.ImageScrappingEnabled;
            }
            set
            {
                applicationSettings.ImageScrappingEnabled = value;
                SetProperty<bool>(value);                
                Notify(this, new NotificationEventArgs("ClassificationCompleted", null));
            }
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
        public bool MediaSourcesVisible
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
        public bool SettingsVisible
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
                    case "ReclassifyAll":
                        await ReclassifyAll();
                        break;
                    case "ExportMemory":
                        await ExportMemoryAsync();
                        break;
                    case "newSource":
                        CreateNewSource();
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

        private bool _Reclassifying = false;
        private async Task ReclassifyAll()
        {
            if (_Reclassifying) return;
            _Reclassifying = true;
            try
            {
                OnStatusReceived($"Lade Medien.");
                await Task.Run(() =>{
                    try
                    {
                        foreach (var mi in _MediaLibrary
                            .GetMediaItems(MediaItemCopyType.Original)
                            .Where(mi => mi is not null))
                        {                            
                            if (mi.Classified)
                            {
                                OnStatusReceived($"Speichere {mi.Name}.");
                                mi.Classified = false;
                                _MediaLibrary.AddOrUpdateMediaItem(mi);
                            }
                            _MediaLibrary.Release(mi);
                        }
                        OnStatusReceived($"");
                    }
                    catch (Exception ex)
                    {
                        OnStatusReceived(ex.Message);
                    }
                });                
            }
            finally
            {
                _Reclassifying = false;
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

        private async Task ExportMemoryAsync()
        {
            if (_ExportingMemory)
                return;
            _ExportingMemory = true;
            try
            {
                string filePath = await _DataExporter.CreateMemoryExportFile();
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
                _ExportingMemory = false;
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

        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            LoadSources();
        }
        public override void ExecuteDisappeared()
        {
            base.ExecuteDisappeared();
            MediaSources.Clear();
        }

        #region Quellen
        private void CreateNewSource()
        {
            var vm = new MediaSourceViewModel(null);
            vm.RemoveRequest += Vm_RemoveRequest;
            vm.SaveRequest += Vm_SaveRequest;
            vm.ScanRequest += Vm_ScanRequest;
            MediaSources.Insert(0, vm);
        }

        private void Vm_RemoveRequest(object sender, EventArgs e)
        {
            var vm = sender as MediaSourceViewModel;
            if (vm.Source.Id == 0)
                MediaSources.Remove(vm);
            else
            {
                vm.Source.Deleted = true;
                _MediaLibrary.AddOrUpdateSource(vm.Source);
            }
        }

        private void LoadSources()
        {
            MediaSources.Clear();
            foreach (var source in _MediaLibrary.GetSources())
            {
                var vm = new MediaSourceViewModel(source);
                vm.RemoveRequest += Vm_RemoveRequest;
                vm.SaveRequest += Vm_SaveRequest;
                vm.ScanRequest += Vm_ScanRequest;
                MediaSources.Add(vm);
            }
        }

        private void Vm_ScanRequest(object sender, EventArgs e)
        {
            var vm = sender as MediaSourceViewModel;
            vm.Source.LastScan = DateTime.MinValue;
            _MediaLibrary.AddOrUpdateSource(vm.Source);
            NotifyStatus($"Ein Scan von {vm.Source.Name} ist geplant!");
            Notify(this, new NotificationEventArgs("Scan", vm.Source));
        }

        private void Vm_SaveRequest(object sender, EventArgs e)
        {
            var vm = sender as MediaSourceViewModel;
            _MediaLibrary.AddOrUpdateSource(vm.Source);
        }

        private void MediaSources_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
                foreach (var item in e.OldItems.Cast<MediaSourceViewModel>())
                    _MediaLibrary.Release(item.Source);
        }
        public ObservableCollection<MediaSourceViewModel> MediaSources { get; } = new ObservableCollection<MediaSourceViewModel>();
        #endregion
    }
}
