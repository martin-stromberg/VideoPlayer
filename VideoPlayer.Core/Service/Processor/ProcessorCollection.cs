using Syncfusion.XlsIO.Parser.Biff_Records;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static VideoPlayer.Service.Processor.ProcessingJob;

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
    public class Processor
    {
        private ConcurrentQueue<ProcessingJob> _Jobs = new ConcurrentQueue<ProcessingJob>();
        private object _WorkerLock = new object();
        private bool _Working = false;
        
        public ProcessingJob Enqueue(Action<object> method, object argument = null, Action<object> continueMethod = null, Action<object, Exception> errorMethod = null)
        {
            try
            {
                var job = new ProcessingJob(method, argument, continueMethod, errorMethod);
                _Jobs.Enqueue(job);
                return job;
            }
            finally
            {
                Start();
            }
        }

        private void Start()
        {
            lock(_WorkerLock)
            {
                if (_Working)
                    return;
                if (_Jobs.Count == 0)
                    return;
                _Working = true;
                ThreadPool.QueueUserWorkItem(new WaitCallback(ExecuteNextJob));
            }
        }
        private void Continue()
        {
            lock (_WorkerLock)
            {
                _Working = false;
                Start();
            }
        }

        private void ExecuteNextJob(object arg)
        {
            try
            {
                while (_Jobs.TryDequeue(out var job))
                    job.Run();
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                Continue();
            }
        }
    }
    public interface IProcessorCollection
    {
        void Enqueue(string name, Action<object> method, object arg);
        void Enqueue(string name, Action<object> method, object arg, Action<object> finishMethod);
        void Enqueue(string name, Action<object> method, object arg, Action<object> finishMethod, Action<object, Exception> failedMethod);
    }
    public class ProcessorCollection: IProcessorCollection
    {
        public ProcessorCollection() 
        { 
        }
        private ConcurrentDictionary<string, Processor> _Processors = new ConcurrentDictionary<string, Processor>();
        private Processor GetProcessor(string name)
        {
            if (_Processors.ContainsKey(name))
                return _Processors[name];
            return _Processors.AddOrUpdate(name, new Processor(), (k, existing) => existing);
        }
        public void Enqueue(string name, Action<object> method, object arg, Action<object> finishMethod, Action<object, Exception> failedMethod )
        {
            var processor = GetProcessor(name);
            processor.Enqueue(method, arg, finishMethod, failedMethod);
        }

        public void Enqueue(string name, Action<object> method, object arg)
        {
            Enqueue(name, method, arg, null, null);
        }

        public void Enqueue(string name, Action<object> method, object arg, Action<object> finishMethod)
        {
            Enqueue(name, method, arg, finishMethod, null);
        }
    }
}
