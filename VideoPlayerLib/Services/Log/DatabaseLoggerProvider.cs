using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database;

namespace VideoPlayerLib.Services.Log
{
    public class DatabaseLoggerProvider : ILoggerProvider
    {
        public DatabaseLoggerProvider(IServiceProvider serviceProvider)
            :base()
        {
            this.serviceProvider = serviceProvider;
        }
        private ConcurrentDictionary<string, ILogger> _loggers = new ConcurrentDictionary<string, ILogger>();
        private readonly IServiceProvider serviceProvider;

        public ILogger CreateLogger(string categoryName)
        {
            var logger = _loggers.GetOrAdd(categoryName, new Logger(serviceProvider.GetService<ILogDatabase>())) as Logger;
            logger.CategoryName = categoryName;
            return logger;
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
