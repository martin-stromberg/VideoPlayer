using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.PlatformConfiguration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Processor;

namespace VideoPlayer.Service.Log
{
    public class DatabaseLoggerProvider : ILoggerProvider
    {
        private DatabaseLogger logger = new DatabaseLogger(null);
        public DatabaseLoggerProvider()
            :base()
        {
            
        }
        public void Init(IMediaLibrary mediaLibrary, IProcessorCollection processorCollection)
        {
            logger?.Init(mediaLibrary, processorCollection);
        }

        public ILogger CreateLogger(string categoryName)
        {
            logger?.Start();
            return logger;
        }

        public void Dispose()
        {
            logger?.Stop();
        }
    }
}
