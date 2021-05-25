using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Etherna.BeehiveManager.Configs.Hangfire
{
    public class AllowAllFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context) => true;
    }
}
