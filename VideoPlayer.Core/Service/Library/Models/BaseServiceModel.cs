using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    public class BaseServiceModel: INotifyPropertyChanged
    {

        public BaseServiceModel(BaseDataModel dataModel)
        {
            DataModel = dataModel;
        }

        protected BaseDataModel DataModel { get; private set; }

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
            SetProperty(value, name);
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

        protected virtual void AssignChanges()
        {
            if (DataModel is null)
                return;
            DataModel.Id = Id;
            DataModel.Name = Name;
        }

        public BaseDataModel GetDatabaseModel()
        {
            try
            {
                if (DataModel is not null)
                    return DataModel;

                var attr = GetType().GetCustomAttribute<DataModelReferenceAttribute>() as DataModelReferenceAttribute;
                if (attr is null)
                    return null;
                DataModel = Activator.CreateInstance(attr.DataModelType) as BaseDataModel;
                return DataModel;
            }
            finally
            {
                AssignChanges();
            }
        }

        public static BaseServiceModel FromDatabaseModel(BaseDataModel dataModel)
        {
            var ownType = typeof(BaseServiceModel);
            var sourceType = dataModel.GetType();
            var modelType = ownType.Assembly
                                   .GetTypes()
                                   .Where(t => !t.IsAbstract)
                                   .Where(t => t.IsAssignableTo(ownType))
                                   .Where(t =>
                                   {
                                       var attr = t.GetCustomAttribute<DataModelReferenceAttribute>() as DataModelReferenceAttribute;
                                       if (attr is null)
                                           return false;
                                       if (attr.DataModelType != sourceType)
                                           return false;
                                       return true;
                                   })
                                   .FirstOrDefault();
            if (modelType is null)
                throw new ApplicationException($"No service model type found for data model {sourceType}.");

            var model = Activator.CreateInstance(modelType, dataModel) as BaseServiceModel;
            return model;
        }

    }
}
