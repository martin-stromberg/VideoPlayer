using System;
using System.Linq;

namespace Mediathek.Services.Database
{
    public interface ISettingsDataSource
    {

        Task<Models.Settings> GetSettingsAsync();

        Task<Models.Settings> UpdateSettingsAsync(Models.Settings settings);

    }
}
