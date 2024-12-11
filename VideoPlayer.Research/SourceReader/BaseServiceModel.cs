using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace VideoPlayer.Service.Library.Models
{

    public class BaseServiceModel: INotifyPropertyChanged
    {

        public BaseServiceModel()
        {
        }

        
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var sourceProp = sender.GetType().GetProperty(e.PropertyName);
            var destProp = GetType().GetProperty(e.PropertyName);
            if (sourceProp is not null && destProp is not null)
                try
                {
                    var value = sourceProp.GetValue(sender);
                    var destValue = Convert.ChangeType(value, destProp.PropertyType);
                    destProp.SetValue(this, destValue, null);
                }
                catch 
                { 
                    
                }
        }

        public long InstanceId { get; } = DateTime.Now.Ticks;
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

        public event PropertyChangedEventHandler PropertyChanged;
        private ConcurrentDictionary<string, object> _Properties = new ConcurrentDictionary<string, object>();

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

        public override string ToString()
        {
            return $"{Id}: {Name}";
        }
        
    }
}
