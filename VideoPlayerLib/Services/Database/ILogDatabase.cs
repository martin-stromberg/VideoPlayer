using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.Database
{
    public interface ILogDatabase
    {
        Task AddLog(LogEntry entry);
        Task<IEnumerable<Models.LogEntry>> GetLogs();
        Task RemoveLog(LogEntry log);
    }
}
