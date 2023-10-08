using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using VideoPlayerLib.Services.Database;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.Log
{
    public class Logger : ILogger
    {
        private readonly ILogDatabase logDatabase;
        private readonly ConcurrentQueue<LogEntry> logs = new System.Collections.Concurrent.ConcurrentQueue<LogEntry>();
        private BackgroundWorker worker = null;

        public string CategoryName { get; set; }

        public Logger(ILogDatabase logDatabase)
            : base()
        {
            this.logDatabase = logDatabase;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            LogEntry entry = new LogEntry()
            {
                Category = CategoryName,
                Message = state.ToString(),
                StackTrace = exception?.StackTrace,
                Type = (logLevel == LogLevel.Error) ? LogEntryType.Error : LogEntryType.Info
            };
            logs.Enqueue(entry);
            StartSaveLogs();
        }

        private void StartSaveLogs()
        {
            if (worker != null)
                return;
            worker = new BackgroundWorker();
            worker.DoWork += Worker_SaveLogs;
            worker.RunWorkerCompleted += Worker_LogsSaved;
            worker.RunWorkerAsync(0);
        }
        private async void Worker_LogsSaved(object sender, RunWorkerCompletedEventArgs e)
        {
            await Task.Delay(1000);
            worker.RunWorkerAsync((int)(e.Result));
        }
        private void Worker_SaveLogs(object sender, DoWorkEventArgs e)
        {
            int loop = (int)e.Argument;
            if (loop == int.MaxValue)
                loop = 0;
            while (logs.TryDequeue(out LogEntry entry))
                try
                {
                    logDatabase.AddLog(entry).Wait();
                    if (loop % 10 == 0)
                        ClearLogsAsync().Wait();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            e.Result = loop + 1;
        }
        private async Task ClearLogsAsync()
        {
            var logs = (await logDatabase.GetLogs())
                .Where(l => l.CreatedAt < DateTime.Now.AddDays(-3));
            foreach (var log in logs)
                await logDatabase.RemoveLog(log);
        }
    }
}
