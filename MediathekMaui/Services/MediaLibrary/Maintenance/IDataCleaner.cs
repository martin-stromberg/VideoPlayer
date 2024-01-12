using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.Maintenance
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
