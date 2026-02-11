using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Assignment;

public interface IAssignmentService
{
    Task<AssignmentResult> AssignAsync(
        IReadOnlyCollection<BusinessCandidate> candidates,
        IReadOnlyCollection<RepRecord> reps,
        AssignmentOptions options,
        CancellationToken cancellationToken);
}
