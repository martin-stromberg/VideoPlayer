using WebPlayerApi.Models;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace WebPlayer.Client.Services
{
    public class APIClient : IAPIClient
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly AuthenticationStateProvider authProvider;
        private const string BaseUriSources = "api/MediaSources/";
        private const string UriExtensionReload = "{0}/reload";
        private const string BaseUriMediaItems = "api/MediaItems/";
        private const string UriExtensionMediaItemDetails = "details/{0}";

        public APIClient(IHttpClientFactory httpClientFactory, AuthenticationStateProvider authProvider)
        {
            this.httpClientFactory = httpClientFactory;
            this.authProvider = authProvider;
        }
        public string BaseAddress { get => CreateHttpClient().BaseAddress.ToString(); }
        protected async Task<bool> IsAuthenticated(){

            return  ((await authProvider.GetAuthenticationStateAsync())?.User?.Identity?.IsAuthenticated ?? false);
        }

        public async Task<IEnumerable<MediaDirectory>> GetSourcesAsync()
        {
            if (!await IsAuthenticated())
                return new MediaDirectory[0];
            return await CreateHttpClient()?.GetFromJsonAsync<List<MediaDirectory>>(BaseUriSources) ?? new List<MediaDirectory>();
        }

        public async Task<MediaDirectory> GetSourceAsync(string id)
        {
            if (!await IsAuthenticated())
                return null;
            return await CreateHttpClient()?.GetFromJsonAsync<MediaDirectory>($"{BaseUriSources}{id}") ?? null;
        }

        public async Task<bool> AddSource(MediaDirectory source)
        {
            if (!await IsAuthenticated())
                return false;
            var response = await CreateHttpClient()?.PostAsJsonAsync<MediaDirectory>($"{BaseUriSources}", source);
            if (!response.IsSuccessStatusCode)
                throw new ApplicationException(await response.Content.ReadAsStringAsync());
            return true;
        }
        public async Task<bool> EditSource(MediaDirectory source)
        {
            if (!await IsAuthenticated())
                return false;
            var response = await CreateHttpClient()?.PutAsJsonAsync<MediaDirectory>($"{BaseUriSources}{source.Id}", source);
            if (!response.IsSuccessStatusCode)
                throw new ApplicationException(await response.Content.ReadAsStringAsync());
            return true;
        }
        public async Task<bool> RemoveSource(string id)
        {
            if (!await IsAuthenticated())
                return false;
            var response = await CreateHttpClient()?.DeleteAsync($"{BaseUriSources}{id}");
            if (!response.IsSuccessStatusCode)
                throw new ApplicationException(await response.Content.ReadAsStringAsync());
            return true;
        }

        protected HttpClient CreateHttpClient()
        {
            return httpClientFactory.CreateClient("My.ServerAPI");
        }
        public async Task<bool> ForceReloadSourceAsync(string sourceId)
        {
            var response = await CreateHttpClient().PostAsync($"{BaseUriSources}{string.Format(UriExtensionReload, sourceId)}", new StringContent(""));
            return response.IsSuccessStatusCode;
        }

        public async Task<IEnumerable<MediaItemDto>> GetMediaItems(string sourceId, int offset, int count)
        {
            var client = CreateHttpClient();
            int attempt = 100;
            while (attempt > 0)
            {
                attempt--;
                var response = await client.GetFromJsonAsync<MediaItemDto[]>($"{BaseUriMediaItems}{sourceId}?offset={offset}&count={count}");
                return response;                
            }
            return new MediaItemDto[0];
        }

        public async Task<MediaItemDetailsDto> GetMediaItemAsync(string id)
        {
            var client = CreateHttpClient();
            int attempt = 100;
            while (attempt > 0)
            {
                attempt--;
                var response = await client.GetFromJsonAsync<MediaItemDetailsDto>($"{BaseUriMediaItems}{string.Format(UriExtensionMediaItemDetails, id)}");
                return response;
            }
            return null;
        }
    }
}
