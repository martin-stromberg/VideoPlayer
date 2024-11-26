using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.XlsIO;
using System;
using System.Linq;
using System.Reflection;
using VideoPlayer.Extensions;
using VideoPlayer.Service.Attributes;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library;

namespace VideoPlayer.Service.Export
{
    public interface IDataExporterRegistration
    {

        public string LicenseKey { get; }

    }

    public interface IDataExporter
    {

        Task<string> CreateExportFile();
        Task<string> CreateBackupFile();
        Task<string> CreateMemoryExportFile();
    }

    public class DataExporter : BaseService, IDataExporter
    {
        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaLibraryDatabase _MediaLibraryDatabase;

        public enum ExportFormat
        {

            CSV,
            XLSX

        }

        public DataExporter(
            IMediaLibrary mediaLibrary,
            IMediaLibraryDatabase mediaLibraryDatabase, 
            IDataExporterRegistration registration,
            ILogger<DataExporter> logger)
            : base(logger)
        {
            RegisterSyncfusion(registration.LicenseKey);
            _MediaLibrary = mediaLibrary;
            _MediaLibraryDatabase = mediaLibraryDatabase;
        }

        private static bool syncfusionRegistered = false;

        private static void RegisterSyncfusion(string key)
        {
            if (syncfusionRegistered)
                return;
            SyncfusionLicenseProvider.RegisterLicense(key);
            syncfusionRegistered = true;
        }

        public ExportFormat Format { get; set; } = ExportFormat.XLSX;

        public Task<string> CreateBackupFile()
        {
            FileInfo sourceFile = new FileInfo(_MediaLibraryDatabase.Settings.DatabasePath);
            return Task.FromResult(sourceFile.FullName);
        }
        public async Task<string> CreateExportFile()
        {
            NotifyStatus($"Generiere Exportdatei.", true);
            try
            {
                DirectoryInfo TempFolder = Directory.CreateTempSubdirectory();
                TempFolder.Delete();
                TempFolder = TempFolder.Parent;
                switch (Format)
                {
                    case ExportFormat.CSV:
                        return CreateCSVFileAsync(TempFolder);
                    case ExportFormat.XLSX:
                        return CreateXLSXFileAsync(TempFolder);
                    default:
                        return await Task.FromResult(string.Empty);
                }
            }
            finally
            {
                NotifyStatus(string.Empty, false);
            }
        }

        public async Task<string> CreateMemoryExportFile()
        {
            NotifyStatus($"Generiere Exportdatei.", true);
            try
            {
                DirectoryInfo TempFolder = Directory.CreateTempSubdirectory();
                TempFolder.Delete();
                TempFolder = TempFolder.Parent;
                switch (Format)
                {
                    case ExportFormat.CSV:
                        return await Task.FromResult(string.Empty);
                    case ExportFormat.XLSX:
                        return CreateMemoryXLSXFileAsync(TempFolder);
                    default:
                        return await Task.FromResult(string.Empty);
                }
            }
            finally
            {
                NotifyStatus(string.Empty, false);
            }
        }

        #region Excel
        private int unknownSheetCounter = 0;

        private string CreateMemoryXLSXFileAsync(DirectoryInfo tempFolder)
        {
            unknownSheetCounter = 0;
            var lastModelTypeName = "";
            List<List<object>> baseModels = new List<List<object>>();
            baseModels.Add(new List<object>());
            foreach (var element in _MediaLibrary.GetAllCachedObjects()
                .OrderBy(e => (e.Item as BaseDataModel)?.GetType().FullName ?? e.ServiceModel?.GetType().FullName))
            {
                baseModels.First().Add(element);

                if (element.Item is null && element.ServiceModel is null)
                    continue;

                var typeName = (element.Item as BaseDataModel)?.GetType().FullName ?? element.ServiceModel?.GetType().FullName;
                if (typeName != lastModelTypeName)
                    baseModels.Add(new List<object>());
                lastModelTypeName = typeName;
                baseModels.Last().Add(element.Item);
            }
            return CreateExcelFile(tempFolder, baseModels);
        }

        private string CreateExcelFile(DirectoryInfo tempFolder, List<List<object>> baseModels)
        {
            using (ExcelEngine excelEngine = new ExcelEngine())
            {
                Syncfusion.XlsIO.IApplication application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;
                IWorkbook workbook = application.Workbooks.Create(baseModels.Count + 1);

                FillModelWorksheets(workbook, baseModels.ToArray());

                MemoryStream ms = new MemoryStream();
                workbook.SaveAs(ms);
                ms.Position = 0;

                FileInfo TempFile = new FileInfo(Path.Combine(tempFolder.FullName, "Export.xlsx"));
                if (TempFile.Exists)
                    TempFile.Delete();

                using (Stream outstream = File.Create(TempFile.FullName))
                {
                    byte[] buffer = ms.ToArray();
                    outstream.Write(buffer, 0, buffer.Length);
                    outstream.Flush();
                }
                return TempFile.FullName;
            }
        }

        private string CreateXLSXFileAsync(DirectoryInfo tempFolder)
        {
            unknownSheetCounter = 0;
            List<List<object>> baseModels = new List<List<object>>();

            var baseType = typeof(BaseDataModel);
            var modelTypes = baseType.Assembly
                                     .GetTypes()
                                     .Where(t => t != baseType)
                                     .Where(t => !t.IsAbstract)
                                     .Where(t => t.IsAssignableTo(baseType))
                                     .Where(t => t.GetCustomAttribute(typeof(SkipExportAttribute)) is null)
                                     .ToArray();
            foreach (var modelType in modelTypes)
            {
                var recordSet = _MediaLibraryDatabase.GetAll(modelType);
                baseModels.Add(recordSet.Cast<object>().ToList());
            }
            return CreateExcelFile(tempFolder, baseModels);
        }

        private void FillModelWorksheets(IWorkbook workbook, params List<object>[] baseModels)
        {
            for (int idx = 0; idx < baseModels.Length; idx++)
                FillModelWorksheet(workbook.Worksheets[idx], baseModels[idx]);

            // FillCacheFilesWorksheet(workbook.Worksheets[workbook.Worksheets.Count - 1]);
        }

        // private void FillCacheFilesWorksheet(IWorksheet worksheet)
        // {
        // var cacheFiles = Directory.GetFiles(_Settings.TempFolderPath);
        // worksheet.Name = "Cached files";
        // worksheet.Range["A1"].Text = "File path";
        // for (int row = 0; row < cacheFiles.Length; row++)
        // {
        // worksheet.Range[$"A{row + 2}"].Text = cacheFiles[row];
        // }
        // }

        private void FillModelWorksheet(IWorksheet worksheet, List<object> items)
        {
            var modelType = items.FirstOrDefault()?.GetType();
            if (modelType == null)
            {
                unknownSheetCounter++;
                if (unknownSheetCounter == 1)
                    worksheet.Name = "Unbekannt";
                else
                    worksheet.Name = $"Unbekannt ({unknownSheetCounter})";
                return;
            }
            var baseModelType = modelType;
            while (baseModelType.BaseType.Name != typeof(object).Name && baseModelType.BaseType.Name != typeof(BaseDataModel).Name)
                baseModelType = baseModelType.BaseType;
            worksheet.Name = baseModelType.Name;

            var fieldNames = WriteTableHeader(worksheet, items);
            WriteTableContent(worksheet, fieldNames, items);
        }

        private void WriteTableContent(IWorksheet worksheet, string[] fieldNames, List<object> items)
        {
            for (int idx = 0; idx < items.Count; idx++)
                WriteTableRow(worksheet, fieldNames, items[idx], idx + 1);
        }

        private void WriteTableRow(IWorksheet worksheet, string[] fieldNames, object item, int row)
        {
            if (item is null)
                return;
            var modelType = item.GetType();
            foreach (var prop in modelType.GetProperties().Where(p => p.CanRead))
            {
                var value = prop.GetValue(item);
                if (value is null)
                    continue;
                var attr = prop.GetCustomAttribute(typeof(PasswordAttribute)) as PasswordAttribute;
                if ((value != null) && prop.PropertyType.IsArray)
                    value = $"[{string.Join(',', ((Array)value).Cast<object>().Select(value => value.ToString()))}]";
                if (attr != null)
                    value = $"***********";
                if (value is string)
                    value = value.ToString().Trim().Replace("\r\n", " ").Replace("\t", "  ");

                var column = fieldNames.IndexOf(prop.Name);
                if (value is int)
                    worksheet.Range[GetFieldName(column, row)].Number = (int)value;
                else if (value is long)
                    worksheet.Range[GetFieldName(column, row)].Number = (long)value;
                else
                    worksheet.Range[GetFieldName(column, row)].Text = value.ToString();
            }
        }

        private string[] WriteTableHeader(IWorksheet worksheet, List<object> items)
        {
            List<string> fieldNames = new List<string>();
            var types = items.Select(item => item.GetType()).Distinct();
            foreach (var type in types)
            {
                PropertyInfo pkProp = null;
                foreach (var prop in type.GetProperties()
                                         .Where(p => p.CanRead)
                                         .OrderBy(p =>
                                         {
                                             if (pkProp == null)
                                                 pkProp = type.GetProperty(nameof(BaseDataModel.Id));
                                             var pk = pkProp == p;
                                             if (pk)
                                                 return 0;
                                             return int.MaxValue;
                                         }))
                {
                    if (!fieldNames.Contains(prop.Name))
                        fieldNames.Add(prop.Name);
                }
            }
            for (int idx = 0; idx < fieldNames.Count; idx++)
            {
                worksheet.Range[GetFieldName(idx, 0)].Text = fieldNames[idx];
            }
            return fieldNames.ToArray();
        }

        private string GetExcelColumnName(int columnNumber) 
        { 
            string columnName = string.Empty; 
            while (columnNumber > 0) 
            { 
                int modulo = (columnNumber - 1) % 26; 
                columnName = Convert.ToChar(65 + modulo) + columnName; 
                columnNumber = (columnNumber - modulo) / 26; 
            } 
            return columnName; 
        }

        private string GetFieldName(int column, int row)
        {
            return $"{GetExcelColumnName(column+1)}{row + 1}";
        }
        #endregion

        #region CSV
        private string CreateCSVFileAsync(DirectoryInfo tempFolder)
        {
            FileInfo TempFile = new FileInfo(Path.Combine(tempFolder.FullName, "Export.csv"));
            if (TempFile.Exists)
                TempFile.Delete();
            using (StreamWriter writer = new StreamWriter(TempFile.FullName))
            {
                foreach (var method in _MediaLibraryDatabase.GetType().GetMethods())
                    try
                    {
                        if (!method.ReturnType.IsAssignableTo(typeof(IEnumerable<BaseDataModel>)))
                            continue;
                        var returnValue = method.Invoke(_MediaLibraryDatabase, null) as IEnumerable<BaseDataModel>;
                        WriteModels(writer, returnValue.OfType<BaseDataModel>().ToList());
                    }
                    catch { }

                // WriteCachedFiles(writer);
            }
            return TempFile.FullName;
        }

        // private void WriteCachedFiles(StreamWriter writer)
        // {
        // var cacheFiles = Directory.GetFiles(_Settings.TempFolderPath);
        // writer.WriteLine("Cached files");
        // foreach (var cacheFile in cacheFiles)
        // writer.WriteLine(cacheFile);
        // writer.WriteLine();
        // }

        private void WriteModels(StreamWriter writer, IEnumerable<BaseDataModel> items)
        {
            PropertyInfo pkProp = null;
            bool headerWritten = false;
            foreach (BaseDataModel model in items
                .OrderBy(i =>
                {
                    if (pkProp == null)
                        pkProp = i.GetType().GetProperty(nameof(BaseDataModel.Id));
                    var value = pkProp?.GetValue(i);
                    return (value == null) ? int.MaxValue : value;
                }))
            {
                if (!headerWritten)
                {
                    WriteModelHeader(writer, model);
                    headerWritten = true;
                }
                WriteModel(writer, model);
            }
            if (headerWritten)
                writer.WriteLine(string.Empty);
        }

        private void WriteModelHeader(StreamWriter writer, BaseDataModel model)
        {
            PropertyInfo pkProp = null;
            var modelType = model.GetType();
            writer.WriteLine($"{modelType.Name}");
            foreach (var prop in modelType
                .GetProperties()
                .Where(p => p.CanRead)
                .OrderBy(p =>
                {
                    if (pkProp == null)
                        pkProp = modelType.GetProperty(nameof(BaseDataModel.Id));
                    var pk = pkProp == p;
                    if (pk)
                        return 0;
                    return int.MaxValue;
                })
                .ThenBy(p => p.Name))

                writer.Write($"{prop.Name};");
            writer.WriteLine();
        }

        private void WriteModel(StreamWriter writer, BaseDataModel model)
        {
            PropertyInfo pkProp = null;
            var modelType = model.GetType();
            foreach (var value in modelType
                .GetProperties()
                .Where(p => p.CanRead)
                .OrderBy(p =>
                {
                    if (pkProp == null)
                        pkProp = modelType.GetProperty(nameof(BaseDataModel.Id));
                    var pk = pkProp == p;
                    if (pk)
                        return 0;
                    return int.MaxValue;
                })
                .ThenBy(p => p.Name)
                .Select(p =>
                {
                    var attr = p.GetCustomAttribute(typeof(PasswordAttribute)) as PasswordAttribute;
                    var value = p.GetValue(model);
                    if ((value != null) && p.PropertyType.IsArray)
                        value = $"[{string.Join(',', ((Array)value).Cast<object>().Select(value => value.ToString()))}]";
                    if (attr != null)
                        value = $"***********";
                    return value?.ToString().Trim().Replace("\r\n", " ").Replace("\t", "  ");
                })
                .Select(val => val?.Replace(';', ',')))
                writer.Write($"{value};");
            writer.WriteLine();
        }
        #endregion

    }
}
