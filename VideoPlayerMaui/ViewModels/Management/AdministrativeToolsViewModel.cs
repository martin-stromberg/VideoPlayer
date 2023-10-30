using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Export;
using VideoPlayer.Services.MediaLibrary.Maintenance;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class AdministrativeToolsViewModel: BaseManagementContentViewModel
    {

        private readonly IDatabaseExporter _DatabaseExporter;
        private readonly IDataCleaner _DataCleaner;
        private bool exporting = false;

        public AdministrativeToolsViewModel(
            IStatusPublisher statusPublisher,
            IDatabaseExporter databaseExporter,
            IDataCleaner dataCleaner,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, settingsService)
        {
            _DataCleaner = dataCleaner;
            _DatabaseExporter = databaseExporter;
            Title = $"Administrative Aufgaben";
            ExportData = new Command(() => DoExportData());
            RemoveAllData = new Command(() => DoRemoveAllData());
        }

        #region Export
        public Command ExportData { get; }

        private async void DoExportData()
        {
            if (exporting)
                return;
            exporting = true;
            try
            {
                string filePath = await _DatabaseExporter.CreateExportFile();
                await Share.Default
                           .RequestAsync(new ShareFileRequest
                           {
                               Title = "Exportdatei speichern",
                               File = new ShareFile(filePath)
                           });
                File.Delete(filePath);
            }
            finally
            {
                exporting = false;
            }
        }
        #endregion

        #region Daten löschen
        public Command RemoveAllData { get; }

        private void DoRemoveAllData()
        {
            _DataCleaner.Mode = DataCleaningMode.Complete;
            _DataCleaner.RunAsync();
        }
        #endregion

    }
}
