using MyVideoPlayer.ViewModels.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database;
using VideoPlayerLib.Services.Database.Models;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Logs
{
    public class LogListViewModel : NavigationContentViewModel
    {
        private readonly ILogDatabase logDatabase;

        public LogListViewModel(ILogDatabase logDatabase, IServiceProvider serviceProvider) 
            : base(null, serviceProvider)
        {
            this.logDatabase = logDatabase;
        }
        public override void OnAppeared()
        {
            base.OnAppeared();
            cancel = false;
            StartLoadLogs();
        }
        public override void OnDisappeared()
        {
            base.OnDisappeared();
            cancel = true;
        }

        private void StartLoadLogs()
        {
            Items.Clear();
            Task.Run(() =>
            {                
                firstEntry = null;
                LoadLogsAsync();
            });
        }

        private async void LoadLogsAsync()
        {
            foreach (var entry in (await logDatabase.GetLogs()).OrderByDescending(entry => entry.CreatedAt))
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    if (cancel)
                        break;
                    StartComplete(entry);
                    await MainThread.InvokeOnMainThreadAsync(() => { try { Items.Add(new LogEntryBoxViewModel(entry)); } catch { } });
                    await Task.Delay(10);
                }
                catch { }
        }

        private LogEntry firstEntry = null;
        private void StartComplete(LogEntry entry)
        {
            if (firstEntry != null)
                return;
            firstEntry = entry;
            Task.Run(() => CompleteNext());
        }

        private bool cancel = false;
        private async void CompleteNext()
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            foreach (var entry in (await logDatabase.GetLogs()).Where(entry => entry.Id > firstEntry.Id).OrderBy(entry => entry.Id))
                try
                {
                    if (cancel)
                        break;
                    await MainThread.InvokeOnMainThreadAsync(() => { try { Items.Insert(0, new LogEntryBoxViewModel(entry)); } catch { } });
                    await Task.Delay(10);
                    firstEntry = entry;
                }
                catch
                {

                }
            if (!cancel)
                Task.Run(() => CompleteNext());
        }

        internal override Task ReadMediaCollection(VideoPlayerLib.Services.MediaLibrary.Models.MediaSource source)
        {
            return Task.CompletedTask;
        }
        internal override Task ReadMediaItems(MediaItemCollection collection)
        {
            return Task.CompletedTask;
        }
    }
}
