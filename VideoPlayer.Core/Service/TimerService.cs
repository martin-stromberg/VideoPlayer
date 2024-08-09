namespace VideoPlayer.Service
{
    public abstract class TimerService: BaseService
    {

        private Timer _Worker = null;

        public TimerService() { }

        protected TimeSpan DueTime { get; set; } = TimeSpan.FromSeconds(10);

        protected TimeSpan Period { get; set; } = TimeSpan.FromSeconds(10);

        public virtual void Start()
        {
            Stop();
            _Worker = new Timer((args) => ExecuteTimer(args), null, DueTime, Period);
        }

        public virtual void Stop()
        {
            if (_Worker is not null)
            {
                _Worker.Dispose();
                _Worker = null;
            }
        }

        private async void ExecuteTimer(object args)
        {
            try
            {
                await ExecuteTimerAsync();
            }
            catch { }
        }

        protected abstract Task ExecuteTimerAsync();

    }
}
