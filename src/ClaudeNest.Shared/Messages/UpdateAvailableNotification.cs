namespace ClaudeNest.Shared.Messages;

public sealed class UpdateAvailableNotification
{
    public required string LatestVersion { get; init; }
    public required string DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public bool IsForced { get; init; }
}
