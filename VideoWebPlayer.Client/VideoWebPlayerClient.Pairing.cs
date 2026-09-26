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

        // Guards the mutation of the shared default request headers.
        private readonly object deviceTokenHeaderGate = new();

        private string? _deviceToken;
        private string? _deviceRefreshToken;

        /// <summary>
        /// Whether this instance put the <c>X-API-Key</c> header on the underlying
        /// <see cref="HttpClient"/>, so clearing <see cref="DeviceToken"/> removes only that header value
        /// and leaves a gate key another component configured on the same client untouched.
        /// </summary>
        private bool deviceTokenHeaderApplied;

        /// <summary>
        /// The device token of this client instance (never static, so every circuit/app instance keeps
        /// its own). While it is set, every request carries it as the <c>X-API-Key</c> gate key.
        /// </summary>
        public string? DeviceToken
        {
            get => _deviceToken;
            set
            {
                _deviceToken = value;
                ApplyDeviceTokenHeader();
            }
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
        /// Puts the current <see cref="DeviceToken"/> on the underlying <see cref="HttpClient"/> as the
        /// <c>X-API-Key</c> header, or removes the header again when the device token was cleared. Called
        /// before every request, so the header is present no matter which HTTP verb is used.
        /// </summary>
        private void ApplyDeviceTokenHeader()
        {
            lock (deviceTokenHeaderGate)
            {
                if (string.IsNullOrWhiteSpace(_deviceToken))
                {
                    if (deviceTokenHeaderApplied)
                    {
                        httpClient.DefaultRequestHeaders.Remove(DeviceTokenHeaderName);
                        deviceTokenHeaderApplied = false;
                    }
                    return;
                }

                if (deviceTokenHeaderApplied
                    && httpClient.DefaultRequestHeaders.TryGetValues(DeviceTokenHeaderName, out var current)
                    && current.Contains(_deviceToken, StringComparer.Ordinal))
                    return;

                httpClient.DefaultRequestHeaders.Remove(DeviceTokenHeaderName);
                httpClient.DefaultRequestHeaders.Add(DeviceTokenHeaderName, _deviceToken);
                deviceTokenHeaderApplied = true;
            }
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
                endPoint, "POST", () => httpClient.PostAsync(endPoint, content), skipReauthorize: true);
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
                endPoint, "POST", () => httpClient.PostAsync(endPoint, content), skipReauthorize: true);
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
                    endPoint, "POST", () => httpClient.PostAsync(endPoint, content), skipReauthorize: true);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                throw new InvalidOperationException(
                    "Die Sitzung konnte nicht erneuert werden: Das Gerät wurde widerrufen oder der Erneuerungsnachweis ist abgelaufen. Bitte das Gerät neu koppeln.",
                    ex);
            }

            _deviceRefreshToken = response.RefreshToken;
            SetAuthorizationToken(new AuthorizationToken { token = response.Token, expires = response.Expires });
            return response;
        }

        /// <summary>
        /// Ends the device session: revokes the refresh token on the server and drops the bearer token,
        /// the refresh token and the device token of this instance.
        /// </summary>
        public async Task LogoutAsync()
        {
            const string endPoint = "api/auth/logout";
            if (!string.IsNullOrWhiteSpace(_deviceRefreshToken))
            {
                var content = CreateJsonContent(new RefreshTokenRequest { RefreshToken = _deviceRefreshToken });
                var response = await SendWithReauthorizationAsync(endPoint, () => httpClient.PostAsync(endPoint, content), skipReauthorize: true);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        string.IsNullOrWhiteSpace(body) ? $"Failed to POST from {endPoint}: {response.ReasonPhrase}" : body,
                        null,
                        response.StatusCode);
                }
            }

            _deviceRefreshToken = null;
            DeviceToken = null;
            ClearAuthorizationToken();
        }

        /// <summary>
        /// Renews the session once for a request that was answered with 401, under
        /// <see cref="refreshGate"/> so parallel 401 answers share a single renewal.
        /// </summary>
        /// <returns><c>true</c> if the request may be repeated; <c>false</c> if no renewal is possible.</returns>
        private async Task<bool> TryRenewDeviceSessionAsync()
        {
            if (string.IsNullOrWhiteSpace(_deviceRefreshToken))
                return false;

            var tokenBeforeWaiting = AuthorizationToken;
            await refreshGate.WaitAsync();
            try
            {
                // Another 401 already renewed the session while this call waited for the gate.
                if (!string.Equals(tokenBeforeWaiting, AuthorizationToken, StringComparison.Ordinal))
                    return true;

                await RefreshAsync();
            }
            catch (InvalidOperationException ex)
            {
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
