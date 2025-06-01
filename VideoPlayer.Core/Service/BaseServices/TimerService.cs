using Microsoft.Extensions.Logging;
using VideoPlayer.Service.Processor;

namespace VideoPlayer.Service.BaseServices
{
    public abstract class TimerService: BaseService, ITimerService
    {

        private System.Timers.Timer _Worker = null;
        private bool _Executing = false;
        private long _ExecutionSession = 0;
        private DateTime _LastExecution = DateTime.MinValue;
        private IProcessorCollection processorCollection;

        public TimerService(string name, IProcessorCollection processorCollection, ILogger logger)
            :base(logger)
        {
            Name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
            ChangeProcessorCollection(processorCollection);
        }
        public TimerService(string name, ILogger logger)
            : this(name, null, logger)
        {
            Name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
            ChangeProcessorCollection(processorCollection);
        }

        protected void ChangeProcessorCollection(IProcessorCollection processorCollection)
        {
            this.processorCollection = processorCollection;
        }

        protected TimeSpan DueTime { get; set; } = TimeSpan.FromSeconds(10);

        protected TimeSpan Period { get; set; } = TimeSpan.FromSeconds(10);
        public string Name { get; }

        public event EventHandler<EventArgs> ExecutionStarted;

        public event EventHandler<EventArgs> ExecutionFinished;
        public virtual void Start()
        {
            if (_Worker is not null) return;
            _Worker = new System.Timers.Timer(DueTime) { AutoReset = false };
            var currentExecutionSession = _ExecutionSession = DateTime.Now.Ticks;
            _LastExecution = DateTime.MinValue;              
            _Worker.Elapsed += (sender, args) =>
            {
                _Worker.Stop();
                if (_Worker is not null && _Worker.Interval != Period.TotalMilliseconds)
                    _Worker.Interval = Period.TotalMilliseconds;
                if (processorCollection is not null)
                {
                    processorCollection.Enqueue(
                        Name,
                        ExecuteTimer,
                        args,
                        (arg) =>
                        {
                            if (_Worker is not null && currentExecutionSession == _ExecutionSession)
                                _Worker.Start();
                        },
                        (arg, ex) =>
                        {
                            NotifyError(ex);
                        });
                }
                else
                {
                    ExecuteTimer(args);
                    _Worker.Start();
                }
            };            
            _Worker.Start();
        }
        protected void ForceExecute()
        {
            Task.Run(() => ExecuteTimer(null));
        }
        public virtual void Stop()
        {
            if (_Worker is not null)
            {
                _Worker.Dispose();
                _Worker = null;
            }
        }
        protected virtual void CheckActive()
        {
            if (_Worker is null)
                throw new CancelledException();
        }

        private void ExecuteTimer(object args)
        {
            if (!_Executing)
                try
                {
                    _Executing = true;

                    ExecutionStarted?.Invoke(this, EventArgs.Empty);
                    try
                    {
                        ExecuteTimerSync();
                        ExecuteTimerAsync().Wait();
                    }
                    finally
                    {
                        ExecutionFinished?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    NotifyError(ex);
                }
                finally
                {
                    _Executing = false;
                }
        }

        protected virtual void ExecuteTimerSync() { }
        protected virtual Task ExecuteTimerAsync()
        {
            return Task.CompletedTask;
        }

    }
}
