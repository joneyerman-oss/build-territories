using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Services;

public interface IScoringFilterEngine
{
    Task<List<BusinessCandidate>> BuildCandidatesAsync(
        IAsyncEnumerable<LightBoxRecord> records,
        IReadOnlyCollection<ZoneFeature> zones,
        FilterOptions filters,
        ScoringOptions scoring,
        IReadOnlyCollection<HashSet<string>> exclusionSets,
        CancellationToken cancellationToken);

    decimal CalculateTotalWeightedOpportunity(IReadOnlyCollection<BusinessCandidate> candidates);
}
