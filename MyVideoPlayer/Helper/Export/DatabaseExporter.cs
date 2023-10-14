using System;
using System.Linq;
using System.Reflection;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.Helper.Export
{
    public interface IDatabaseExporter
    {

        Task<string> CreateExportFile();

    }

    public class DatabaseExporter: IDatabaseExporter
    {

        private readonly IMediaLibrary _MediaLibrary;

        public DatabaseExporter(IMediaLibrary mediaLibrary)
        {
            _MediaLibrary = mediaLibrary;
        }

        public async Task<string> CreateExportFile()
        {
            DirectoryInfo TempFolder = Directory.CreateTempSubdirectory();
            TempFolder.Delete();
            TempFolder = TempFolder.Parent;
            FileInfo TempFile = new FileInfo(Path.Combine(TempFolder.FullName, "Export.csv"));
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
            }
            return TempFile.FullName;
        }

        private void WriteModels(StreamWriter writer, IEnumerable<BaseModel> items)
        {
            bool headerWritten = false;
            foreach (BaseModel model in items)
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
            var modelType = model.GetType();
            foreach (var prop in modelType
                .GetProperties()
                .Where(p => p.CanRead))
                writer.Write($"{prop.Name};");
            writer.WriteLine();
        }

        private void WriteModel(StreamWriter writer, BaseModel model)
        {
            var modelType = model.GetType();
            foreach (var value in modelType
                .GetProperties()
                .Where(p => p.CanRead)
                .Select(p =>
                {
                    var attr = p.GetCustomAttribute(typeof(PasswordAttribute)) as PasswordAttribute;
                    var value = p.GetValue(model);
                    if ((value != null) && p.PropertyType.IsArray)
                        value = $"[{string.Join(',', ((Array)value).Cast<object>().Select(value => value.ToString()))}]";
                    if (attr != null)
                        value = $"***********";
                    return value?.ToString();
                })
                .Select(val => val?.Replace(';', ',')))
                writer.Write($"{value};");
            writer.WriteLine();
        }

    }
}
