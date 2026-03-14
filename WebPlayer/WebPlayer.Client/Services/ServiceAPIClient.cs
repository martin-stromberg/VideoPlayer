using WebPlayerApi.Models;
using System.Net.Http.Json;

namespace WebPlayer.Client.Services
{
    public class ServiceAPIClient: IServiceAPIClient
    {
        private IHttpClientFactory httpClientFactory;
        private const string BaseUriSources = "api/MediaSources/";

        public ServiceAPIClient(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }
        public string BaseAddress { get => CreateHttpClient().BaseAddress.ToString(); }
        protected HttpClient CreateHttpClient()
        {
            return httpClientFactory.CreateClient("My.ServerAPI");
        }
        public async Task<IEnumerable<MediaDirectory>> GetSourcesAsync()
        {
            return await CreateHttpClient()?.GetFromJsonAsync<List<MediaDirectory>>(BaseUriSources) ?? new List<MediaDirectory>();
        }
    }
}
