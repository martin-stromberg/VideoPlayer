using System.Collections.Concurrent;

namespace VideoPlayer.Service.Library.SourceReader
{
    public class RequestCache
    {
        private struct CacheEntry
        {
            public string uri { get; set; }
            public DateTime invalidAt { get; set; }
            public string response { get; set; }
        }
        private ConcurrentDictionary<string, CacheEntry> _entries = new ConcurrentDictionary<string, CacheEntry>();

        public string GetResponse(string fullPath)
        {
            if (!_entries.ContainsKey(fullPath))
                return string.Empty;
            var entry = _entries[fullPath];
            if (entry.invalidAt > DateTime.Now)
                return entry.response;
            _entries.Remove(fullPath, out entry);
            return string.Empty;
        }

        public void Save(string fullPath, string response)
        {
            var entry = _entries.ContainsKey(fullPath) ? _entries[fullPath] : new CacheEntry() { uri = fullPath, invalidAt = DateTime.Now.AddMinutes(5), response = response };
            entry.invalidAt = DateTime.Now.AddMinutes(5);
            entry.response = response;
            _entries[fullPath] = entry;
        }
    }
}
