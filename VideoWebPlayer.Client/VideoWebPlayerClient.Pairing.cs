using Microsoft.Extensions.Logging;
using System.Net;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client
{
    // Device pairing, QR bootstrap, session renewal and logout. Physically separated from
    // VideoWebPlayerClient.cs (see the class summary there) while sharing its HTTP infrastructure:
    // SendAndDeserializeAsync is used directly instead of the Http*Async helpers, so these calls are
    // never impersonated by InternalVideoWebPlayerClient - a device session is not a web session.
    public partial class VideoWebPlayerClient
    {
        /// <summary>Name of the gate-key header the pairing and session endpoints expect.</summary>
        private const string DeviceTokenHeaderName = "X-API-Key";

        // Serializes the session renewal, so several requests that run into 401 at the same time cause
        // exactly one call to RefreshAsync and the others reuse its result.
        private readonly SemaphoreSlim refreshGate = new(1, 1);

        private string? _deviceToken;
        private string? _deviceRefreshToken;

        // Counts the completed session renewals of this instance. SendWithReauthorizationAsync remembers
        // the value BEFORE it sends a request; a 401 whose renewal then finds a different value was
        // answered with a session that has meanwhile been replaced, so the request is simply repeated
        // instead of renewing a second time. Reading the counter at the start of the renewal would be too
        // late: a request whose 401 handling only begins after another renewal has finished would already
        // see the new session and renew again.
        private long refreshGeneration;

        // The InvalidOperationException of the most recent failed renewal of this instance (revoked
        // device, expired or already used renewal proof), so the cause survives the automatic path and can
        // be handed to the caller as the InnerException of the 401 it gets. Cleared on every successful
        // renewal. Several requests of one client instance always fail for the same reason - they share
        // device token and renewal proof - so the most recent failure describes all of them.
        private Exception? lastRenewalFailure;

        /// <summary>
        /// The device token of this client instance (never static, so every circuit/app instance keeps
        /// its own). While it is set, every request carries it as the <c>X-API-Key</c> gate key.
        /// </summary>
        public string? DeviceToken
        {
            get => _deviceToken;
            set => _deviceToken = value;
        }

        /// <summary>
        /// The refresh token of the current device session, stored per client instance. It is set from the
        /// decrypted bootstrap payload and replaced by every successful <see cref="RefreshAsync"/> (the
        /// server rotates it). Without it no automatic renewal happens.
        /// </summary>
        public string? DeviceRefreshToken
        {
            get => _deviceRefreshToken;
            set => _deviceRefreshToken = value;
        }

        // Waiting time before a request is repeated after a successful renewal, so a server that just
        // rotated the session is not hit again in the same instant. Randomized to spread out several
        // clients renewing at once.
        private static TimeSpan RetryBackoff => TimeSpan.FromMilliseconds(100 + Random.Shared.Next(0, 50));

        /// <summary>
        /// Puts the current <see cref="DeviceToken"/> on a single request as its <c>X-API-Key</c> gate
        /// key. Deliberately set per request instead of on <see cref="HttpClient.DefaultRequestHeaders"/>:
        /// a gate key another component configured on the same <see cref="HttpClient"/> (the web
        /// interface does exactly that) stays untouched and keeps working for every request this client
        /// sends without a device token, and no shared header collection is mutated while other requests
        /// of the same client are in flight.
        /// </summary>
        /// <param name="request">The request to add the gate key to.</param>
        private void ApplyDeviceTokenHeader(HttpRequestMessage request)
        {
            var deviceToken = _deviceToken;
            if (string.IsNullOrWhiteSpace(deviceToken))
                return;

            // A header set on the request takes precedence over the client's default header, so this
            // replaces the foreign gate key for this one request only.
            request.Headers.Remove(DeviceTokenHeaderName);
            request.Headers.TryAddWithoutValidation(DeviceTokenHeaderName, deviceToken);
        }

        /// <summary>
        /// Builds and sends one request, carrying the device token of this instance as its gate key.
        /// Every HTTP helper of this client goes through here, so no verb can miss the header, and a
        /// repeated attempt after a session renewal builds a fresh <see cref="HttpRequestMessage"/>
        /// (a request message can only be sent once) while reusing the same body content.
        /// </summary>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="content">The request body, or <see langword="null"/> for a request without one.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The server's response.</returns>
        private Task<HttpResponseMessage> SendRequestAsync(
            HttpMethod method, string endPoint, HttpContent? content, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(method, endPoint) { Content = content };
            ApplyDeviceTokenHeader(request);
            return httpClient.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Redeems a one-time pairing code for a device token. The answer carries the token encrypted
        /// against the client's ECDH public key from <paramref name="request"/>; decrypting it and
        /// assigning <see cref="DeviceToken"/> is up to the caller, which owns the private key.
        /// </summary>
        /// <param name="request">The pairing code, the client's ECDH public key and the device name.</param>
        /// <returns>The server's public key and the encrypted device token.</returns>
        public Task<PairingExchangeResponse> PairingExchangeAsync(PairingExchangeRequest request)
        {
            const string endPoint = "api/pairing/exchange";
            var content = CreateJsonContent(request);
            return SendAndDeserializeAsync<PairingExchangeResponse>(
                endPoint, "POST", () => SendRequestAsync(HttpMethod.Post, endPoint, content), skipReauthorize: true);
        }

        /// <summary>
        /// Redeems a bootstrap ticket from a QR code (or its short-code alias) for the encrypted trio of
        /// device token, user session and refresh token. As with the pairing exchange, decrypting the
        /// payload and assigning <see cref="DeviceToken"/>, <see cref="DeviceRefreshToken"/> and the
        /// bearer token is up to the caller, which owns the private key.
        /// </summary>
        /// <param name="request">The ticket, the client's ECDH public key and the device name.</param>
        /// <returns>The server's public key and the encrypted payload.</returns>
        public Task<PairingBootstrapResponse> PairingBootstrapAsync(PairingBootstrapRequest request)
        {
            const string endPoint = "api/pairing/bootstrap";
            var content = CreateJsonContent(request);
            return SendAndDeserializeAsync<PairingBootstrapResponse>(
                endPoint, "POST", () => SendRequestAsync(HttpMethod.Post, endPoint, content), skipReauthorize: true);
        }

        /// <summary>
        /// Renews the device session: sends the stored <see cref="DeviceRefreshToken"/> together with the
        /// <see cref="DeviceToken"/> gate key, applies the new bearer token and stores the rotated refresh
        /// token. Refused renewals (revoked device, expired or already used refresh token) end in an
        /// <see cref="InvalidOperationException"/> instead of another attempt.
        /// </summary>
        /// <returns>The new session: bearer token, its expiry and the rotated refresh token.</returns>
        public async Task<RefreshTokenResponse> RefreshAsync()
        {
            const string endPoint = "api/auth/refresh";
            if (string.IsNullOrWhiteSpace(_deviceRefreshToken))
                throw new InvalidOperationException(
                    "Die Sitzung kann nicht erneuert werden, weil kein Erneuerungsnachweis vorliegt. Bitte das Gerät neu koppeln.");

            var content = CreateJsonContent(new RefreshTokenRequest { RefreshToken = _deviceRefreshToken });
            RefreshTokenResponse response;
            try
            {
                response = await SendAndDeserializeAsync<RefreshTokenResponse>(
                    endPoint, "POST", () => SendRequestAsync(HttpMethod.Post, endPoint, content), skipReauthorize: true);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                throw new InvalidOperationException(
                    "Die Sitzung konnte nicht erneuert werden: Das Gerät wurde widerrufen oder der Erneuerungsnachweis ist abgelaufen. Bitte das Gerät neu koppeln.",
                    ex);
            }

            _deviceRefreshToken = response.RefreshToken;
            SetAuthorizationToken(new AuthorizationToken { token = response.Token, expires = response.Expires });
            lastRenewalFailure = null;
            Interlocked.Increment(ref refreshGeneration);
            return response;
        }

        /// <summary>
        /// Ends the device session: revokes the refresh token on the server and drops the bearer token,
        /// the refresh token and the device token of this instance. The local values are dropped even
        /// when the server call fails, so nothing of the ended session is left behind on a shared device;
        /// the server error is reported afterwards.
        /// </summary>
        public async Task LogoutAsync()
        {
            const string endPoint = "api/auth/logout";
            try
            {
                if (string.IsNullOrWhiteSpace(_deviceRefreshToken))
                    return;

                var content = CreateJsonContent(new RefreshTokenRequest { RefreshToken = _deviceRefreshToken });
                var response = await SendWithReauthorizationAsync(
                    endPoint, () => SendRequestAsync(HttpMethod.Post, endPoint, content), skipReauthorize: true);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        string.IsNullOrWhiteSpace(body) ? $"Failed to POST from {endPoint}: {response.ReasonPhrase}" : body,
                        null,
                        response.StatusCode);
                }
            }
            finally
            {
                _deviceRefreshToken = null;
                _deviceToken = null;
                lastRenewalFailure = null;
                ClearAuthorizationToken();
            }
        }

        /// <summary>
        /// Renews the session once for a request that was answered with 401, under
        /// <see cref="refreshGate"/> so parallel 401 answers share a single renewal.
        /// </summary>
        /// <param name="refreshGenerationBeforeRequest">
        /// The value of <see cref="refreshGeneration"/> at the moment the failed request was sent. A
        /// different value now means another renewal has completed in the meantime, so this request ran
        /// against a session that no longer exists and only needs to be repeated.
        /// </param>
        /// <returns><c>true</c> if the request may be repeated; <c>false</c> if no renewal is possible.</returns>
        private async Task<bool> TryRenewDeviceSessionAsync(long refreshGenerationBeforeRequest)
        {
            if (string.IsNullOrWhiteSpace(_deviceRefreshToken))
                return false;

            await refreshGate.WaitAsync();
            try
            {
                // The session this request used has already been replaced - no second renewal needed.
                if (Interlocked.Read(ref refreshGeneration) != refreshGenerationBeforeRequest)
                    return true;

                await RefreshAsync();
            }
            catch (InvalidOperationException ex)
            {
                lastRenewalFailure = ex;
                Logger?.LogWarning(ex, "Die Sitzung konnte nicht erneuert werden; die Anfrage wird nicht wiederholt.");
                return false;
            }
            finally
            {
                refreshGate.Release();
            }

            await Task.Delay(RetryBackoff);
            return true;
        }
    }
}
