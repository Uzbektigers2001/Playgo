using Hangfire.Dashboard;

namespace Playgo.API.Middleware;

public class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        return user.Identity?.IsAuthenticated == true && user.IsInRole("Admin");
    }
}
