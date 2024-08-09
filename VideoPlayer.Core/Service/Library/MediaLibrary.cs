using System;
using System.Collections.Concurrent;
using System.Linq;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library
{
    public interface IMediaLibrary
    {

        void CreateDemoData();

        MediaSource GetNextScanSource();

    }

    public class MediaLibrary: BaseService, IMediaLibrary
    {

        private readonly IMediaLibraryDatabase _Database;
        private ConcurrentDictionary<Type, ModelCache<BaseDataModel>> _ModelCaches = new ConcurrentDictionary<Type, ModelCache<BaseDataModel>>();

        public MediaLibrary(IMediaLibraryDatabase database)
        {
            _Database = database;
        }

        private ModelCache<BaseDataModel> GetModelCache(Type type)
        {
            lock (_Database)
            {
                if (!_ModelCaches.ContainsKey(type))
                    _ModelCaches[type] = new ModelCache<BaseDataModel>(_Database);
            }
            return _ModelCaches[type];
        }

        private BaseServiceModel UpdateCache(BaseServiceModel model)
        {
            var cache = GetModelCache(model.GetType());
            model = cache.Update(model);
        }

        private void Clear()
        {
            foreach (var cache in _ModelCaches.Values)
                cache.Clear();
            _Database.Clear();
        }

        public void CreateDemoData()
        {
            Clear();
            Setup setup = new Setup() { Name = nameof(Setup) };
            MediaSource[] mediaSources = new MediaSource[]
            {
                new HttpMediaSource() { Name = "Filme", Uri = $"http://mstromberg.ddns.com/MediaServer/Disk3/Filme" },
                new HttpMediaSource()
                {
                    Name = "Serien",
                    Uri = $"http://mstromberg.ddns.com/MediaServer/Crucial X62/Serien"
                },
                new HttpMediaSource()
                {
                    Name = "Serien (2)",
                    Uri = $"http://mstromberg.ddns.com/MediaServer/Disk2/Serien"
                }
            };
            AddOrUpdate(setup);
            AddOrUpdateRange(mediaSources);
        }

        public void AddOrUpdate<T>(T model) where T: BaseServiceModel
        {
            var dbModel = ((BaseServiceModel)model).GetDatabaseModel();
            dbModel = _Database.AddOrUpdate(dbModel);
            model.Id = dbModel.Id;
            dbModel.SetRestorePoint();
        }

        public void AddOrUpdateRange<T>(params T[] models) where T: BaseServiceModel
        {
            foreach (var model in models)
                AddOrUpdate(model);
        }

        public MediaSource GetNextScanSource()
        {
            var source = _Database.GetAll<MediaDataSource>().OrderBy(s => s.LastScan).FirstOrDefault();
            if (source is null)
                return null;
            return GetSource(source.Id);
        }

        public MediaSource GetSource(long id)
        {
            var cache = GetModelCache(typeof(MediaDataSource));
            var source = cache.Get(id) as MediaDataSource;
            return MediaSource.FromDatabaseModel(source) as MediaSource;
        }

    }
}
