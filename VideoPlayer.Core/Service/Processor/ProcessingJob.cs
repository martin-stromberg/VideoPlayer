namespace VideoPlayer.Service.Processor
{
    public class ProcessingJob
    {
        public enum JobStatus { Waiting, Running, Finished, Failed }
        public ProcessingJob(
            Action<object> actionMethod, 
            object argument,
            Action<object> continueMethod = null,
            Action<object, Exception> errorMethod = null)
        {
            ActionMethod = actionMethod;
            ContinueMethod = continueMethod;
            ErrorMethod = errorMethod ?? new Action<object, Exception>((a, e) => { continueMethod?.Invoke(a); });
            Argument = argument;
            Status = JobStatus.Waiting;
        }
        private void Clear()
        {
            ActionMethod = null;
            ContinueMethod = null;
            ErrorMethod = null;
            Argument = null;
            LastError = null;
        }
        public Action<object> ActionMethod { get; private set; }
        public Action<object> ContinueMethod { get; private set; }
        public Action<object, Exception> ErrorMethod { get; private set; }
        public object Argument { get; private set; }
        private JobStatus _Status = JobStatus.Waiting;

        public Exception LastError { get; private set; }

        public JobStatus Status 
        { 
            get => _Status;
            private set
            {
                var oldStatus = _Status;
                _Status = value;
                if (oldStatus != value)
                    StatusChanged?.Invoke(this, value);
            }
        } 

        private void Failed(Exception ex)
        {
            LastError = ex;
            Status = JobStatus.Failed;
            ErrorMethod?.Invoke(Argument, ex);
            Clear();
        }

        private void Finished()
        {
            Status = JobStatus.Finished;
            ContinueMethod?.Invoke(Argument);
            Clear();
        }

        internal void Run()
        {
            Status = JobStatus.Running;
            try
            {
                ActionMethod.Invoke(Argument);
                Finished();
            }
            catch(Exception ex) 
            {
                Failed(ex);
            }            
        }

        public event EventHandler<JobStatus> StatusChanged;
    }
}
