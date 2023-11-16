using System;
using System.Linq;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Services.Database
{
    public interface IJobDatabase
    {

        Task<IEnumerable<DownloadJob>> GetDownloadJobs();

        Task AddDownloadJob(DownloadJob job);

        Task RemoveDownloadJob(DownloadJob job);

        Task<bool> DownloadJobsExist();

    }
}
