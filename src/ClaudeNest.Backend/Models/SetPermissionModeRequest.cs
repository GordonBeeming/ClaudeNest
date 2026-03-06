using System.ComponentModel.DataAnnotations;

namespace ClaudeNest.Backend.Models;

public record SetPermissionModeRequest([Required] string Mode);
