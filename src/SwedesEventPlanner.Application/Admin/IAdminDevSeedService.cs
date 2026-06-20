using SwedesEventPlanner.Contracts.Admin;

namespace SwedesEventPlanner.Application.Admin;

public interface IAdminDevSeedService
{
    Task<AdminDevSeedResponse> SeedMockActivityDemoAsync(CancellationToken cancellationToken);
}
