using NetTopologySuite.Geometries;

namespace TerritoryBuilder.Core.Utilities;

public static class GeoUtils
{
    private const double EarthRadiusMiles = 3958.8;

    public static bool IsValidCoordinate(double lat, double lon) => lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

    public static double HaversineMiles(Coordinate a, Coordinate b)
    {
        var dLat = ToRadians(b.Y - a.Y);
        var dLon = ToRadians(b.X - a.X);
        var originLat = ToRadians(a.Y);
        var destLat = ToRadians(b.Y);

        var h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(originLat) * Math.Cos(destLat) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusMiles * Math.Asin(Math.Sqrt(h));
    }

    private static double ToRadians(double deg) => deg * (Math.PI / 180.0);
}
