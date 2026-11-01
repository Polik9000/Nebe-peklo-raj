namespace NebePekloRaj.GpxValidation;

/// <summary>Where a point lies relative to the official route.</summary>
/// <param name="DistanceFromRouteM">Shortest distance to the route line.</param>
/// <param name="AlongM">Distance from the route start to the projected point.</param>
public readonly record struct RouteProjection(double DistanceFromRouteM, double AlongM);

/// <summary>
/// The official route as a polyline with cumulative distances and a grid index,
/// so each recorded point can be projected onto it quickly.
/// </summary>
public sealed class RouteModel
{
    private const double CellSizeM = 500;

    private readonly GeoPoint[] _points;
    private readonly double[] _cumulative;
    private readonly Dictionary<(int, int), List<int>> _grid = new();
    private readonly double _refCos;

    public RouteModel(IEnumerable<GeoPoint> points)
    {
        // Drop invalid and repeated points; repeated points make zero-length segments.
        var list = new List<GeoPoint>();
        foreach (var p in points)
        {
            if (!p.IsValid) continue;
            if (list.Count > 0 && list[^1] == p) continue;
            list.Add(p);
        }
        if (list.Count < 2)
            throw new ArgumentException("Trasa musí mít aspoň dva platné body.", nameof(points));

        _points = list.ToArray();
        _cumulative = new double[_points.Length];
        for (int i = 1; i < _points.Length; i++)
            _cumulative[i] = _cumulative[i - 1] + Geo.Distance(_points[i - 1], _points[i]);

        _refCos = Math.Cos(Geo.ToRad(_points.Average(p => p.Lat)));
        for (int i = 0; i < _points.Length - 1; i++)
            IndexSegment(i);
    }

    public static RouteModel FromGpx(GpxTrack track) => new(track.Points.Select(p => p.Position));

    public IReadOnlyList<GeoPoint> Points => _points;
    public double LengthM => _cumulative[^1];

    /// <summary>The point at the given distance along the route.</summary>
    public GeoPoint PointAt(double alongM)
    {
        alongM = Math.Clamp(alongM, 0, LengthM);
        int i = Array.BinarySearch(_cumulative, alongM);
        if (i >= 0) return _points[i];
        i = ~i; // first index with cumulative > along
        var a = _points[i - 1];
        var b = _points[i];
        double seg = _cumulative[i] - _cumulative[i - 1];
        double t = seg == 0 ? 0 : (alongM - _cumulative[i - 1]) / seg;
        return new GeoPoint(a.Lat + (b.Lat - a.Lat) * t, a.Lon + (b.Lon - a.Lon) * t);
    }

    /// <summary>Route bearing at a distance along it, in radians clockwise from north.</summary>
    public double BearingAt(double alongM)
    {
        var a = PointAt(alongM - 20);
        var b = PointAt(alongM + 20);
        double east = (b.Lon - a.Lon) * Math.Cos(Geo.ToRad(a.Lat));
        double north = b.Lat - a.Lat;
        return Math.Atan2(east, north);
    }

    /// <summary>Nearest point of the whole route, or null if the route is further than searchRadiusM.</summary>
    public RouteProjection? ProjectNearest(GeoPoint p, double searchRadiusM = 5000)
    {
        RouteProjection? best = null;
        foreach (var seg in CandidateSegments(p, searchRadiusM))
        {
            var proj = ProjectOnSegment(p, seg);
            if (best is null || proj.DistanceFromRouteM < best.Value.DistanceFromRouteM)
                best = proj;
        }
        return best is { } b && b.DistanceFromRouteM <= searchRadiusM ? b : null;
    }

    /// <summary>
    /// Projects a point that is expected near <paramref name="progressM"/>. Among all route parts
    /// within <paramref name="corridorM"/> it picks the one closest to the current progress, so
    /// places where the route crosses or doubles back on itself resolve to the right kilometre.
    /// Returns null when no part of the route is within the corridor.
    /// </summary>
    public RouteProjection? ProjectNear(GeoPoint p, double progressM, double corridorM)
    {
        var hits = new List<RouteProjection>();
        foreach (var seg in CandidateSegments(p, corridorM))
        {
            var proj = ProjectOnSegment(p, seg);
            if (proj.DistanceFromRouteM <= corridorM) hits.Add(proj);
        }
        if (hits.Count == 0) return null;

        // Group hits into passes (separate times the route goes by this place),
        // take the closest point of each pass, then the pass nearest to the progress.
        hits.Sort((a, b) => a.AlongM.CompareTo(b.AlongM));
        RouteProjection best = hits[0];
        double bestCost = double.MaxValue;
        var passBest = hits[0];
        for (int i = 1; i <= hits.Count; i++)
        {
            if (i < hits.Count && hits[i].AlongM - hits[i - 1].AlongM <= 2 * corridorM)
            {
                if (hits[i].DistanceFromRouteM < passBest.DistanceFromRouteM) passBest = hits[i];
                continue;
            }
            double cost = Math.Abs(passBest.AlongM - progressM);
            if (cost < bestCost) { bestCost = cost; best = passBest; }
            if (i < hits.Count) passBest = hits[i];
        }
        return best;
    }

    /// <summary>Route vertices from one distance to another, going forward, including both ends.</summary>
    public IEnumerable<GeoPoint> Between(double fromM, double toM)
    {
        yield return PointAt(fromM);
        for (int i = 0; i < _points.Length; i++)
            if (_cumulative[i] > fromM && _cumulative[i] < toM)
                yield return _points[i];
        yield return PointAt(toM);
    }

    private RouteProjection ProjectOnSegment(GeoPoint p, int i)
    {
        var a = _points[i];
        var b = _points[i + 1];
        double cos = Math.Cos(Geo.ToRad(p.Lat));
        // Local flat frame in metres, centred on a.
        double bx = Geo.ToRad(b.Lon - a.Lon) * cos * Geo.EarthRadiusM;
        double by = Geo.ToRad(b.Lat - a.Lat) * Geo.EarthRadiusM;
        double px = Geo.ToRad(p.Lon - a.Lon) * cos * Geo.EarthRadiusM;
        double py = Geo.ToRad(p.Lat - a.Lat) * Geo.EarthRadiusM;
        double len2 = bx * bx + by * by;
        double t = len2 == 0 ? 0 : Math.Clamp((px * bx + py * by) / len2, 0, 1);
        double dx = px - t * bx, dy = py - t * by;
        double seg = _cumulative[i + 1] - _cumulative[i];
        return new RouteProjection(Math.Sqrt(dx * dx + dy * dy), _cumulative[i] + t * seg);
    }

    private (double X, double Y) ToGrid(GeoPoint p) => (
        Geo.ToRad(p.Lon) * _refCos * Geo.EarthRadiusM / CellSizeM,
        Geo.ToRad(p.Lat) * Geo.EarthRadiusM / CellSizeM);

    private void IndexSegment(int i)
    {
        var (ax, ay) = ToGrid(_points[i]);
        var (bx, by) = ToGrid(_points[i + 1]);
        for (int x = (int)Math.Floor(Math.Min(ax, bx)); x <= (int)Math.Floor(Math.Max(ax, bx)); x++)
        for (int y = (int)Math.Floor(Math.Min(ay, by)); y <= (int)Math.Floor(Math.Max(ay, by)); y++)
        {
            if (!_grid.TryGetValue((x, y), out var list))
                _grid[(x, y)] = list = new List<int>();
            list.Add(i);
        }
    }

    private IEnumerable<int> CandidateSegments(GeoPoint p, double radiusM)
    {
        var (gx, gy) = ToGrid(p);
        // A little slack for the flat-frame approximation.
        double r = radiusM * 1.05 / CellSizeM;
        var seen = new HashSet<int>();
        for (int x = (int)Math.Floor(gx - r); x <= (int)Math.Floor(gx + r); x++)
        for (int y = (int)Math.Floor(gy - r); y <= (int)Math.Floor(gy + r); y++)
        {
            if (!_grid.TryGetValue((x, y), out var list)) continue;
            foreach (var i in list)
                if (seen.Add(i))
                    yield return i;
        }
    }
}
