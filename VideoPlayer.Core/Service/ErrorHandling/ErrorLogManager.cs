using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Download;

namespace VideoPlayer.Service.ErrorHandling
{
    public class ErrorLogManager: IErrorLogManager
    {
        private readonly IEnvironment environment;

        public ErrorLogManager(IEnvironment environment)
        {
            this.environment = environment;
        }

        protected string RootPath { get => environment.GetErrorLogPath(); }

        public bool HasErrors { get => Directory.GetFiles(RootPath, "*.error").Any(); }

        public IEnumerable<string> ReadErrors()
        {
            foreach (var file in Directory.GetFiles(RootPath, "*.error"))
                try
                {
                    yield return File.ReadAllText(file);
                }
                finally
                {
                    File.Delete(file);
                }
        }

        public void WriteError(Exception error)
        {
            string logPath = Path.Combine(RootPath, $"{Guid.NewGuid()}.error");
            File.WriteAllText(logPath, $"{DateTime.Now}\r\n{error}");
        }
    }
}
