using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Apex.Core.Models;

namespace Apex.Infrastructure.Packs;

/// <summary>
/// AES-256-CBC encryption/decryption for .apexpack files.
/// Envelope includes SHA256 integrity hash of plaintext.
/// </summary>
public static class PackCrypto
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Generate a new 256-bit encryption key as a base64 string.
    /// </summary>
    public static string GenerateKey()
    {
        var key = new byte[32]; // 256 bits
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// Encrypt an ApexPack into an EncryptedPackEnvelope.
    /// </summary>
    public static EncryptedPackEnvelope Encrypt(ApexPack pack, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var plaintext = JsonSerializer.Serialize(pack, _json);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var hash = SHA256.HashData(plaintextBytes);

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Data = ciphertext || tag (16-byte GCM tag appended)
        var data = new byte[ciphertext.Length + tag.Length];
        ciphertext.CopyTo(data, 0);
        tag.CopyTo(data, ciphertext.Length);

        return new EncryptedPackEnvelope
        {
            Format = "apexpack-v2",
            PackId = pack.PackId,
            Cipher = "AES-256-GCM",
            Iv = Convert.ToBase64String(nonce),
            Hash = Convert.ToHexString(hash).ToLowerInvariant(),
            Data = Convert.ToBase64String(data)
        };
    }

    /// <summary>
    /// Decrypt an EncryptedPackEnvelope back to an ApexPack.
    /// Validates SHA256 integrity hash.
    /// </summary>
    public static ApexPack Decrypt(EncryptedPackEnvelope envelope, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var iv = Convert.FromBase64String(envelope.Iv);
        var data = Convert.FromBase64String(envelope.Data);

        byte[] plaintextBytes;
        switch (envelope.Format)
        {
            case "apexpack-v2":
            {
                const int tagLength = 16;
                if (data.Length < tagLength)
                    throw new InvalidOperationException("Pack data is truncated.");
                var ciphertext = data[..^tagLength];
                var tag = data[^tagLength..];
                plaintextBytes = new byte[ciphertext.Length];
                using var aes = new AesGcm(key, tagLength);
                try
                {
                    aes.Decrypt(iv, ciphertext, tag, plaintextBytes);
                }
                catch (AuthenticationTagMismatchException)
                {
                    throw new InvalidOperationException("Pack integrity check failed — wrong key, or data has been corrupted or tampered with.");
                }
                break;
            }
            case "apexpack-v1":
            {
                // Legacy CBC packs exported before v2
                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var decryptor = aes.CreateDecryptor();
                plaintextBytes = decryptor.TransformFinalBlock(data, 0, data.Length);
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported pack format: {envelope.Format}");
        }

        // Verify plaintext hash (defense-in-depth for v2, sole integrity check for v1)
        var expectedHash = Convert.FromHexString(envelope.Hash);
        var actualHash = SHA256.HashData(plaintextBytes);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new InvalidOperationException("Pack integrity check failed — data may be corrupted or tampered with.");

        var pack = JsonSerializer.Deserialize<ApexPack>(plaintextBytes, _json)
            ?? throw new InvalidOperationException("Failed to deserialize pack content.");

        return pack;
    }

    /// <summary>
    /// Serialize an EncryptedPackEnvelope to JSON string.
    /// </summary>
    public static string SerializeEnvelope(EncryptedPackEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, _json);

    /// <summary>
    /// Deserialize an EncryptedPackEnvelope from JSON string.
    /// </summary>
    public static EncryptedPackEnvelope DeserializeEnvelope(string json) =>
        JsonSerializer.Deserialize<EncryptedPackEnvelope>(json, _json)
        ?? throw new InvalidOperationException("Failed to deserialize pack envelope.");

    /// <summary>
    /// Serialize an ApexPack to unencrypted JSON (for export/validation).
    /// </summary>
    public static string SerializePack(ApexPack pack) =>
        JsonSerializer.Serialize(pack, _json);

    /// <summary>
    /// Deserialize an ApexPack from unencrypted JSON.
    /// </summary>
    public static ApexPack DeserializePack(string json) =>
        JsonSerializer.Deserialize<ApexPack>(json, _json)
        ?? throw new InvalidOperationException("Failed to deserialize pack.");

    /// <summary>
    /// Get machine ID for license activation (deterministic, hardware-based).
    /// </summary>
    public static string GetMachineId()
    {
        var raw = $"{Environment.MachineName}:{Environment.UserName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
