using System.Text.Json;
using Fewshot.Core.Models;
using Fewshot.Infrastructure.Packs;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();

try
{
    return command switch
    {
        "new" => NewPack(args),
        "validate" => Validate(args),
        "encrypt" => Encrypt(args),
        "decrypt" => Decrypt(args),
        "keygen" => Keygen(),
        "machine-id" => MachineId(),
        _ => PrintUsage()
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

int PrintUsage()
{
    Console.WriteLine("""
    fewshot-pack — Fewshot Pack Tool

    Usage:
      fewshot-pack new <pack-id> <name> <project> [-o output.json]
      fewshot-pack validate <file.json>
      fewshot-pack encrypt <file.json> --key <base64-key> [-o output.fewshotpack]
      fewshot-pack decrypt <file.fewshotpack> --key <base64-key> [-o output.json]
      fewshot-pack keygen
      fewshot-pack machine-id

    Commands:
      new         Scaffold a blank pack JSON template
      validate    Check that a pack JSON is structurally valid
      encrypt     Wrap a plaintext pack in AES-256-CBC envelope
      decrypt     Unwrap an encrypted pack back to plaintext
      keygen      Generate a new 256-bit encryption key
      machine-id  Print this machine's deterministic ID
    """);
    return 1;
}

int NewPack(string[] args)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: fewshot-pack new <pack-id> <name> <project> [-o output.json]");
        return 1;
    }

    var pack = new FewshotPack
    {
        PackId = args[1],
        Name = args[2],
        TargetProject = args[3],
        Description = $"Pack template for {args[2]}",
        Author = Environment.UserName,
        Version = "1.0.0",
        CreatedAt = DateTime.UtcNow,
        Memories =
        [
            new PackMemory
            {
                Summary = "Example: Solved X by doing Y",
                Solution = "Applied Y approach which resolved the issue",
                Tags = "example,template"
            }
        ],
        Preferences =
        [
            new PackPreference
            {
                Category = "coding_style",
                Key = "example_preference",
                Value = "Use descriptive variable names",
                ConfidenceScore = 0.7
            }
        ],
        AntiPatterns =
        [
            new PackAntiPattern
            {
                Pattern = "Example anti-pattern to avoid",
                Reason = "Causes maintenance issues",
                Language = "csharp"
            }
        ]
    };

    var json = PackCrypto.SerializePack(pack);
    var outputPath = GetArg(args, "-o") ?? $"{args[1]}.fewshotpack.json";
    File.WriteAllText(outputPath, json);
    Console.WriteLine($"Created pack template: {outputPath}");
    return 0;
}

int Validate(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: fewshot-pack validate <file.json>");
        return 1;
    }

    var json = File.ReadAllText(args[1]);
    var pack = PackCrypto.DeserializePack(json);

    Console.WriteLine($"Pack ID:        {pack.PackId}");
    Console.WriteLine($"Name:           {pack.Name}");
    Console.WriteLine($"Version:        {pack.Version}");
    Console.WriteLine($"Author:         {pack.Author}");
    Console.WriteLine($"Target Project: {pack.TargetProject}");
    Console.WriteLine($"Memories:       {pack.Memories.Count}");
    Console.WriteLine($"Preferences:    {pack.Preferences.Count}");
    Console.WriteLine($"Anti-Patterns:  {pack.AntiPatterns.Count}");
    Console.WriteLine("Validation: PASSED");
    return 0;
}

int Encrypt(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: fewshot-pack encrypt <file.json> --key <base64-key> [-o output.fewshotpack]");
        return 1;
    }

    var key = GetArg(args, "--key") ?? throw new ArgumentException("--key is required");
    var json = File.ReadAllText(args[1]);
    var pack = PackCrypto.DeserializePack(json);
    var envelope = PackCrypto.Encrypt(pack, key);
    var envelopeJson = PackCrypto.SerializeEnvelope(envelope);

    var outputPath = GetArg(args, "-o") ?? Path.ChangeExtension(args[1], ".fewshotpack");
    File.WriteAllText(outputPath, envelopeJson);
    Console.WriteLine($"Encrypted: {outputPath} (SHA256: {envelope.Hash[..16]}...)");
    return 0;
}

int Decrypt(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: fewshot-pack decrypt <file.fewshotpack> --key <base64-key> [-o output.json]");
        return 1;
    }

    var key = GetArg(args, "--key") ?? throw new ArgumentException("--key is required");
    var envelopeJson = File.ReadAllText(args[1]);
    var envelope = PackCrypto.DeserializeEnvelope(envelopeJson);
    var pack = PackCrypto.Decrypt(envelope, key);
    var json = PackCrypto.SerializePack(pack);

    var outputPath = GetArg(args, "-o") ?? Path.ChangeExtension(args[1], ".json");
    File.WriteAllText(outputPath, json);
    Console.WriteLine($"Decrypted: {outputPath} ({pack.PackId})");
    return 0;
}

int Keygen()
{
    Console.WriteLine(PackCrypto.GenerateKey());
    return 0;
}

int MachineId()
{
    Console.WriteLine(PackCrypto.GetMachineId());
    return 0;
}

static string? GetArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
