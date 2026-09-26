using System.Security.Cryptography;

namespace VideoWebPlayer.Services.Security
{
    /// <summary>
    /// Shared crypto helpers for the pairing channel: client ECDH key import
    /// and AES-256-GCM payload encryption in the contract wire layout.
    /// </summary>
    internal static class PairingCrypto
    {
        private const int GcmNonceSize = 12;
        private const int GcmTagSize = 16;

        /// <summary>
        /// Imports a client ECDH public key (Base64 SubjectPublicKeyInfo) and verifies the nistP256 curve.
        /// </summary>
        /// <param name="clientPublicKeyBase64">The Base64-encoded SubjectPublicKeyInfo blob.</param>
        /// <returns>The imported key, or <c>null</c> when the blob is unusable or not nistP256.</returns>
        public static ECDiffieHellman? TryImportClientPublicKey(string clientPublicKeyBase64)
        {
            var key = ECDiffieHellman.Create();
            try
            {
                key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(clientPublicKeyBase64), out _);
                // Der Parameterexport kann fuer importierbare, aber exotische Blobs
                // (z. B. explizit parametrisierte Kurven) ebenfalls fehlschlagen —
                // deshalb innerhalb des try/catch, damit jeder nicht verwendbare
                // Schluessel kontrolliert als ungueltiger Request endet.
                if (key.ExportParameters(false).Curve.Oid?.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
                {
                    key.Dispose();
                    return null;
                }
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException or PlatformNotSupportedException)
            {
                key.Dispose();
                return null;
            }

            return key;
        }

        /// <summary>
        /// Encrypts a payload with AES-256-GCM and returns the contract wire layout
        /// Base64(12-byte nonce | ciphertext | 16-byte tag).
        /// </summary>
        /// <param name="aesKey">The shared AES key.</param>
        /// <param name="plaintext">The plaintext bytes.</param>
        /// <returns>The Base64-encoded encrypted payload.</returns>
        public static string EncryptPayload(byte[] aesKey, byte[] plaintext)
        {
            var nonce = RandomNumberGenerator.GetBytes(GcmNonceSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[GcmTagSize];
            using (var aes = new AesGcm(aesKey, GcmTagSize))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            var payload = new byte[nonce.Length + ciphertext.Length + tag.Length];
            nonce.CopyTo(payload, 0);
            ciphertext.CopyTo(payload, nonce.Length);
            tag.CopyTo(payload, nonce.Length + ciphertext.Length);
            return Convert.ToBase64String(payload);
        }
    }
}
