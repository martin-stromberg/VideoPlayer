using Microsoft.Extensions.Logging;
using SQLite;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoPlayer.Service.Database.Models
{

    public class BaseDataModel: INotifyPropertyChanged
    {

        [PrimaryKey]
        [AutoIncrement]
        public long Id
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public string Name
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

        public DateTime CreatedAt
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public DateTime LastModified
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private ConcurrentDictionary<string, object> _Properties = new ConcurrentDictionary<string, object>();
        private ConcurrentDictionary<string, object> _PropertiesBackup = new ConcurrentDictionary<string, object>();

        protected T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!_Properties.ContainsKey(name))
                return default(T);
            return (T)_Properties[name];
        }

        protected void SetProperty<T>(T value, [CallerMemberName] string name = "")
        {
            SetProperty((object)value, name);
        }

        protected void SetProperty(object value, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(nameof(name));
            _Properties.AddOrUpdate(name, value, (name, oldValue) => value);
            OnPropertyChanged(name);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetRestorePoint()
        {
            _PropertiesBackup.Clear();
            foreach (var key in _Properties.Keys)
                _PropertiesBackup.AddOrUpdate(key, _Properties[key], (key, old) => _PropertiesBackup[key]);
        }

        public bool HasChanged
        {
            get
            {
                if (_Properties.Count != _PropertiesBackup.Count)
                    return true;
                foreach (var propName in _Properties.Keys)
                {
                    var currValue = _Properties[propName];
                    var prevValue = _PropertiesBackup[propName];
                    if (currValue is null && prevValue is null)
                        continue;
                    if (currValue is null)
                        return true;
                    if (!currValue.Equals(prevValue))
                        return true;
                }
                return false;
            }
        }

        public void Update<T>(T element) where T: BaseDataModel
        {
            foreach (var prop in element._Properties)
                SetProperty(prop.Value, prop.Key);
            foreach (var prop in _Properties.Keys.Where(k => !element._Properties.ContainsKey(k)))
                SetProperty(null, prop);
            SetRestorePoint();
        }

        public override string ToString()
        {
            return $"{Id}: {Name}";
        }
    }
}
