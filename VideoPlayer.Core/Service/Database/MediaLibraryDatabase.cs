using SQLite;
using System;
using System.Diagnostics;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Database
{
    public class MediaLibraryDatabase: IMediaLibraryDatabase
    {

        private readonly IDatabaseSettings _Settings;
        private bool _SchemaUpdated = false;

        public MediaLibraryDatabase(IDatabaseSettings settings)
        {
            _Settings = settings;
        }

        private SQLiteConnection _Connection = null;

        protected SQLiteConnection Connection
        {
            get
            {
                if (_Connection is null)
                    _Connection = CreateConnection();
                return _Connection;
            }
        }

        protected SQLiteConnection CreateConnection()
        {
            return new SQLiteConnection(_Settings.DatabasePath,
                                        SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex,
                                        true);
        }
        public IDatabaseSettings Settings { get => _Settings; }

        protected Type[] ModelTypes
        {
            get
            {
                var baseType = typeof(BaseDataModel);
                return baseType.Assembly
                               .GetTypes()
                               .Where(t => t != baseType)
                               .Where(t => !t.IsAbstract)
                               .Where(t => t.IsAssignableTo(baseType))
                               .ToArray();
            }
        }

        public void UpdateSchema()
        {
            if (_SchemaUpdated)
                return;
            _SchemaUpdated = true;
            var connection = CreateConnection();
            try
            {
                connection.CreateTables(CreateFlags.None, ModelTypes);
            }
            finally
            {
                connection.Close();
            }
        }

        public bool IsEmpty()
        {
            var connection = CreateConnection();
            try
            {
                foreach (var tableMapping in connection.TableMappings.Where(m => m.PK is not null))
                {
                    if (connection.Query(tableMapping, $"select * from {tableMapping.TableName} LIMIT 1;").Any())
                        return false;
                }
                return true;
            }
            finally
            {
                connection.Close();
            }
        }

        public T Get<T>(long id) where T: BaseDataModel
        {
            return (T)Get(typeof(T), id);
        }

        public BaseDataModel Get(Type elementType, long id)
        {
            BaseDataModel dbModel = null;
            ExecuteConnectionAction((connection) =>
            {
                var tableMapping = connection.TableMappings.FirstOrDefault(tm => tm.TableName == elementType.Name);
                try
                {
                    dbModel = connection.Get(id, tableMapping) as BaseDataModel;
                }
                catch(InvalidOperationException ex)
                {
                    dbModel = null;
                    Debug.WriteLine(ex.Message);
                }
            });
            return dbModel;
        }

        public void Clear()
        {
            var connection = CreateConnection();
            try
            {
                connection.BeginTransaction();
                try
                {
                    foreach (var tableMapping in connection.TableMappings)
                        connection.DropTable(tableMapping);
                    connection.CreateTables(CreateFlags.None, ModelTypes);
                    connection.Commit();
                }
                catch
                {
                    connection.Rollback();
                    throw;
                }
            }
            finally
            {
                connection.Close();
            }
        }

        public T AddOrUpdate<T>(T dbModel) where T: BaseDataModel
        {
            ExecuteConnectionAction((connection) =>
            {
                if (dbModel.Id == 0)
                {
                    dbModel.CreatedAt = DateTime.Now;
                    connection.Insert(dbModel);
                }
                else
                {
                    connection.Update(dbModel);
                }
            });
            return (T)dbModel;
        }

        private void ExecuteConnectionAction(Action<SQLiteConnection> callback)
        {
            var connection = Connection;// CreateConnection();
            try
            {
                callback(connection);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }
            finally
            {
                // connection.Close();
            }
        }

        public IEnumerable<BaseDataModel> GetAll(Type modelType, params KeyValuePair<string, object>[] args)
        {
            IEnumerable<BaseDataModel> dbModels = null;
            ExecuteConnectionAction((connection) =>
            {
                var tableMapping = connection.TableMappings.FirstOrDefault(tm => tm.TableName == modelType.Name);
                var query = $"SELECT * FROM {tableMapping.TableName}";
                if (args.Any())
                {
                    query += $" where {string.Join(" and ", args.Select(a =>
                    { 
                        if (a.Value is Array)
                            return $"({string.Join(" or ", ((Array)a.Value).Cast<object>().Select(v => $"\"{a.Key}\" = ?"))})";
                        return $"\"{a.Key}\" = ?";
                    }))}";
                    var param = args?.SelectMany(a =>
                    {
                        if (a.Value is Array)
                            return ((Array)a.Value).Cast<object>();
                        return new object[] { a.Value };
                    })
                                     .ToArray();
                    dbModels = connection.Query(tableMapping, query, param).Cast<BaseDataModel>();
                }
                else
                    dbModels = connection.Query(tableMapping, query).Cast<BaseDataModel>();
            });
            return dbModels;
        }
        public IEnumerable<BaseDataModel> GetAll(Type modelType, params Filter[] args)
        {
            IEnumerable<BaseDataModel> dbModels = null;
            ExecuteConnectionAction((connection) =>
            {
                var tableMapping = connection.TableMappings.FirstOrDefault(tm => tm.TableName == modelType.Name);
                var query = $"SELECT * FROM {tableMapping.TableName}";
                if (args.Any())
                {
                    query += $" where {string.Join(" and ", args
                        .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                        .Select(a =>
                        {
                            if (a.Value is Array)
                                return $"({string.Join(" or ", ((Array)a.Value)
                                    .Cast<object>().Select(v =>
                                    {
                                        switch (a.Type)
                                        {
                                            case FilterType.Equal:
                                                return $"\"{a.Name}\" = ?";
                                            case FilterType.Contains:
                                                return $"\"{a.Name}\" like '%{v.ToString()}%'";
                                            default:
                                                return $"\"{a.Name}\" = ?";
                                        }                                        
                                    }))})";
                            switch(a.Type)
                            {
                                case FilterType.Equal:
                                    return $"\"{a.Name}\" = ?";
                                case FilterType.Contains:
                                    return $"\"{a.Name}\" like '%{a.Value.ToString()}%'";
                                default:
                                    return $"\"{a.Name}\" = ?";
                            }
                        }))}";
                    var param = args?
                        .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                        .Where(a => a.Type != FilterType.Contains)
                        .SelectMany(a =>
                        {
                            if (a.Value is Array)
                                return ((Array)a.Value).Cast<object>();
                            return new object[] { a.Value };
                        })
                        .ToArray();
                    dbModels = connection.Query(tableMapping, query, param).Cast<BaseDataModel>();
                }
                else
                    dbModels = connection.Query(tableMapping, query).Cast<BaseDataModel>();
            });
            return dbModels;
        }

        public IEnumerable<T> GetAll<T>(params KeyValuePair<string, object>[] args) where T: BaseDataModel
        {
            return GetAll(typeof(T), args).Cast<T>();
        }

        public IEnumerable<T> GetAll<T>(int offset, int count, params KeyValuePair<string, object>[] args)
            where T: BaseDataModel
        {
            return GetAll(typeof(T), args)
                .OrderBy(rec => rec.Name)
                .Skip(offset)
                .Take(count)
                .Cast<T>();
        }

        public IEnumerable<T> GetAll<T>(int offset, int count, params Filter[] args)
            where T : BaseDataModel
        {
            return GetAll(typeof(T), args)
                .OrderBy(rec => rec.Name)
                .Skip(offset)
                .Take(count)
                .Cast<T>();
        }

        public IEnumerable<T> GetAll<T>(int offset, int count, string orderFieldName, bool ascending, params Filter[] args)
            where T : BaseDataModel
        {
            var orderProp = typeof(T).GetProperty(orderFieldName);
            if (ascending)
                return GetAll(typeof(T), args)
                    .OrderBy(rec => orderProp.GetValue(rec))
                    .Skip(offset)
                    .Take(count)
                    .Cast<T>();
            else
                return GetAll(typeof(T), args)
                    .OrderByDescending(rec => orderProp.GetValue(rec))
                    .Skip(offset)
                    .Take(count)
                    .Cast<T>();
        }

        public bool Delete<T>(T entryToDelete) where T : BaseDataModel
        {
            return Connection.Delete(entryToDelete) == 1;
        }

        public void Truncate<T>()
        {
            Connection.BeginTransaction();
            try
            {
                Connection.DropTable<T>();
                Connection.CreateTable<T>(CreateFlags.None);
                Connection.Commit();
            }
            catch
            {
                Connection.Rollback();
                throw;
            }
        }
    }

    public struct Filter
    {
        public string Name;
        public FilterType Type;
        public object Value;
    }
    public enum FilterType { Equal, Contains }

    public interface IMediaLibraryDatabase
    {
        IDatabaseSettings Settings { get; }
        void UpdateSchema();

        bool IsEmpty();

        void Clear();

        T Get<T>(long id) where T: BaseDataModel;

        BaseDataModel Get(Type elementType, long id);

        T AddOrUpdate<T>(T dbModel) where T: BaseDataModel;

        IEnumerable<T> GetAll<T>(params KeyValuePair<string, object>[] args) where T: BaseDataModel;

        IEnumerable<T> GetAll<T>(int offset, int count, params KeyValuePair<string, object>[] args)
            where T: BaseDataModel;
        IEnumerable<T> GetAll<T>(int offset, int count, params Filter[] args)
            where T : BaseDataModel;
        IEnumerable<T> GetAll<T>(int offset, int count, string orderFieldName, bool ascending, params Filter[] args)
            where T : BaseDataModel;

        IEnumerable<BaseDataModel> GetAll(Type modelType, params KeyValuePair<string, object>[] args);
        bool Delete<T>(T entryToDelete) where T : BaseDataModel;
        void Truncate<T>();
    }
}
