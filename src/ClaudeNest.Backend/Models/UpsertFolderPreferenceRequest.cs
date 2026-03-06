namespace ClaudeNest.Backend.Models;

public class UpsertFolderPreferenceRequest
{
    public required string Path { get; set; }
    public bool IsFavorite { get; set; }
    public string? Color { get; set; }
}
