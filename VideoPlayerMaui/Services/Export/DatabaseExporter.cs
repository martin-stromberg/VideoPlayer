using Syncfusion.Licensing;
using Syncfusion.XlsIO;
using System;
using System.Linq;
using System.Reflection;
using VideoPlayer.Extensions;
using VideoPlayer.Models;
using VideoPlayer.Models.Attributes;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Demo;

namespace VideoPlayer.Services.Export
{

    public class DatabaseExporter: IDatabaseExporter
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly MediaLibrarySettings _Settings;

        public enum ExportFormat
        {

            CSV,
            XLSX

        }

        public DatabaseExporter(IMediaLibrary mediaLibrary, MediaLibrarySettings settings, IUserSecrets userSecrets)
        {
            RegisterSyncfusion(userSecrets.SyncfusionLicenseKey);

            _Settings = settings;
            _MediaLibrary = mediaLibrary;
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

        public async Task<string> CreateExportFile()
        {
            DirectoryInfo TempFolder = Directory.CreateTempSubdirectory();
            TempFolder.Delete();
            TempFolder = TempFolder.Parent;
            switch (Format)
            {
                case ExportFormat.CSV:
                    return await CreateCSVFileAsync(TempFolder);
                case ExportFormat:
                    return await CreateXLSXFileAsync(TempFolder);
                default:
                    return string.Empty;
            }
        }

        #region Excel
        private int unknownSheetCounter = 0;

        private async Task<string> CreateXLSXFileAsync(DirectoryInfo tempFolder)
        {
            unknownSheetCounter = 0;
            List<List<BaseModel>> baseModels = new List<List<BaseModel>>();
            var sources = await _MediaLibrary.GetSourcesAsync();
            baseModels.Add(sources.Cast<BaseModel>().ToList());

            List<MediaItemCollection> collections = new List<MediaItemCollection>();
            foreach (var source in sources)
            {
                var sourceCollections = await _MediaLibrary.GetMediaItemCollectionsAsync(source.Id);
                collections.AddRange(sourceCollections);
            }
            baseModels.Add(collections.Cast<BaseModel>().ToList());

            List<MediaItem> mediaItems = new List<MediaItem>();
            foreach (var collection in collections)
            {
                var collectionMediaItems = await _MediaLibrary.GetMediaItemsAsync(collection.Id);
                mediaItems.AddRange(collectionMediaItems);
            }
            baseModels.Add(mediaItems.Cast<BaseModel>().ToList());

            var movies = await _MediaLibrary.GetMovies();
            baseModels.Add(movies.Cast<BaseModel>().ToList());

            var tvShows = await _MediaLibrary.GetTVShows();
            baseModels.Add(tvShows.Cast<BaseModel>().ToList());

            List<TVShowSeason> seasons = new List<TVShowSeason>();
            foreach (var show in tvShows)
            {
                var showSeasons = await _MediaLibrary.GetTVShowSeasons(show.Id);
                seasons.AddRange(showSeasons);
            }
            baseModels.Add(seasons.Cast<BaseModel>().ToList());

            List<TVShowEpisode> episodes = new List<TVShowEpisode>();
            foreach (var season in seasons)
            {
                var seasonEpisodes = await _MediaLibrary.GetTVShowEpisodes(season.Id);
                episodes.AddRange(seasonEpisodes);
            }
            baseModels.Add(episodes.Cast<BaseModel>().ToList());

            using (ExcelEngine excelEngine = new ExcelEngine())
            {
                Syncfusion.XlsIO.IApplication application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;
                IWorkbook workbook = application.Workbooks.Create(baseModels.Count + 1);

                FillModelWorksheets(workbook, baseModels);

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

        private void FillModelWorksheets(IWorkbook workbook, List<List<BaseModel>> baseModels)
        {
            for (int idx = 0; idx < baseModels.Count; idx++)
                FillModelWorksheet(workbook.Worksheets[idx], baseModels[idx]);
            FillCacheFilesWorksheet(workbook.Worksheets[workbook.Worksheets.Count - 1]);
        }

        private void FillCacheFilesWorksheet(IWorksheet worksheet)
        {
            var cacheFiles = Directory.GetFiles(_Settings.TempFolderPath);
            worksheet.Name = "Cached files";
            worksheet.Range["A1"].Text = "File path";
            for (int row = 0; row < cacheFiles.Length; row++)
            {
                worksheet.Range[$"A{row + 2}"].Text = cacheFiles[row];
            }
        }

        private void FillModelWorksheet(IWorksheet worksheet, List<BaseModel> items)
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
            while (baseModelType.BaseType.Name != typeof(BaseModel).Name)
                baseModelType = baseModelType.BaseType;
            worksheet.Name = baseModelType.Name;

            var fieldNames = WriteTableHeader(worksheet, items);
            WriteTableContent(worksheet, fieldNames, items);
        }

        private void WriteTableContent(IWorksheet worksheet, string[] fieldNames, List<BaseModel> items)
        {
            for (int idx = 0; idx < items.Count; idx++)
                WriteTableRow(worksheet, fieldNames, items[idx], idx + 1);
        }

        private void WriteTableRow(IWorksheet worksheet, string[] fieldNames, BaseModel item, int row)
        {
            var modelType = item.GetType();
            foreach (var prop in modelType.GetProperties().Where(p => p.CanRead))
            {
                var value = prop.GetValue(item);
                if (value == null)
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

        private string[] WriteTableHeader(IWorksheet worksheet, List<BaseModel> items)
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
                                                 pkProp = type.GetProperty(nameof(BaseModel.Id));
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

        private string GetFieldName(int column, int row)
        {
            var StartField = (int)'A';
            StartField = StartField + column;
            string columnName = ((char)StartField).ToString();
            return $"{columnName}{row + 1}";
        }
        #endregion

        #region CSV
        private async Task<string> CreateCSVFileAsync(DirectoryInfo tempFolder)
        {
            FileInfo TempFile = new FileInfo(Path.Combine(tempFolder.FullName, "Export.csv"));
            if (TempFile.Exists)
                TempFile.Delete();
            using (StreamWriter writer = new StreamWriter(TempFile.FullName))
            {
                var sources = await _MediaLibrary.GetSourcesAsync();
                WriteModels(writer, sources);

                List<MediaItemCollection> collections = new List<MediaItemCollection>();
                foreach (var source in sources)
                {
                    var sourceCollections = await _MediaLibrary.GetMediaItemCollectionsAsync(source.Id);
                    collections.AddRange(sourceCollections);
                }
                WriteModels(writer, collections);

                List<MediaItem> mediaItems = new List<MediaItem>();
                foreach (var collection in collections)
                {
                    var collectionMediaItems = await _MediaLibrary.GetMediaItemsAsync(collection.Id);
                    mediaItems.AddRange(collectionMediaItems);
                }
                WriteModels(writer, mediaItems);

                var movies = await _MediaLibrary.GetMovies();
                WriteModels(writer, movies);

                var tvShows = await _MediaLibrary.GetTVShows();
                WriteModels(writer, tvShows);

                List<TVShowSeason> seasons = new List<TVShowSeason>();
                foreach (var show in tvShows)
                {
                    var showSeasons = await _MediaLibrary.GetTVShowSeasons(show.Id);
                    seasons.AddRange(showSeasons);
                }
                WriteModels(writer, seasons);

                List<TVShowEpisode> episodes = new List<TVShowEpisode>();
                foreach (var season in seasons)
                {
                    var seasonEpisodes = await _MediaLibrary.GetTVShowEpisodes(season.Id);
                    episodes.AddRange(seasonEpisodes);
                }
                WriteModels(writer, episodes);

                WriteCachedFiles(writer);
            }
            return TempFile.FullName;
        }

        private void WriteCachedFiles(StreamWriter writer)
        {
            var cacheFiles = Directory.GetFiles(_Settings.TempFolderPath);
            writer.WriteLine("Cached files");
            foreach (var cacheFile in cacheFiles)
                writer.WriteLine(cacheFile);
            writer.WriteLine();
        }

        private void WriteModels(StreamWriter writer, IEnumerable<BaseModel> items)
        {
            PropertyInfo pkProp = null;
            bool headerWritten = false;
            foreach (BaseModel model in items
                .OrderBy(i =>
                {
                    if (pkProp == null)
                        pkProp = i.GetType().GetProperty(nameof(BaseModel.Id));
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

        private void WriteModelHeader(StreamWriter writer, BaseModel model)
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
                        pkProp = modelType.GetProperty(nameof(BaseModel.Id));
                    var pk = pkProp == p;
                    if (pk)
                        return 0;
                    return int.MaxValue;
                })
                .ThenBy(p => p.Name))

                writer.Write($"{prop.Name};");
            writer.WriteLine();
        }

        private void WriteModel(StreamWriter writer, BaseModel model)
        {
            PropertyInfo pkProp = null;
            var modelType = model.GetType();
            foreach (var value in modelType
                .GetProperties()
                .Where(p => p.CanRead)
                .OrderBy(p =>
                {
                    if (pkProp == null)
                        pkProp = modelType.GetProperty(nameof(BaseModel.Id));
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
