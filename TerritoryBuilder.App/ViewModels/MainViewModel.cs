using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TerritoryBuilder.App.Views;
using TerritoryBuilder.Core.Assignment;
using TerritoryBuilder.Core.Export;
using TerritoryBuilder.Core.Models;
using TerritoryBuilder.Core.Services;

namespace TerritoryBuilder.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDataIngestionService _ingestion = new DataIngestionService();
    private readonly IScoringFilterEngine _engine = new ScoringFilterEngine();
    private readonly IAssignmentService _assignment = new InitialAssignmentService();
    private readonly ExportService _export = new();

    private List<ZoneFeature> _zones = [];
    private List<RepRecord> _reps = [];
    private List<HashSet<string>> _exclusionSets = [];
    private AssignmentResult? _latestResult;

    public UserControl DataTab { get; } = new DataTabView();
    public UserControl FiltersTab { get; } = new FiltersTabView();
    public UserControl ScoringTab { get; } = new ScoringTabView();
    public UserControl AssignmentTab { get; } = new AssignmentTabView();
    public UserControl MapTab { get; } = new MapTabView();
    public UserControl ResultsTab { get; } = new ResultsTabView();
    public UserControl ExportTab { get; } = new ExportTabView();

    [ObservableProperty] private string lightBoxPath = string.Empty;
    [ObservableProperty] private string zoneGeoJsonPath = string.Empty;
    [ObservableProperty] private string repRosterPath = string.Empty;
    [ObservableProperty] private string exclusionPath = string.Empty;
    [ObservableProperty] private bool includeLargeBusiness = true;
    [ObservableProperty] private bool includeMediumBusiness = true;
    [ObservableProperty] private bool includeSmallBusiness = true;
    [ObservableProperty] private bool includeUnknown = true;
    [ObservableProperty] private bool includeBlanks = true;
    [ObservableProperty] private bool useAddressMultiplier;
    [ObservableProperty] private decimal fairnessTolerance = 5;
    [ObservableProperty] private string statusMessage = "Ready.";
    [ObservableProperty] private decimal totalWeightedOpportunity;
    [ObservableProperty] private int repRosterCount;
    [ObservableProperty] private int repCountInput;

    public ObservableCollection<RepMetrics> RepMetrics { get; } = [];

    public MainViewModel()
    {
        foreach (var tab in new[] { DataTab, FiltersTab, ScoringTab, AssignmentTab, MapTab, ResultsTab, ExportTab })
        {
            tab.DataContext = this;
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!File.Exists(LightBoxPath) || !File.Exists(ZoneGeoJsonPath))
        {
            StatusMessage = "Please provide valid file paths for LightBox and zones.";
            return;
        }

        StatusMessage = "Loading zones/reps...";
        _zones = await _ingestion.ReadZonesAsync(ZoneGeoJsonPath, CancellationToken.None);
        if (RepCountInput > 0)
        {
            _reps = CreateSyntheticReps(RepCountInput);
        }
        else if (File.Exists(RepRosterPath))
        {
            _reps = await _ingestion.ReadRepsAsync(RepRosterPath, CancellationToken.None);
        }
        else
        {
            StatusMessage = "Provide a rep roster CSV or enter a rep count.";
            return;
        }

        RepRosterCount = _reps.Count;
        _exclusionSets = [];

        if (!string.IsNullOrWhiteSpace(ExclusionPath) && File.Exists(ExclusionPath))
        {
            _exclusionSets.Add(await _ingestion.ReadExclusionKeysAsync(ExclusionPath, CancellationToken.None));
        }

        StatusMessage = $"Loaded {_zones.Count} zone features and {_reps.Count} reps.";
    }

    [RelayCommand]
    private void BrowseLightBoxFile()
    {
        var path = BrowseForFile("CSV files (*.csv)|*.csv|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(path)) LightBoxPath = path;
    }

    [RelayCommand]
    private void BrowseZoneGeoJsonFile()
    {
        var path = BrowseForFile("GeoJSON files (*.geojson;*.json)|*.geojson;*.json|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(path)) ZoneGeoJsonPath = path;
    }

    [RelayCommand]
    private void BrowseRepRosterFile()
    {
        var path = BrowseForFile("CSV files (*.csv)|*.csv|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(path)) RepRosterPath = path;
    }

    [RelayCommand]
    private void BrowseExclusionFile()
    {
        var path = BrowseForFile("CSV files (*.csv)|*.csv|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(path)) ExclusionPath = path;
    }

    [RelayCommand]
    private async Task PreviewScoringAsync()
    {
        try
        {
            var candidates = await BuildCandidatesAsync();
            TotalWeightedOpportunity = _engine.CalculateTotalWeightedOpportunity(candidates);
            StatusMessage = $"Preview complete: {candidates.Count} included businesses.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RunAssignmentAsync()
    {
        try
        {
            var candidates = await BuildCandidatesAsync();
            var result = await _assignment.AssignAsync(candidates, _reps, new AssignmentOptions { FairnessTolerancePercent = FairnessTolerance }, CancellationToken.None);
            _latestResult = result;
            RepMetrics.Clear();
            foreach (var row in result.RepMetrics) RepMetrics.Add(row);
            StatusMessage = $"Run complete. Fairness index: {result.Overall.FairnessIndex:P2}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Run failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_latestResult is null)
        {
            StatusMessage = "Run assignment before export.";
            return;
        }

        Directory.CreateDirectory("output");
        await _export.ExportAssignmentsCsvAsync(Path.Combine("output", "assignments.csv"), _latestResult, CancellationToken.None);
        await _export.ExportSummaryCsvAsync(Path.Combine("output", "summary.csv"), _latestResult, CancellationToken.None);
        await _export.ExportRunLogJsonAsync(Path.Combine("output", "run-log.json"), _latestResult, CancellationToken.None);
        await _export.ExportTerritoriesGeoJsonAsync(Path.Combine("output", "territories.geojson"), _latestResult, CancellationToken.None);
        StatusMessage = "Exports written to output/.";
    }

    private async Task<List<BusinessCandidate>> BuildCandidatesAsync()
    {
        var buildingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IncludeLargeBusiness) buildingTypes.Add("Large Business");
        if (IncludeMediumBusiness) buildingTypes.Add("Medium Business");
        if (IncludeSmallBusiness) buildingTypes.Add("Small Business");
        if (IncludeUnknown) buildingTypes.Add("Unknown");
        if (IncludeBlanks) buildingTypes.Add("(Blanks)");

        var filters = new FilterOptions
        {
            IncludedBuildingTypes = buildingTypes
        };

        var scoring = new ScoringOptions { UseAddressMultiplier = UseAddressMultiplier };
        return await _engine.BuildCandidatesAsync(_ingestion.ReadLightBoxAsync(LightBoxPath, CancellationToken.None), _zones, filters, scoring, _exclusionSets, CancellationToken.None);
    }

    private static string? BrowseForFile(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static List<RepRecord> CreateSyntheticReps(int repCount)
    {
        return Enumerable.Range(1, repCount)
            .Select(i => new RepRecord
            {
                RepId = $"rep-{i}",
                RepName = $"Rep {i}",
                Active = true,
                HomeLat = 0,
                HomeLon = 0
            })
            .ToList();
    }
}
