using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminRequiredAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auth0UserId = context.HttpContext.User.FindFirst("sub")?.Value;
        if (auth0UserId is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<NestDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null || !user.IsAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
