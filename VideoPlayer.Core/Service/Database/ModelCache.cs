using System;
using System.Collections.Concurrent;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Database
{

    public class ModelCache<T> where T: BaseDataModel
    {

        private class CacheElement
        {

            public T Item { get; set; }

            public DateTime LastUpdate { get; set; }

        }

        private ConcurrentDictionary<long, CacheElement> _cache = new ConcurrentDictionary<long, CacheElement>();

        private readonly IMediaLibraryDatabase _Database;

        public ModelCache(IMediaLibraryDatabase database)
        {
            _Database = database;
        }

        public TimeSpan MaxCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

        public void Clear() { }

        public T Get(long id)
        {
            var element = _cache.ContainsKey(id) ? _cache[id] : default(CacheElement);
            if (element.LastUpdate.Add(MaxCacheDuration) < DateTime.Now)
                Update(element, id);
            return element.Item;
        }

        private void Update(CacheElement element, long id)
        {
            var storedObject = _Database.Get<T>(id);
            ((BaseDataModel)element.Item).Update(storedObject);
            element.LastUpdate = DateTime.Now;
        }

    }
}
