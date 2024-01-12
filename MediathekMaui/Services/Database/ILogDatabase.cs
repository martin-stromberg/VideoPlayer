using Mediathek.Services.Database.Models;

namespace Mediathek.Services.Database
{
    public interface ILogDatabase
    {

        Task AddLog(LogEntry entry);

        Task<IEnumerable<LogEntry>> GetLogs();

        Task RemoveLog(LogEntry log);

    }
}
