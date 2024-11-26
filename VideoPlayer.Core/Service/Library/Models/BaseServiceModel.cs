using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using VideoPlayer.Service.Attributes;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{

    public class BaseServiceModel: INotifyPropertyChanged, ICloneable
    {

        public BaseServiceModel(BaseDataModel dataModel)
        {
            CheckDataModelReferenceExists();
            DataModel = dataModel;
            if (dataModel is not null)
            {
                Id = dataModel.Id;
                Name = dataModel.Name;
                CreatedAt = dataModel.CreatedAt;
            }
        }

        private void CheckDataModelReferenceExists()
        {
            if (GetType().GetCustomAttribute(typeof(DataModelReferenceAttribute)) is null)
                throw new ApplicationException($"Service model class does not define a data model reference.");
        }

        private BaseDataModel _DataModel = null;
        protected BaseDataModel DataModel 
        { 
            get => GetProperty<BaseDataModel>();
            private set
            {
                var old = GetProperty<BaseDataModel>();
                if (old is not null)
                    old.PropertyChanged -= OnPropertyChanged;
                if (value is not null)
                    value.PropertyChanged += OnPropertyChanged;
                SetProperty(value);                
            }
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

        [IgnoreCheck]
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

        protected virtual void AssignChanges()
        {
            if (DataModel is null)
                return;
            DataModel.Id = Id;
            DataModel.Name = Name;
            DataModel.CreatedAt = CreatedAt;
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
                AssignReferenceField(DataModel, attr);
                return DataModel;
            }
            finally
            {
                AssignChanges();
            }
        }

        private void AssignReferenceField(BaseDataModel dataModel, DataModelReferenceAttribute attr)
        {
            if (string.IsNullOrWhiteSpace(attr.ReferenceFieldName))
                return;
            var prop = dataModel.GetType().GetProperty(attr.ReferenceFieldName);
            object value = attr.ReferenceFieldValue;
            if (prop.PropertyType.IsEnum)
                value = Enum.Parse(prop.PropertyType, attr.ReferenceFieldValue, true);
            else
                value = Convert.ChangeType(attr.ReferenceFieldValue, prop.PropertyType);
            prop.SetValue(dataModel, value, null);
        }

        public static BaseServiceModel FromDatabaseModel(BaseDataModel dataModel)
        {
            if (dataModel is null) return null;
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

                                       if (string.IsNullOrWhiteSpace(attr.ReferenceFieldName))
                                           return true;

                                       var prop = attr.DataModelType.GetProperty(attr.ReferenceFieldName);
                                       if (prop is null)
                                           return false;
                                       var refValue = prop.GetValue(dataModel);
                                       if (refValue is null)
                                           return false;
                                       return refValue.ToString() == attr.ReferenceFieldValue;
                                   })
                                   .FirstOrDefault();
            if (modelType is null)
                throw new ApplicationException($"No service model type found for data model {sourceType}.");

            var model = Activator.CreateInstance(modelType, dataModel) as BaseServiceModel;
            return model;
        }
        public override string ToString()
        {
            return $"{Id}: {Name}";
        }
        public object Clone()
        {
            var clone = Activator.CreateInstance(GetType(), DataModel) as BaseServiceModel;
            foreach(var prop in GetType().GetProperties()
                .Where(p => p.CanRead && p.CanWrite))
            {
                var sourceValue = prop.GetValue(this);
                if (sourceValue is not null)
                    if (prop.PropertyType.IsAssignableTo(typeof(ICloneable)))
                        sourceValue = ((ICloneable)sourceValue).Clone();
                    else if (prop.PropertyType.IsArray)
                    {
                        var sourceArray = (Array)sourceValue;
                        var destArray = Activator.CreateInstance(prop.PropertyType, sourceArray.Length) as Array;
                        for (int idx = sourceArray.GetLowerBound(0); idx <= sourceArray.GetUpperBound(0); idx++)
                        {
                            sourceValue = sourceArray.GetValue(idx);
                            if (sourceValue.GetType().IsAssignableTo(typeof(ICloneable)))
                                sourceValue = ((ICloneable)sourceValue).Clone();
                            destArray.SetValue(sourceValue, idx);
                        }
                        sourceValue = destArray;
                    }
                prop.SetValue(clone, sourceValue, null);
            }
            return clone;
        }
    }
}
