# TerritoryBuilder

TerritoryBuilder is a .NET 8 WPF + MVVM desktop application for creating fair, contiguous sales territories from LightBox geolocated business data.

## Tech
- .NET 8
- WPF + MVVM (CommunityToolkit.Mvvm)
- NetTopologySuite
- CsvHelper
- Serilog file logging
- Optional WebView2 map integration

## Solution layout
- `TerritoryBuilder.App` - WPF UI and orchestration
- `TerritoryBuilder.Core` - models, ingestion, scoring/filtering, assignment, exports
- `TerritoryBuilder.Tests` - unit tests
- `sample-data` - CSV/GeoJSON templates

## Setup
1. Install .NET 8 SDK and Windows Desktop runtime.
2. Restore packages:
   ```bash
   dotnet restore TerritoryBuilder.sln
   ```
3. Run tests:
   ```bash
   dotnet test TerritoryBuilder.sln
   ```
4. Run the app:
   ```bash
   dotnet run --project TerritoryBuilder.App
   ```

## Publish
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## MVP workflow
1. Data tab: load LightBox CSV, zone GeoJSON, optional exclusion CSV, rep roster CSV.
2. Filters tab: choose VNN/NN and building type checkboxes.
3. Scoring tab: preview weighted opportunity and optional address multiplier.
4. Assignment tab: run heuristic assignment.
5. Results tab: inspect per-rep metrics.
6. Export tab: create assignment CSV, summary CSV, territory GeoJSON, and run log JSON.

## Notes
- Logs are written to `logs/territorybuilder-*.log`.
- Export output is written to `output/`.
- Current phase-1 assignment uses greedy nearest+load balancing and includes fairness metrics.
