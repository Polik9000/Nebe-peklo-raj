namespace NebePekloRaj.GpxValidation;

/// <summary>A WGS84 coordinate in degrees.</summary>
public readonly record struct GeoPoint(double Lat, double Lon)
{
    public bool IsValid =>
        double.IsFinite(Lat) && double.IsFinite(Lon) &&
        Lat is >= -90 and <= 90 && Lon is >= -180 and <= 180 &&
        !(Lat == 0 && Lon == 0);
}

/// <summary>A recorded GPS point with its time.</summary>
public readonly record struct TrackPoint(GeoPoint Position, DateTimeOffset Time, double? Elevation = null);

public static class Geo
{
    public const double EarthRadiusM = 6_371_000;

    /// <summary>Great-circle distance in metres.</summary>
    public static double Distance(GeoPoint a, GeoPoint b)
    {
        double lat1 = ToRad(a.Lat), lat2 = ToRad(b.Lat);
        double dLat = lat2 - lat1, dLon = ToRad(b.Lon - a.Lon);
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Moves a point by the given metres east and north (small distances only).</summary>
    public static GeoPoint Offset(GeoPoint p, double eastM, double northM) => new(
        p.Lat + ToDeg(northM / EarthRadiusM),
        p.Lon + ToDeg(eastM / (EarthRadiusM * Math.Cos(ToRad(p.Lat)))));

    internal static double ToRad(double deg) => deg * Math.PI / 180;
    internal static double ToDeg(double rad) => rad * 180 / Math.PI;
}
