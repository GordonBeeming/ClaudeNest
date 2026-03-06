using System.ComponentModel.DataAnnotations;

namespace ClaudeNest.Backend.Models;

public record UpdateDisplayNameRequest([Required] string DisplayName);
