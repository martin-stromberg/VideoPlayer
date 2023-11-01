using System;
using System.Linq;

namespace VideoPlayer.Services.Database
{
    public interface ISettingsDataSource
    {

        Task<Models.Settings> GetSettingsAsync();

        Task<Models.Settings> UpdateSettingsAsync(Models.Settings settings);

    }
}
