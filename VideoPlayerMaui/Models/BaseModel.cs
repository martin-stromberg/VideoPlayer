using Newtonsoft.Json;
using SQLite;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using VideoPlayer.Extensions;
using VideoPlayer.Services.Database;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Models
{
    public class BaseModel: INotifyPropertyChanged, IDisposable
    {

        ~BaseModel()
        {
            Dispose();
        }

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

        #region IDisposable
        public virtual void Dispose() { }
        #endregion

        #region INotifyPropertyChanged
        private Dictionary<string, object> properties = new Dictionary<string, object>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        protected T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!properties.ContainsKey(name))
                return default(T);
            return (T)properties[name];
        }

        protected void SetProperty<T>(object value, [CallerMemberName] string name = "")
        {
            var oldValue = GetProperty<T>(name);
            if (!properties.ContainsKey(name))
                properties.Add(name, value);
            else
                properties[name] = value;
            OnPropertyChanged(new PropertyChangedEventArgs(name));
        }
        #endregion

        public BaseDataModel ToDataModelAsync()
        {
            var ownType = GetType();
            var dataModelType = (ownType.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute).DataModelType;
            var dataModel = Activator.CreateInstance(dataModelType) as BaseDataModel;
            foreach (var prop in ownType.GetProperties().Where(p => p.CanRead))
            {
                var convertToJson = false;
                var dataModelProp = dataModelType.GetProperty(prop.Name);
                if (dataModelProp == null)
                {
                    convertToJson = true;
                    dataModelProp = dataModelType.GetProperty($"{prop.Name}Json");
                    if (dataModelProp == null)
                        continue;
                }
                if (!dataModelProp.CanWrite)
                    continue;
                var ownValue = prop.GetValue(this);
                if ((prop.PropertyType == typeof(StreamImageSource)) && (ownValue != null))
                {
                    StreamImageSource img = (StreamImageSource)ownValue;
                    using (var memoryStream = new MemoryStream())
                    {
                        img.Stream(CancellationToken.None).Wait<Stream>().CopyTo(memoryStream);
                        byte[] bytes = memoryStream.ToArray();
                        ownValue = Convert.ToBase64String(bytes);
                    }
                }
                if (prop.PropertyType.IsEnum)
                {
                    ownValue = (int)ownValue;
                }
                if (convertToJson)
                    ownValue = JsonConvert.SerializeObject(ownValue, new JsonSerializerSettings()
                        {
                            TypeNameHandling = TypeNameHandling.Objects
                        });
                dataModelProp.SetValue(dataModel, ownValue);
            }
            return dataModel;
        }

        public void UpdateAutoincrements(BaseDataModel dataModel)
        {
            foreach (var prop in dataModel.GetType()
                                          .GetProperties()
                                          .Where(p => p.GetCustomAttribute(typeof(PrimaryKeyAttribute)) != null))
            {
                var ownProp = GetType().GetProperty(prop.Name);
                if (ownProp == null)
                    continue;
                var value = prop.GetValue(dataModel);
                ownProp.SetValue(this, value);
            }
        }

        private static Dictionary<Type, Type[]> dataModelTypeAssosiation = new Dictionary<Type, Type[]>();

        public static BaseModel FromDataModel(BaseDataModel source)
        {
            if (source == null)
                return null;
            Type dataModelType = source.GetType();
            Type[] modelTypes;
            lock (dataModelTypeAssosiation)
            {
                modelTypes = dataModelTypeAssosiation.ContainsKey(dataModelType) ? dataModelTypeAssosiation[dataModelType] : null;
                if (modelTypes == null)
                {
                    modelTypes = typeof(BaseModel).Assembly
                                                  .GetTypes()
                                                  .Where(t =>
                                                  {
                                                      var attr = t.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute;
                                                      return (attr != null) && (attr.DataModelType == dataModelType);
                                                  })
                                                  .ToArray();
                    dataModelTypeAssosiation.Add(dataModelType, modelTypes);
                }
            }
            if (modelTypes == null)
                throw new ApplicationException($"Data model type {dataModelType.Name} is not referenced at a model type.");
            if (modelTypes.Length == 0)
                throw new ApplicationException($"Data model type {dataModelType.Name} is not referenced at a model type.");
            Type modelType = null;
            foreach (var mT in modelTypes)
            {
                var attr = mT.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute;
                if (string.IsNullOrWhiteSpace(attr.FilterPropertyName) && (modelType == null))
                    modelType = mT;
                else if (!string.IsNullOrWhiteSpace(attr.FilterPropertyName)
                    && ((dataModelType.GetProperty(attr.FilterPropertyName).GetValue(source)?.ToString()) == attr.FilterPropertyValue))
                    modelType = mT;
            }
            var model = Activator.CreateInstance(modelType) as BaseModel;
            model.UpdateFromDataModel(source);
            return model;
        }

        protected virtual void UpdateFromDataModel(BaseDataModel dataModel)
        {
            var ownType = GetType();
            var dataModelType = dataModel.GetType();
            foreach (var prop in ownType.GetProperties().Where(p => p.CanWrite))
            {
                var convertToJson = false;
                var dataModelProp = dataModelType.GetProperty(prop.Name);
                if (dataModelProp == null)
                {
                    convertToJson = true;
                    dataModelProp = dataModelType.GetProperty($"{prop.Name}Json");
                    if (dataModelProp == null)
                        continue;
                }
                if (!dataModelProp.CanRead)
                    continue;
                var sourceValue = dataModelProp.GetValue(dataModel);
                if ((prop.PropertyType == typeof(StreamImageSource)) && (sourceValue != null))
                {
                    byte[] bytes = Convert.FromBase64String((string)sourceValue);
                    MemoryStream stream = new MemoryStream(bytes);
                    sourceValue = StreamImageSource.FromStream(() => stream);
                }
                if (convertToJson)
                    sourceValue = JsonConvert.DeserializeObject((string)sourceValue, new JsonSerializerSettings()
                        {
                            TypeNameHandling = TypeNameHandling.Objects
                        });
                prop.SetValue(this, sourceValue);
            }
        }

        public BaseModel Duplicate()
        {
            var ownType = GetType();
            var newObj = Activator.CreateInstance(ownType) as BaseModel;
            foreach (var prop in ownType.GetProperties().Where(p => p.CanWrite && p.CanRead))
            {
                var sourceValue = prop.GetValue(this);
                prop.SetValue(newObj, sourceValue);
            }
            return newObj;
        }

        public BaseModel UpdatePicture(string cacheRootPath)
        {
            var propPicture = GetType().GetProperties().FirstOrDefault(p => p.Name == "Picture");
            if (propPicture == null)
                return this;
            var propPicturePath = GetType().GetProperties().FirstOrDefault(p => p.Name == "PicturePath");
            if (propPicturePath == null)
                return this;
            var path = propPicturePath.GetValue(this) as string;
            if (string.IsNullOrWhiteSpace(path))
                return this;
            path = Path.Combine(cacheRootPath, path);
            propPicture.SetValue(this, ImageSource.FromFile(path));
            return this;
        }

    }
}
