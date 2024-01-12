using Mediathek.Services.Database.Models;
using System;
using System.Linq;

namespace Mediathek.Services.Database
{
    public interface IJobDatabase
    {

        Task<IEnumerable<DownloadJob>> GetDownloadJobs();

        Task AddDownloadJob(DownloadJob job);

        Task RemoveDownloadJob(DownloadJob job);

        Task<bool> DownloadJobsExist();

    }
}
