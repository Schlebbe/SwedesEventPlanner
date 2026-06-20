using SwedesEventPlanner.Contracts.Activity;

namespace SwedesEventPlanner.Application.Activity;

public interface IActivityIngestionService
{
    Task<CreateActivityResponse> CreateActivityAsync(
        CreateActivityRequest request,
        CancellationToken cancellationToken);
}
