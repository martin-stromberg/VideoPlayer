using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.ErrorHandling
{
    public class ErrorLogManager: IErrorLogManager
    {        
        public bool HasErrors { get => Directory.GetFiles(Environment.CurrentDirectory, "*.error").Any(); }

        public IEnumerable<string> ReadErrors()
        {
            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "*.error"))
                try
                {
                    yield return File.ReadAllText(file);
                }
                finally
                {
                    File.Delete(file);
                }
        }
    }
}
