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

        // SHA256 integrity hash of plaintext
        var hash = SHA256.HashData(plaintextBytes);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        return new EncryptedPackEnvelope
        {
            Format = "apexpack-v1",
            PackId = pack.PackId,
            Cipher = "AES-256-CBC",
            Iv = Convert.ToBase64String(aes.IV),
            Hash = Convert.ToHexString(hash).ToLowerInvariant(),
            Data = Convert.ToBase64String(ciphertext)
        };
    }

    /// <summary>
    /// Decrypt an EncryptedPackEnvelope back to an ApexPack.
    /// Validates SHA256 integrity hash.
    /// </summary>
    public static ApexPack Decrypt(EncryptedPackEnvelope envelope, string base64Key)
    {
        if (envelope.Format != "apexpack-v1")
            throw new InvalidOperationException($"Unsupported pack format: {envelope.Format}");

        var key = Convert.FromBase64String(base64Key);
        var iv = Convert.FromBase64String(envelope.Iv);
        var ciphertext = Convert.FromBase64String(envelope.Data);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        // Verify integrity
        var actualHash = Convert.ToHexString(SHA256.HashData(plaintextBytes)).ToLowerInvariant();
        if (actualHash != envelope.Hash)
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
