using System.Globalization;
using System.Text;

namespace NebePekloRaj.GpxValidation.Tests;

/// <summary>Builds synthetic recordings by walking along paths at a set speed.</summary>
public sealed class TrackBuilder
{
    private readonly double _intervalS;
    private readonly List<GpxPoint> _points = new();

    public TrackBuilder(GeoPoint start, DateTimeOffset startTime, double intervalS = 10)
    {
        _intervalS = intervalS;
        Position = start;
        Time = startTime;
        Emit();
    }

    public GeoPoint Position { get; private set; }
    public DateTimeOffset Time { get; private set; }
    public List<GpxPoint> Points => _points;

    /// <summary>Stays in place, recording points.</summary>
    public TrackBuilder Stand(TimeSpan duration)
    {
        var end = Time + duration;
        while (Time + TimeSpan.FromSeconds(_intervalS) <= end)
        {
            Time += TimeSpan.FromSeconds(_intervalS);
            Emit();
        }
        return this;
    }

    /// <summary>Time passes with no points recorded (auto-pause, dead battery, signal loss).</summary>
    public TrackBuilder Skip(TimeSpan duration)
    {
        Time += duration;
        return this;
    }

    /// <summary>Moves without recording and records one point at the end (a gap while moving).</summary>
    public TrackBuilder JumpTo(GeoPoint p, TimeSpan duration)
    {
        Time += duration;
        Position = p;
        Emit();
        return this;
    }

    public TrackBuilder Walk(IEnumerable<GeoPoint> path, double kmh)
    {
        var poly = new List<GeoPoint> { Position };
        poly.AddRange(path);
        var cum = new double[poly.Count];
        for (int i = 1; i < poly.Count; i++) cum[i] = cum[i - 1] + Geo.Distance(poly[i - 1], poly[i]);
        double total = cum[^1];
        double step = kmh / 3.6 * _intervalS;

        int seg = 1;
        double s = step;
        for (; s <= total; s += step)
        {
            while (seg < poly.Count - 1 && cum[seg] < s) seg++;
            double segLen = cum[seg] - cum[seg - 1];
            double t = segLen == 0 ? 0 : (s - cum[seg - 1]) / segLen;
            var a = poly[seg - 1];
            var b = poly[seg];
            Time += TimeSpan.FromSeconds(_intervalS);
            Position = new GeoPoint(a.Lat + (b.Lat - a.Lat) * t, a.Lon + (b.Lon - a.Lon) * t);
            Emit();
        }

        double rest = total - (s - step);
        if (rest > 0.5)
        {
            Time += TimeSpan.FromSeconds(_intervalS * rest / step);
            Position = poly[^1];
            Emit();
        }
        return this;
    }

    public TrackBuilder Walk(GeoPoint to, double kmh) => Walk([to], kmh);

    public GpxTrack Build() => new(_points.ToList());

    private void Emit() => _points.Add(new GpxPoint(Position, Time, 300));

    /// <summary>Points along a route from one distance to another.</summary>
    public static IEnumerable<GeoPoint> Along(RouteModel route, double fromM, double toM) =>
        route.Between(fromM, toM);

    public static string ToGpxXml(IEnumerable<GpxPoint> points)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1"><trk><trkseg>""");
        foreach (var p in points)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"""<trkpt lat="{p.Position.Lat:F7}" lon="{p.Position.Lon:F7}">""");
            if (p.Elevation is { } e) sb.Append(CultureInfo.InvariantCulture, $"<ele>{e:F1}</ele>");
            if (p.Time is { } t) sb.Append(CultureInfo.InvariantCulture, $"<time>{t.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}</time>");
            sb.AppendLine("</trkpt>");
        }
        sb.AppendLine("</trkseg></trk></gpx>");
        return sb.ToString();
    }
}

/// <summary>A clock fixed at a given time.</summary>
public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>A straight 10 km test route heading east, Peklo at 4 km.</summary>
public static class StraightRoute
{
    public static readonly GeoPoint Origin = new(50.0, 16.0);
    public const double LengthM = 10_000;
    public const double PekloM = 4_000;

    public static GeoPoint At(double eastM, double northM = 0) => Geo.Offset(Origin, eastM, northM);

    public static RouteModel Route { get; } =
        new(Enumerable.Range(0, 101).Select(i => At(i * 100)));

    public static ChallengeConfig Config { get; } = new(
        new Checkpoint("Start", At(0).Lat, At(0).Lon),
        new Checkpoint("Peklo", At(PekloM).Lat, At(PekloM).Lon),
        new Checkpoint("Cíl", At(LengthM).Lat, At(LengthM).Lon),
        new ValidationLimits());

    public static readonly DateTimeOffset T0 = new(2027, 5, 1, 4, 0, 0, TimeSpan.Zero);

    public static AttemptValidator Validator() =>
        new(Config, Route, new FixedTime(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)));

    /// <summary>A recording that walks the route east at the given speed.</summary>
    public static TrackBuilder Walker(double intervalS = 10) => new(At(0), T0, intervalS);

    public static IEnumerable<GeoPoint> Line(double fromM, double toM, double stepM = 50)
    {
        double dir = toM >= fromM ? 1 : -1;
        for (double d = fromM + dir * stepM; dir * (toM - d) > 0; d += dir * stepM)
            yield return At(d);
        yield return At(toM);
    }
}

public static class RealRoute
{
    private static readonly Lazy<(ChallengeConfig Config, RouteModel Route)> Loaded = new(() =>
        ChallengeConfig.LoadWithRoute(Path.Combine(AppContext.BaseDirectory, "data", "challenge.json")));

    public static ChallengeConfig Config => Loaded.Value.Config;
    public static RouteModel Route => Loaded.Value.Route;
}

public static class Approx
{
    public static void Near(double expected, double actual, double tolerance) =>
        Xunit.Assert.InRange(actual, expected - tolerance, expected + tolerance);
}
