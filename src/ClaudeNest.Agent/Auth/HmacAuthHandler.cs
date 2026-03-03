using System.Security.Cryptography;
using System.Text;

namespace ClaudeNest.Agent.Auth;

/// <summary>
/// DelegatingHandler that computes HMAC-SHA256 auth headers per HTTP request.
/// The raw secret never leaves the agent — only its HMAC signature is sent.
/// </summary>
public sealed class HmacAuthHandler : DelegatingHandler
{
    private readonly Guid _agentId;
    private readonly byte[] _derivedKey; // SHA256(secret), same as server's SecretHash

    public HmacAuthHandler(Guid agentId, string secret)
        : base(new HttpClientHandler())
    {
        _agentId = agentId;
        _derivedKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public HmacAuthHandler(Guid agentId, string secret, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _agentId = agentId;
        _derivedKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var message = $"{timestamp}|{_agentId}";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signature = HMACSHA256.HashData(_derivedKey, messageBytes);

        request.Headers.Remove("X-Agent-Id");
        request.Headers.Remove("X-Agent-Timestamp");
        request.Headers.Remove("X-Agent-Signature");

        request.Headers.Add("X-Agent-Id", _agentId.ToString());
        request.Headers.Add("X-Agent-Timestamp", timestamp);
        request.Headers.Add("X-Agent-Signature", Convert.ToBase64String(signature));

        return base.SendAsync(request, cancellationToken);
    }
}
