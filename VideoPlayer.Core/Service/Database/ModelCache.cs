using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Database
{
    public class ModelCache<T> where T: BaseDataModel
    {

        public class CacheElementEventArgs: EventArgs
        {

            public CacheElementEventArgs(CacheElement element)
            {
                Element = element;
            }

            public CacheElement Element { get; private set; }

        }

        public class CacheElement
        {
            private int _Counter = 0;
            public T Item { get; set; }

            public DateTime LastUpdate { get; set; }

            public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();
            public BaseServiceModel ServiceModel { get; internal set; }
            public int Counter { get => _Counter; }
            public bool Deactivated { get; set; }
            public DateTime ReleaseDue { get; private set; }

            private void OnRelease()
            {
                ReleaseRequested?.Invoke(this, EventArgs.Empty);
            }
            public event EventHandler ReleaseRequested;

            internal void IncreaseCounter()
            {
                Deactivated = false;
                Interlocked.Increment(ref _Counter);
            }
            internal void DecreaseCounter()
            {
                var newValue = Interlocked.Decrement(ref _Counter);
                if (newValue == 0)
                    Deactivate();
            }

            private void Deactivate()
            {                
                ReleaseDue = DateTime.Now.AddSeconds(10);
                Deactivated = true;
            }

            public void CheckRelease()
            {
                if (!Deactivated) return;
                if (_Counter > 0)
                {
                    Deactivated = false;
                    return;
                }
                if (ReleaseDue < DateTime.Now)
                    OnRelease();
            }
        }

        private ConcurrentDictionary<long, CacheElement> _cache = new ConcurrentDictionary<long, CacheElement>();

        private readonly IMediaLibraryDatabase _Database;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILogger logger;

        public ModelCache(
            IMediaLibraryDatabase database, 
            IMediaLibrary mediaLibrary,
            Type type,
            ILogger logger)
        {
            _Database = database;
            _MediaLibrary = mediaLibrary;
            ElementType = type;
            this.logger = logger;
        }

        public TimeSpan MaxCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

        public Type ElementType { get; set; }

        public void Clear() { _cache.Clear(); }

        private T Get(long id)
        {
            logger?.LogTrace($"{GetType()}: Get {typeof(T).Name} {id}");
            var element = _cache.ContainsKey(id) ? _cache[id] : (new CacheElement());
            element.IncreaseCounter();
            if (element.LastUpdate.Add(MaxCacheDuration) < DateTime.Now)
                Update(element, id);            
            return element.Item;
        }

        public T2 GetServiceModel<T2>(long id) where T2: BaseServiceModel
        {
            logger?.LogTrace($"{GetType()}: GetServiceModel {typeof(T).Name} {id}");
            var element = _cache.ContainsKey(id) ? _cache[id] : (new CacheElement());
            element.IncreaseCounter();
            if (element.LastUpdate.Add(MaxCacheDuration) < DateTime.Now)
                Update(element, id);
            if (element.ServiceModel is not null)
                if (!(element.ServiceModel is T2))
                    return null;
            if (element.ServiceModel is not null)
            {
                logger?.LogTrace($"{GetType()}: GetServiceModel.Result = {typeof(T).Name} {id} (Instance {element.ServiceModel.InstanceId})");
                return (T2)element.ServiceModel;
            }

            var model = element.Item;
            var serviceModel = BaseServiceModel.FromDatabaseModel(model);
            if (serviceModel is not null)
                UpdateServiceModelData(serviceModel, element.Data);
            element.ServiceModel = serviceModel;
            if (element.ServiceModel is not null)
                if (!(element.ServiceModel is T2))
                    return null;
            if (element.ServiceModel is not null)
                return (T2)element.ServiceModel;
            return null;
        }

        private void UpdateServiceModelData(BaseServiceModel serviceModel, Dictionary<string, object> data)
        {
            var modelType = serviceModel.GetType();
            logger?.LogTrace($"{GetType()}: UpdateServiceModelData {modelType.Name} {serviceModel.Id} (Instance {serviceModel.InstanceId})");
            foreach (var dataEntry in data)
            {
                var prop = modelType.GetProperty(dataEntry.Key);
                if (prop is null)
                    continue;
                var value = dataEntry.Value;
                prop.SetValue(serviceModel, value, null);
            }

            foreach (var prop in modelType
                .GetProperties()
                .Where(p => p.CanWrite)
                .Where(p =>
                {
                    if (p.PropertyType.IsAssignableTo(typeof(BaseServiceModel)))
                        return true;
                    return false;
                }))
            {
                var IdRefFieldProp = modelType.GetProperty($"{prop.PropertyType.Name}Id")
                        ?? modelType.GetProperty($"{prop.Name}Id");
                var refProp = prop.PropertyType.GetProperty($"{modelType.Name}Id");
                if (refProp is not null)
                {
                    var serviceChild = GetRefServiceModel(prop.PropertyType, new KeyValuePair<string, object>(refProp.Name, serviceModel.Id));
                    prop.SetValue(serviceModel, serviceChild, null);
                }
                else if (IdRefFieldProp is not null)
                {
                    var value = (long)IdRefFieldProp.GetValue(serviceModel);                    
                    if (value != 0)
                    {
                        var attr = prop.PropertyType.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute;
                        BaseServiceModel serviceChild = null;
                        if (attr is null)
                        {
                            var derivedTypes = prop.PropertyType.Assembly.GetTypes().Where(t => t.IsAssignableTo(prop.PropertyType)).ToArray();
                            var children = derivedTypes
                                .Select(dT =>
                                {
                                    attr = dT.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute;
                                    if (attr is null) return new KeyValuePair<Type, BaseServiceModel>(null, null);
                                    var derivedChild = GetRefServiceModel(dT, new KeyValuePair<string, object>(nameof(BaseDataModel.Id), value));
                                    if (derivedChild is null) return new KeyValuePair<Type, BaseServiceModel>(null, null);
                                    return new KeyValuePair<Type, BaseServiceModel>(dT, derivedChild);
                                })
                                .Where(result => result.Key is not null)
                                .FirstOrDefault();
                            serviceChild = children.Value;
                        }
                        else
                        {
                            serviceChild = GetRefServiceModel(prop.PropertyType, new KeyValuePair<string, object>(nameof(BaseDataModel.Id), value));
                        }
                        if (serviceChild is not null)
                            prop.SetValue(serviceModel, serviceChild, null);
                    }
                }
            }

            var dataModelType = (modelType.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute)?.DataModelType;
            var isArray = false;
            var isObservableCollection = false;
            var isIList = false;
            foreach (var prop in modelType
                .GetProperties()
                .Where(p =>
                {
                    isArray = p.PropertyType.IsAssignableTo(typeof(BaseServiceModel[]));
                    isObservableCollection = p.PropertyType.IsAssignableTo(typeof(ObservableCollection<BaseServiceModel>));
                    isIList = (p.PropertyType.IsGenericType && p.PropertyType.IsAssignableTo(typeof(IList)) && (p.PropertyType.GenericTypeArguments.FirstOrDefault().IsAssignableTo(typeof(BaseServiceModel))));
                    return isArray || isObservableCollection || isIList;
                }))
            {
                var refType = isArray ? prop.PropertyType.GetElementType() 
                    : isObservableCollection ? prop.PropertyType.GenericTypeArguments.FirstOrDefault()
                    : isIList ? prop.PropertyType.GenericTypeArguments.FirstOrDefault()
                    : null;
                var attr = refType.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute;
                if (attr is null)
                    continue;
                var refProp = attr.DataModelType
                    .GetProperty($"{dataModelType.Name}Id") 
                            ?? attr.DataModelType
                    .GetProperty($"{modelType.Name}Id");
                if (refProp is null)
                    continue;
                var children = _Database.GetAll(attr.DataModelType, new KeyValuePair<string, object>(refProp.Name, serviceModel.Id)).ToArray();
                var destValue = isArray ? Activator.CreateInstance(prop.PropertyType, children.Length)
                        : isObservableCollection ? prop.GetValue(serviceModel) as ObservableCollection<BaseServiceModel>
                        : isIList ? prop.GetValue(serviceModel) as IList
                        : null;
                if (isIList || isObservableCollection)
                    (destValue as IList).Clear();
                if (!children.Any())
                {
                    if (isArray)
                        prop.SetValue(serviceModel, destValue, null);
                }
                else
                {
                    for (int idx = children.GetLowerBound(0); idx<= children.GetUpperBound(0); idx++)
                    {
                        var targetChild = Activator.CreateInstance(refType, children[idx]) as BaseServiceModel;
                        UpdateServiceModelData(targetChild, new Dictionary<string, object>());
                        if (isArray)
                            ((Array)destValue).SetValue(targetChild, idx);
                        else if (isObservableCollection)
                            ((ObservableCollection<BaseServiceModel>)destValue).Add(targetChild);
                        else if (isIList)
                            ((IList)destValue).Add(targetChild);
                    }
                    if (isArray)
                        prop.SetValue(serviceModel, destValue, null);
                }
            }
        }

        private BaseServiceModel GetRefServiceModel(Type modelType, KeyValuePair<string, object> args)
        {
            var attr = modelType.GetCustomAttribute(typeof(DataModelReferenceAttribute)) as DataModelReferenceAttribute;
            if (attr is null)
                return null;
            var id = args.Key == nameof(BaseServiceModel.Id) ? (long)args.Value : 0;
            var idMethod = _MediaLibrary.GetType().GetMethods()
                .Where(m =>
                {
                    if (m.ReturnType != modelType) return false;
                    var paramList = m.GetParameters();
                    if (paramList.Length != 1) return false;
                    if (paramList[0].ParameterType != typeof(Int64)) return false;
                    return true;
                })
                .OrderBy(m => m.Name)
                .FirstOrDefault();
            if (idMethod is null)
                return null;
            if (id == 0)
            {
                var child = _Database.GetAll(attr.DataModelType, args).FirstOrDefault();
                id = child.Id;
            }
            try
            {
                return idMethod.Invoke(_MediaLibrary, new object[] { id }) as BaseServiceModel;
            }
            catch
            {
                throw;
            }
        }

        public IEnumerable<T> GetAll()
        {
            return _cache
                .Values
                .Where(e => e.LastUpdate.Add(MaxCacheDuration) < DateTime.Now)
                .Select(e => e.Item);
        }

        private void Update(CacheElement element, long id)
        {
            var storedObject = _Database.Get(ElementType, id);
            if (element.Item is null)
                element.Item = (T)storedObject;
            else
                ((BaseDataModel)element.Item).Update(storedObject);
            element.LastUpdate = DateTime.Now;
            if (!_cache.ContainsKey(id))
                _cache.AddOrUpdate(id, element, (id, existing) => element);
            OnElementUpdated(new CacheElementEventArgs(element));
        }

        public event EventHandler<EventArgs> ElementUpdated;

        protected virtual void OnElementUpdated(EventArgs e)
        {
            ElementUpdated?.Invoke(this, e);
        }

        internal void Update(BaseServiceModel model, BaseDataModel dbModel) 
        {
            var element = _cache.ContainsKey(dbModel.Id) ? _cache[dbModel.Id] : (new CacheElement());
            element.ReleaseRequested += Element_ReleaseRequested;
            element.Item = (T)dbModel;
            element.ServiceModel = model;
            element.LastUpdate = DateTime.Now;
            if (!_cache.ContainsKey(dbModel.Id))
                _cache.AddOrUpdate(dbModel.Id, element, (id, existing) => element);
            OnElementUpdated(new CacheElementEventArgs(element));
        }

        private void Element_ReleaseRequested(object sender, EventArgs e)
        {
            var element = sender as CacheElement;
            if (element.Item is null)
                return;
            if (_cache.Remove(element.Item.Id, out element))
            {
                element.ReleaseRequested -= Element_ReleaseRequested;
                element.ServiceModel = null;
                element.Item = null;
            }
        }
        internal void Remove(BaseServiceModel entry)
        {
            var element = _cache.ContainsKey(entry.Id) ? _cache[entry.Id] : null;
            if (element is null) return;
            _cache.TryRemove(entry.Id, out element);
        }
        internal void Release(BaseServiceModel entry)
        {
            var element = _cache.ContainsKey(entry.Id) ? _cache[entry.Id] : null;
            if (element is null) return;
            element.DecreaseCounter();
        }

        internal void Hold(BaseServiceModel entry)
        {
            var element = _cache.ContainsKey(entry.Id) ? _cache[entry.Id] : null;
            if (element is null) return;
            element.IncreaseCounter();
        }

        internal void CheckReleases()
        {
            foreach (var element in _cache.Values.ToList())
                element.CheckRelease();
        }
    }
}
