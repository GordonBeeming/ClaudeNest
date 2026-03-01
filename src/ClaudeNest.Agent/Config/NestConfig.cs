namespace ClaudeNest.Agent.Config;

public sealed class NestConfig
{
    public List<string> AllowedPaths { get; set; } = [];
    public List<string> DeniedPaths { get; set; } = [];
    public string ClaudeBinary { get; set; } = "claude";
    public int MaxSessions { get; set; } = 3;
}
