namespace ClaudeNest.Agent.Config;

public sealed class NestConfig
{
    public string? Name { get; set; }
    public List<string> AllowedPaths { get; set; } = [];
    public List<string> DeniedPaths { get; set; } = [];
    public string ClaudeBinary { get; set; } = "claude";
}
