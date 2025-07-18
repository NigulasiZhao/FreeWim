using Hangfire.Dashboard;

namespace FreeWim;

public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true; // 允许所有人访问
    }
}