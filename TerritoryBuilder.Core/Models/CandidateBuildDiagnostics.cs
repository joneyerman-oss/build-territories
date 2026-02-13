namespace TerritoryBuilder.Core.Models;

public sealed class CandidateBuildDiagnostics
{
    public int TotalRecordsRead { get; set; }
    public int InvalidCoordinateSkipped { get; set; }
    public int NonBusinessSkipped { get; set; }
    public int BuildingTypeFiltered { get; set; }
    public int CityFiltered { get; set; }
    public int CountyFiltered { get; set; }
    public int ExclusionFiltered { get; set; }
    public int DuplicateFiltered { get; set; }
    public int ZoneNotMatchedFiltered { get; set; }
    public int IncludedCandidates { get; set; }
}

public sealed class CandidateBuildResult
{
    public List<BusinessCandidate> Candidates { get; init; } = [];
    public CandidateBuildDiagnostics Diagnostics { get; init; } = new();
}
