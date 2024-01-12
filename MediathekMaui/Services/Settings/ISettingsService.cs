
namespace Mediathek.Services.Settings
{
    public interface ISettingsService
    {

        Task InitializeAsync();

        Models.Settings Current { get; }

    }
}
