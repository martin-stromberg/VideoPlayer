using WebPlayerApi.Models;

namespace WebPlayer.Client.Services
{
    public interface IServiceAPIClient
    {
        string BaseAddress { get; }
        Task<IEnumerable<MediaDirectory>> GetSourcesAsync();
    }
}
