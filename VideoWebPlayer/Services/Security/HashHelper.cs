using System.Security.Cryptography;
using System.Text;

namespace VideoWebPlayer.Services.Security
{
    /// <summary>
    /// Shared hashing helper for secrets that are only persisted as hashes
    /// (device tokens, pairing codes).
    /// </summary>
    internal static class HashHelper
    {
        /// <summary>
        /// Computes the uppercase hexadecimal SHA-256 hash of the UTF-8 encoded value.
        /// </summary>
        /// <param name="value">The plaintext value.</param>
        /// <returns>The SHA-256 hash as uppercase hex string.</returns>
        public static string Sha256Hex(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
