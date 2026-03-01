namespace ClaudeNest.Shared.Messages;

public sealed class DirectoryListingResponse
{
    public required string RequestId { get; init; }
    public required string Path { get; init; }
    public List<string> Directories { get; init; } = [];
    public string? Error { get; init; }
}
