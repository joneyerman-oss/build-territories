using System.Globalization;
using CsvHelper;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using TerritoryBuilder.Core.Data;
using TerritoryBuilder.Core.Models;
using TerritoryBuilder.Core.Utilities;

namespace TerritoryBuilder.Core.Services;

public sealed class DataIngestionService : IDataIngestionService
{
    public async IAsyncEnumerable<LightBoxRecord> ReadLightBoxAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<LightBoxCsvMap>();
        await foreach (var record in csv.GetRecordsAsync<LightBoxRecord>().WithCancellation(cancellationToken))
        {
            yield return record;
        }
    }

    public async Task<List<RepRecord>> ReadRepsAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<RepCsvMap>();
        cancellationToken.ThrowIfCancellationRequested();
        return csv.GetRecords<RepRecord>().ToList();
    }

    public async Task<List<ZoneFeature>> ReadZonesAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var serializer = GeoJsonSerializer.Create();
        using var jsonReader = new JsonTextReader(new StringReader(text));
        var featureCollection = serializer.Deserialize<FeatureCollection>(jsonReader) ?? new FeatureCollection();
        return featureCollection
            .Select(f => new ZoneFeature
            {
                ZoneName = f.Attributes.Exists("zone_name") ? Convert.ToString(f.Attributes["zone_name"]) ?? string.Empty : string.Empty,
                Geometry = f.Geometry
            })
            .Where(z => z.Geometry is not null)
            .ToList();
    }

    public async Task<HashSet<string>> ReadExclusionKeysAsync(string path, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        return lines.Select(AddressNormalizer.Normalize).Where(v => !string.IsNullOrWhiteSpace(v)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
