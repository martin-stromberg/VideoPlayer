using SQLite;
using System;
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

        protected SQLiteConnection CreateConnection()
        {
            return new SQLiteConnection(_Settings.DatabasePath, true);
        }

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
                    if (connection.Query(tableMapping, $"select * from {tableMapping.TableName};").Any())
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
            BaseDataModel dbModel = null;
            ExecuteConnectionAction((connection) =>
            {
                var tableMapping = connection.TableMappings.FirstOrDefault(tm => tm.TableName == typeof(T).Name);
                dbModel = connection.Get(id, tableMapping) as BaseDataModel;
            });
            return (T)dbModel;
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
                    connection.Insert(dbModel);
                else
                    connection.Update(dbModel);
            });
            return (T)dbModel;
        }

        private void ExecuteConnectionAction(Action<SQLiteConnection> callback)
        {
            var connection = CreateConnection();
            try
            {
                callback(connection);
            }
            finally
            {
                connection.Close();
            }
        }

    }

    public interface IMediaLibraryDatabase
    {

        void UpdateSchema();

        bool IsEmpty();

        void Clear();

        T Get<T>(long id) where T: BaseDataModel;

        T AddOrUpdate<T>(T dbModel) where T: BaseDataModel;

        IEnumerable<T> GetAll<T>() where T: BaseDataModel;

    }
}
