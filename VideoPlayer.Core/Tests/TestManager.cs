using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests
{
    public enum TestStatus
    {

        Unknown,
        Running,
        Success,
        Failed

    }

    public class TestFinishedEventArgs: EventArgs
    {

        public TestFinishedEventArgs(TestStatus status, string message = "")
        {
            Result = status;
            Message = message;
        }

        public TestStatus Result { get; }

        public string Message { get; }

    }

    public class TestManager
    {

        public ObservableCollection<BaseTest> Tests { get; } = new ObservableCollection<BaseTest>();

        public event EventHandler<TestFinishedEventArgs> Finished;

        public void Start(bool forceAll)
        {
            Thread TestThread = new Thread(new ThreadStart(async () =>
            {
                string result = string.Empty;
                TestStatus status = TestStatus.Unknown;
                try
                {
                    Init();
                    await Run(forceAll);
                    status = TestStatus.Success;
                }
                catch (Exception ex)
                {
                    status = TestStatus.Failed;
                    result = $"{ex}\r\n";
                }
                finally
                {
                    foreach (var test in Tests)
                        if (test.Status == TestStatus.Failed)
                        {
                            status = TestStatus.Failed;
                            result += $"{test.GetType().Name}: {test.Message}\r\n";
                        }

                    Finished?.Invoke(this, new TestFinishedEventArgs(status, result));
                }
            }))
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            TestThread.Start();
        }

        protected virtual void Init()
        {
            var testType = typeof(BaseTest);

            Tests.Clear();
            var testTypes = testType.Assembly
                                    .GetTypes()
                                    .Where(t => !t.IsAbstract)
                                    .Where(t => t.IsAssignableTo(testType));
            foreach (var currType in testTypes)
                Tests.Add(Activator.CreateInstance(currType) as BaseTest);
        }

        private async Task Run(bool forceAll = false)
        {
            foreach (var test in Tests.OrderBy(t =>
            {
                var attr = t.GetType().GetCustomAttribute<DisabledAttribute>();
                if (attr is null) return 0;
                else return 1;
            }))
                if (forceAll || test.GetType().GetCustomAttribute<DisabledAttribute>() is null)
                    await test.Run();
        }

    }
}
