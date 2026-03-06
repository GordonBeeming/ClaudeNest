using System.ComponentModel.DataAnnotations;

namespace ClaudeNest.Backend.Models;

public record ExchangeRequest([Required] string Token, string? AgentName, string? Hostname, string? OS);
