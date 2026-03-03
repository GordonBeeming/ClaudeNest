namespace ClaudeNest.Agent.Config;

/// <summary>
/// On-disk model for encrypted credentials (version 2).
/// Legacy format (AgentCredentials with plaintext Secret) is auto-migrated on read.
/// </summary>
public sealed class StoredCredentials
{
    public int Version { get; set; } = 2;
    public Guid AgentId { get; set; }
    public string EncryptedSecret { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty; // base64
    public string BackendUrl { get; set; } = string.Empty;
}
