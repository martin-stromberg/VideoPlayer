using System;
using System.Linq;
using VideoPlayer.Services.Export;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class AdministrativeToolsViewModel: BaseManagementContentViewModel
    {

        private readonly IDatabaseExporter _DatabaseExporter;
        private bool exporting = false;

        public AdministrativeToolsViewModel(IStatusPublisher statusPublisher, IDatabaseExporter databaseExporter)
            : base(statusPublisher)
        {
            _DatabaseExporter = databaseExporter;
            Title = $"Administrative Aufgaben";
            ExportData = new Command(() => DoExportData());
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

    }
}
