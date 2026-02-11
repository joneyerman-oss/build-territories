using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Services;

public interface IDataIngestionService
{
    IAsyncEnumerable<LightBoxRecord> ReadLightBoxAsync(string path, CancellationToken cancellationToken);
    Task<List<RepRecord>> ReadRepsAsync(string path, CancellationToken cancellationToken);
    Task<List<ZoneFeature>> ReadZonesAsync(string path, CancellationToken cancellationToken);
    Task<HashSet<string>> ReadExclusionKeysAsync(string path, CancellationToken cancellationToken);
}
