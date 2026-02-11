# NoFiberClusterTool build restore fixes

The restore errors you posted are caused by package IDs that do not exist on nuget.org:

- `Mapsui.Geometries`
- `Mapsui.Rendering.Xaml`
- `NotMapsui`

## Root cause

Those package IDs are either from an older ecosystem split or are typos. Current Mapsui usage is generally based on:

- `Mapsui`
- `Mapsui.UI.Wpf` (for WPF desktop rendering)

Geometry support is typically consumed through `NetTopologySuite` rather than a standalone `Mapsui.Geometries` package.

## Fix to apply in `NoFiberClusterTool.csproj`

Replace invalid package references with valid ones:

```xml
<ItemGroup>
  <PackageReference Include="Mapsui" Version="4.1.9" />
  <PackageReference Include="Mapsui.UI.Wpf" Version="4.1.9" />
  <PackageReference Include="NetTopologySuite" Version="2.5.0" />
</ItemGroup>
```

And remove any references to:

- `Mapsui.Geometries`
- `Mapsui.Rendering.Xaml`
- `NotMapsui`

## Compatibility note

If your code uses legacy namespaces, update imports/usages to current namespaces in Mapsui 4.x or pin to a known older Mapsui version where your API surface still matches.

## Verify

Run:

```bash
dotnet restore NoFiberClusterTool.csproj
dotnet build NoFiberClusterTool.csproj
```

If restore still fails, check if a `NuGet.config` in the repo overrides sources and blocks nuget.org.
