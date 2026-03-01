using System.Diagnostics;
using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Agent.Services;

public sealed class ManagedSession
{
    public required Guid SessionId { get; init; }
    public required string Path { get; init; }
    public SessionState State { get; set; }
    public int? Pid { get; set; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PermissionMode { get; set; }
    public Process? Process { get; set; }
}
