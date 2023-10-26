using System;
using System.Linq;

namespace VideoPlayer.Services.MediaLibrary.Maintenance
{
    public enum DataCleaningMode
    {

        Complete

    }

    public interface IDataCleaner
    {

        DataCleaningMode Mode { get; set; }

        Task RunAsync();

    }
}
