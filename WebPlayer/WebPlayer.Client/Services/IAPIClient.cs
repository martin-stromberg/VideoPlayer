using static System.Net.WebRequestMethods;
using System.Net.Http;
using WebPlayerApi.Models;
using Microsoft.AspNetCore.Http;

namespace WebPlayer.Client.Services
{

    public interface IAPIClient
    {
        string BaseAddress { get; }
        Task<IEnumerable<MediaDirectory>> GetSourcesAsync();
        Task<MediaDirectory> GetSourceAsync(string id);
        Task<bool> AddSource(MediaDirectory source);
        Task<bool> EditSource(MediaDirectory source);
        Task<bool> RemoveSource(string id);
        Task<bool> ForceReloadSourceAsync(string sourceName);

        Task<IEnumerable<MediaItemDto>> GetMediaItems(string sourceId, int offset, int count);
        Task<MediaItemDetailsDto> GetMediaItemAsync(string id);
    }
}
