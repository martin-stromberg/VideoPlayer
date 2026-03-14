using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using WebPlayerApi.Models;
using WebPlayerApi.Service.Data.SFtp;

namespace WebPlayerApi.Services
{
    public interface IBaseDataService<T> where T : BaseDataModel
    {
        IEnumerable<T> Items { get; }
        T Get(string id);
        void Add(T item);
        void Remove(string id);
        void Update(string id, T updated);
    }
    public class BaseDataService<T> where T : BaseDataModel
    {
        private List<T> _list;
        private string savePath = $"data\\{typeof(T).Name}.dat";

        public BaseDataService()
            :base()
        {
            
        }

        protected virtual FileInfo CreateFilePath()
        {
            return new FileInfo(savePath);
        }
        private void CheckLoad()
        {
            if (_list is null) Load();
        }
        protected void Load()
        {
            var saveFile = CreateFilePath();
            if (!saveFile.Directory.Exists)
                saveFile.Directory.Create();
            try
            {
                if (saveFile.Exists)
                    _list = JsonSerializer.Deserialize<T[]>(File.ReadAllText(saveFile.FullName)).ToList();
                else
                    _list = new List<T>();
            }
            catch(Exception ex)
            {
                _list = new List<T>();
            }
        }
        protected void Save()
        {
            var saveFile = CreateFilePath();
            if (!saveFile.Directory.Exists)
                saveFile.Directory.Create();
            File.WriteAllText(saveFile.FullName, JsonSerializer.Serialize(_list.ToArray()));
        }
        protected void Clear()
        {
            _list.Clear();
            Save();
        }

        public IEnumerable<T> Items { get { CheckLoad(); return _list; } }
        public bool IsEmpty => !Items.Any();
        public void Add(T item)
        {
            CheckLoad();
            item.Id = Guid.NewGuid().ToString();
            item.LastUpdate = DateTime.Now;
            _list.Add(item);
            Save();
        }
        public T Get(string id)
        {
            CheckLoad();
            return _list.FirstOrDefault(x => (x.Id == id));
        }
        public virtual void Update(string id, T updated)
        {
            var existing = Get(id);
            if (existing is null)
                throw new ApplicationException($"Record {id} was not found.");
            foreach (var prop in existing.GetType().GetProperties().Where(p => p.Name != nameof(BaseDataModel.Id)))
            {
                var value = prop.GetValue(updated, null);
                prop.SetValue(existing, value);
            }
            existing.LastUpdate = DateTime.Now;
            Save();
        }
        public void Remove(string id)
        {
            var existing = Get(id);
            if (existing is null)
                throw new ApplicationException($"Record {id} was not found.");
            _list.Remove(existing);
            Save();
        }
    }
    public interface ISourceService : IBaseDataService<MediaDirectory>
    {
        IMediaItemService GetMediaService(MediaDirectory source);
    }
    public class SourceService : BaseDataService<MediaDirectory>, ISourceService
    {
        private ConcurrentDictionary<string, IMediaItemService> _MediaServices = new ConcurrentDictionary<string, IMediaItemService> ();
        public SourceService()
            : base()
        {
        }

        public IMediaItemService GetMediaService(MediaDirectory source)
        {
            if (!_MediaServices.ContainsKey(source.Id))                
                _MediaServices.AddOrUpdate(source.Id, new MediaItemService(source), (id, existing) => existing);
            return _MediaServices[source.Id];
        }

    }


    public class BaseNamedDataService<T>: BaseDataService<T> where T : BaseDataModel
    {
        public BaseNamedDataService(string name)
            :base()
        {
            Name = name;
        }

        public string Name { get; }

        protected override FileInfo CreateFilePath()
        {
            return new FileInfo($"data\\{typeof(T).Name}-{Name}.dat");
        }
    }
    public interface IMediaItemService : IBaseDataService<MediaItem>
    {
        Stream GetMediaStream(string id);
    }
    public class MediaItemService : BaseNamedDataService<MediaItem>, IMediaItemService
    {
        public MediaItemService(MediaDirectory source) : base(source.Name)
        {
            Source = source;
        }

        public MediaDirectory Source { get; }

        public Stream GetMediaStream(string id)
        {            
            var item = Items.Where(i => i.Children is not null).SelectMany(i => i.Children).FirstOrDefault(i => i.Id == id);
            if (item is null)
                return null;

            var reader = new SFTPSourceReader(Source);
            return reader.ReadStream(item);
        }

        public override void Update(string id, MediaItem updated)
        {
            var existing = Get(id);
            if (existing is null)
                throw new ApplicationException($"Record {id} was not found.");
            if (updated.Children is not null)
                foreach (var child in updated.Children)
                {
                    var existingChild = existing.Children.FirstOrDefault(c => c.FilePath == child.FilePath);
                    if (existingChild is null || string.IsNullOrWhiteSpace(existingChild.Id))
                        continue;
                    child.Id = existingChild.Id;
                }
            base.Update(id, updated);
        }
    }
}
