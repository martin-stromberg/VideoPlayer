using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Processor;

namespace VideoPlayer.Service.Device
{
    public class MemoryInfo : INotifyPropertyChanged
    {
        public MemoryInfo() 
        {
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


        public long TotalMemory { get => GetProperty<long>(); set => SetProperty(value); }
        public long TotalAllocatedBytes { get => GetProperty<long>(); set => SetProperty(value); }
    }
    public interface IMemoryInformation
    {
        MemoryInfo Info { get; }
    }
    public class MemoryInformation : TimerService, IMemoryInformation
    {
        public MemoryInformation(IProcessorCollection processorCollection) 
            : base("", processorCollection, null)
        {
            Start();DueTime = TimeSpan.FromSeconds(10); Period = TimeSpan.FromSeconds(10);
        }

        public MemoryInfo Info { get; } = new MemoryInfo();

        protected override Task ExecuteTimerAsync()
        {
            Info.TotalMemory = GC.GetTotalMemory(false);
            Info.TotalAllocatedBytes = GC.GetTotalAllocatedBytes(false);
            return Task.CompletedTask;
        }
    }
}
