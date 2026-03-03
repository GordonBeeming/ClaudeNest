using System.Security.Cryptography;
using System.Text;

namespace ClaudeNest.Agent.Config;

/// <summary>
/// Encrypts/decrypts secrets at rest using AES-256-GCM with a machine-derived key.
/// The key is derived via HKDF from machine-specific data + a random salt.
/// Not unbreakable by root, but prevents casual reading of plaintext secrets.
/// </summary>
public static class CredentialProtector
{
    private const int NonceSize = 12; // AES-GCM standard
    private const int TagSize = 16;   // AES-GCM standard
    private const int KeySize = 32;   // AES-256

    public static string Encrypt(string plaintext, byte[] salt)
    {
        var key = DeriveKey(salt);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: nonce + tag + ciphertext, all base64-encoded together
        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        ciphertext.CopyTo(result, NonceSize + TagSize);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string encryptedBase64, byte[] salt)
    {
        var key = DeriveKey(salt);
        var data = Convert.FromBase64String(encryptedBase64);

        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid encrypted data");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var ciphertext = data.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public static byte[] GenerateSalt()
    {
        var salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    private static byte[] DeriveKey(byte[] salt)
    {
        // Machine-specific input keying material
        var ikm = $"{Environment.MachineName}|{Environment.UserName}|ClaudeNest";
        var ikmBytes = Encoding.UTF8.GetBytes(ikm);

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikmBytes,
            KeySize,
            salt,
            Encoding.UTF8.GetBytes("ClaudeNest.Credentials"));
    }
}
