using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Services.Database
{
    public interface ILogDatabase
    {

        Task AddLog(LogEntry entry);

        Task<IEnumerable<LogEntry>> GetLogs();

        Task RemoveLog(LogEntry log);

    }
}
