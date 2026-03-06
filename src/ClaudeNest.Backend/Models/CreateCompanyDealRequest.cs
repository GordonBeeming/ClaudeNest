using System.ComponentModel.DataAnnotations;

namespace ClaudeNest.Backend.Models;

public record CreateCompanyDealRequest([Required] string Domain, Guid PlanId);
