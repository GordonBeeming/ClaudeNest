using System.ComponentModel.DataAnnotations;

namespace ClaudeNest.Backend.Models;

public record RedeemCouponRequest([Required] string Code);
