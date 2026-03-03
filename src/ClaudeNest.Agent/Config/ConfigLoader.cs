using System.Runtime.InteropServices;
using System.Text.Json;
using ClaudeNest.Agent.Serialization;

namespace ClaudeNest.Agent.Config;

public static class ConfigLoader
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claudenest");

    public static NestConfig LoadConfig()
    {
        var configPath = Path.Combine(ConfigDir, "config.json");
        if (!File.Exists(configPath))
        {
            return new NestConfig();
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize(json, AgentJsonContext.Default.NestConfig) ?? new NestConfig();
    }

    public static AgentCredentials? LoadCredentials()
    {
        var credentialsPath = Path.Combine(ConfigDir, "credentials.json");
        if (!File.Exists(credentialsPath))
        {
            return null;
        }

        var json = File.ReadAllText(credentialsPath);

        // Try encrypted format (version 2) first
        var stored = JsonSerializer.Deserialize(json, AgentJsonContext.Default.StoredCredentials);
        if (stored is not null && stored.Version == 2
            && !string.IsNullOrEmpty(stored.EncryptedSecret)
            && !string.IsNullOrEmpty(stored.Salt))
        {
            try
            {
                var salt = Convert.FromBase64String(stored.Salt);
                var secret = CredentialProtector.Decrypt(stored.EncryptedSecret, salt);
                return new AgentCredentials
                {
                    AgentId = stored.AgentId,
                    Secret = secret,
                    BackendUrl = stored.BackendUrl
                };
            }
            catch
            {
                // Decryption failed (e.g. different machine) — fall through to legacy
            }
        }

        // Fall back to legacy plaintext format
        var legacy = JsonSerializer.Deserialize(json, AgentJsonContext.Default.AgentCredentials);
        if (legacy is not null && !string.IsNullOrEmpty(legacy.Secret))
        {
            // Auto-migrate to encrypted format
            SaveCredentials(legacy);
            return legacy;
        }

        return null;
    }

    public static void SaveConfig(NestConfig config)
    {
        EnsureConfigDir();
        var configPath = Path.Combine(ConfigDir, "config.json");
        var json = JsonSerializer.Serialize(config, AgentJsonContext.Default.NestConfig);
        File.WriteAllText(configPath, json);
        SetRestrictivePermissions(configPath);
    }

    public static void DeleteCredentials()
    {
        var credentialsPath = Path.Combine(ConfigDir, "credentials.json");
        if (File.Exists(credentialsPath))
        {
            File.Delete(credentialsPath);
        }
    }

    public static void DeleteConfig()
    {
        var configPath = Path.Combine(ConfigDir, "config.json");
        if (File.Exists(configPath))
        {
            File.Delete(configPath);
        }
    }

    public static void SaveCredentials(AgentCredentials credentials)
    {
        EnsureConfigDir();

        var salt = CredentialProtector.GenerateSalt();
        var encryptedSecret = CredentialProtector.Encrypt(credentials.Secret, salt);

        var stored = new StoredCredentials
        {
            Version = 2,
            AgentId = credentials.AgentId,
            EncryptedSecret = encryptedSecret,
            Salt = Convert.ToBase64String(salt),
            BackendUrl = credentials.BackendUrl
        };

        var credentialsPath = Path.Combine(ConfigDir, "credentials.json");
        var json = JsonSerializer.Serialize(stored, AgentJsonContext.Default.StoredCredentials);
        File.WriteAllText(credentialsPath, json);
        SetRestrictivePermissions(credentialsPath);
    }

    private static void EnsureConfigDir()
    {
        Directory.CreateDirectory(ConfigDir);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Set 0700 on the config directory (owner only)
            File.SetUnixFileMode(ConfigDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SetRestrictivePermissions(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Set 0600 on sensitive files (owner read/write only)
            File.SetUnixFileMode(filePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
