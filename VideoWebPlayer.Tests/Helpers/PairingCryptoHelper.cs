using System.Security.Cryptography;
using System.Text;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Client-side counterpart of the pairing exchange crypto: derives the shared AES key
/// from the client ECDH key and the server public key and decrypts the device token.
/// </summary>
internal static class PairingCryptoHelper
{
    public static string DecryptToken(ECDiffieHellman clientKey, string serverPublicKeyBase64, string encryptedTokenBase64)
    {
        using var serverKey = ECDiffieHellman.Create();
        serverKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(serverPublicKeyBase64), out _);
        var aesKey = clientKey.DeriveKeyFromHmac(serverKey.PublicKey, HashAlgorithmName.SHA256, null, null, null);
        var payload = Convert.FromBase64String(encryptedTokenBase64);
        var nonce = payload[..12];
        var tag = payload[^16..];
        var ciphertext = payload[12..^16];
        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(aesKey, 16))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        return Encoding.UTF8.GetString(plaintext);
    }
}
