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
        return JsonSerializer.Deserialize(json, AgentJsonContext.Default.AgentCredentials);
    }
}
