using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Flow.Launcher.Plugin.SteamLauncher.Security;

public sealed class DpapiApiKeyStore : IApiKeyStore
{
    private const string FileName = "owned_api_key.bin";

    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("Flow.Launcher.Plugin.SteamLauncher:owned_api_key:v1");

    private readonly string _filePath;

    public DpapiApiKeyStore(string storageDirectory)
    {
        Directory.CreateDirectory(storageDirectory);
        _filePath = Path.Combine(storageDirectory, FileName);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Load());

    public string? Load()
    {
        if (!File.Exists(_filePath)) return null;

        var ciphertext = File.ReadAllBytes(_filePath);
        if (ciphertext.Length == 0) return null;

        try
        {
            var plaintext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            try { File.Delete(_filePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return null;
        }
    }

    public void Save(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            return;
        }

        var plaintext = Encoding.UTF8.GetBytes(apiKey);
        var ciphertext = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, ciphertext);
    }
}
