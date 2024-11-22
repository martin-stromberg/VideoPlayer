using System.Net;
using System.Runtime.Serialization;

namespace VideoPlayer.Extensions
{
    public static class HttpClientExt
    {
        public static async Task DownloadAsync(
            this HttpClient client,
            string requestUri,
            Stream destination,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            // Get the http headers first to examine the content length
            using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpClientRequestException($"Request failed with code {response.StatusCode}.", response.StatusCode);
                var contentLength = response.Content.Headers.ContentLength;

                using (var download = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    // Ignore progress reporting when no progress reporter was 
                    // passed or when the content length is unknown
                    if ((progress == null) || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination);
                        return;
                    }

                    // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                    var relativeProgress = new Progress<long>(totalBytes =>
                                                              progress.Report(((float)totalBytes) / contentLength.Value));

                    // Use extension method to report progress while downloading
                    await download.CopyToAsync(destination, relativeProgress, cancellationToken, 81920);
                    progress.Report(1);
                }
            }
        }
    }
}
