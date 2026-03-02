using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController(NestDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory) : ControllerBase
{
    /// <summary>
    /// Returns the current user's profile.
    /// Auto-provisions a user record on first login.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var email = User.FindFirst("email")?.Value;
        var name = User.FindFirst("name")?.Value;

        var user = await db.Users
            .Include(u => u.Account)
            .ThenInclude(a => a.Plan)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null)
        {
            // Access tokens don't include profile claims — fetch from Auth0 /userinfo
            if (email is null)
            {
                (email, name) = await FetchAuth0UserInfo();
            }

            var account = new Account
            {
                Name = name ?? email ?? "User"
            };

            user = new User
            {
                Auth0UserId = auth0UserId,
                Email = email!,
                DisplayName = name,
                Account = account
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Reload with includes
            user = await db.Users
                .Include(u => u.Account)
                .ThenInclude(a => a.Plan)
                .FirstAsync(u => u.Id == user.Id);
        }

        var account2 = user.Account;

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            Account = new
            {
                account2.Id,
                PlanId = account2.PlanId,
                PlanName = account2.Plan?.Name,
                SubscriptionStatus = account2.SubscriptionStatus.ToString(),
                account2.TrialEndsAt,
                MaxAgents = account2.Plan?.MaxAgents ?? 0,
                MaxSessions = account2.Plan?.MaxSessions ?? 0
            }
        });
    }

    private async Task<(string? email, string? name)> FetchAuth0UserInfo()
    {
        var authority = config["Auth0:Authority"]?.TrimEnd('/');
        if (authority is null) return (null, null);

        var accessToken = HttpContext.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", "");
        if (accessToken is null) return (null, null);

        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{authority}/userinfo");
            if (!response.IsSuccessStatusCode) return (null, null);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var email = json.TryGetProperty("email", out var e) ? e.GetString() : null;
            var name = json.TryGetProperty("name", out var n) ? n.GetString() : null;
            return (email, name);
        }
        catch
        {
            return (null, null);
        }
    }
}
