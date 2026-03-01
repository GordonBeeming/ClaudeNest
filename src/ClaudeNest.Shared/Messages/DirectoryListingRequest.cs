namespace ClaudeNest.Shared.Messages;

public sealed class DirectoryListingRequest
{
    public required string RequestId { get; init; }
    public required string Path { get; init; }
}
