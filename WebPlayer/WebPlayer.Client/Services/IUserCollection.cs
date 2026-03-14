using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using WebPlayerApi.Models;

namespace WebPlayer.Data
{
    public interface IUserCollection
    {
        IEnumerable<IApplicationUser> GetAll();
        void ChangeAccess(MediaDirectory dir, IApplicationUser[] users);
        bool HasAccess(IApplicationUser user, MediaDirectory? source);
    }

    public interface IMediaDirectoryAccessApi
    {
        Task<IEnumerable<IApplicationUser>> GetAccessUsersAsync();
        Task<IEnumerable<IApplicationUser>> GetAccessUsersAsync(string sourceId);
        Task SetAccessUsersAsync(string directoryId, string[] userIds);
    }
    public class MediaDirectoryAccessApi : IMediaDirectoryAccessApi
    {
        private readonly HttpClient _http;
        public MediaDirectoryAccessApi(IHttpClientFactory http) => _http = http.CreateClient("Own");

        public async Task<IEnumerable<IApplicationUser>> GetAccessUsersAsync()
        {
            try
            {
                var response = await _http.GetStringAsync("api/mediadirectories/access");
                return JsonSerializer.Deserialize<IEnumerable<AppUser>>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })?.Cast<IApplicationUser>() ?? Enumerable.Empty<IApplicationUser>();
            }
            catch (Exception ex)
            { 
                throw new ApplicationException($"Error fetching access users from {_http.BaseAddress}", ex);
            }
        }

        public async Task<IEnumerable<IApplicationUser>> GetAccessUsersAsync(string sourceId)
        {
            return (await _http.GetFromJsonAsync<IEnumerable<AppUser>>(
                 $"api/mediadirectories/access?sourceId={sourceId}", new JsonSerializerOptions
                 {
                     PropertyNameCaseInsensitive = true
                 })).Cast<IApplicationUser>()
               ?? Enumerable.Empty<IApplicationUser>();
        }
        public async Task SetAccessUsersAsync(string directoryId, string[] userIds)
        {
            var res = await _http.PostAsJsonAsync(
                $"api/mediadirectories/access?directoryId={directoryId}", userIds);
            res.EnsureSuccessStatusCode();
        }
    }
}
