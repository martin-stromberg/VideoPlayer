using Microsoft.Extensions.Logging;

namespace VideoPlayer.Service.BaseServices
{
    public abstract class TimerService: BaseService, ITimerService
    {

        private System.Timers.Timer _Worker = null;
        private bool _Executing = false;

        public TimerService(ILogger logger)
            :base(logger)
        { }

        protected TimeSpan DueTime { get; set; } = TimeSpan.FromSeconds(10);

        protected TimeSpan Period { get; set; } = TimeSpan.FromSeconds(10);

        public event EventHandler<EventArgs> ExecutionStarted;

        public event EventHandler<EventArgs> ExecutionFinished;

        public virtual void Start()
        {
            Stop();

            _Worker = new System.Timers.Timer(DueTime) { AutoReset = true };
            _Worker.Elapsed += (sender, args) =>
            {
                if (_Worker is not null)
                    _Worker.Interval = Period.TotalMilliseconds;
                ExecuteTimer(args);
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
        protected void CheckActive()
        {
            if (_Worker is null)
                throw new CancelledException();
        }

        private async void ExecuteTimer(object args)
        {
            if (!_Executing)
                try
                {
                    _Executing = true;

                    ExecutionStarted?.Invoke(this, EventArgs.Empty);
                    try
                    {
                        await ExecuteTimerAsync();
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

        protected abstract Task ExecuteTimerAsync();

    }
}
