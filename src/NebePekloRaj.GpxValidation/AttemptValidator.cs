
namespace NebePekloRaj.GpxValidation;

/// <summary>
/// Checks one attempt (one or more GPX files) against the official route and the rules,
/// and computes its elapsed time and stage splits.
/// </summary>
public sealed class AttemptValidator
{
    private const double CoverageBinM = 100;
    private const int MaxListedFindingsPerCode = 20;

    private readonly ChallengeConfig _config;
    private readonly RouteModel _route;
    private readonly TimeProvider _time;
    private ValidationLimits L => _config.Limits;

    public AttemptValidator(ChallengeConfig config, RouteModel route, TimeProvider? time = null)
    {
        _config = config;
        _route = route;
        _time = time ?? TimeProvider.System;
    }

    public ValidationResult Validate(IEnumerable<Stream> gpxFiles, RouteVariant variant,
        IEnumerable<KnownAttempt>? knownAttempts = null)
    {
        var tracks = new List<GpxTrack>();
        try
        {
            foreach (var s in gpxFiles)
                tracks.Add(GpxReader.Read(s));
        }
        catch (GpxFormatException ex)
        {
            return Fail(variant, "", new Finding(FindingCode.InvalidGpx, Severity.Reject, ex.Message), 0);
        }
        return Validate(tracks, variant, knownAttempts);
    }

    public ValidationResult Validate(IEnumerable<GpxTrack> gpxFiles, RouteVariant variant,
        IEnumerable<KnownAttempt>? knownAttempts = null)
    {
        var findings = new List<Finding>();
        var points = Merge(gpxFiles, findings);
        if (points is null)
            return Fail(variant, "", findings, 0);

        var fingerprint = TrackFingerprint.Compute(points);
        CheckFuture(points, findings);
        points = RemoveSpikes(points, findings);

        var checkpoints = CheckpointsFor(variant);
        var zones = FindZoneVisits(points, checkpoints, findings);
        if (zones is null)
        {
            CheckDuplicates(fingerprint, null, null, knownAttempts, findings);
            return Fail(variant, fingerprint, findings, points.Count);
        }

        var (departure, arrivals) = zones.Value;
        var slice = points.GetRange(departure, arrivals[^1] - departure + 1);
        double startAlong = AlongOf(checkpoints[0].Position, 0);
        double endAlong = AlongOf(checkpoints[^1].Position, _route.LengthM);

        var analysis = Analyze(slice, startAlong, findings);
        CheckDensity(slice, findings);
        CheckGaps(slice, analysis, findings);
        CheckDetours(slice, analysis, findings);
        CheckSpeed(slice, analysis, findings);
        double coverage = CheckCoverage(analysis, startAlong + checkpoints[0].RadiusM,
            endAlong - checkpoints[^1].RadiusM, findings);

        var startUtc = points[departure].Time;
        var finishUtc = points[arrivals[^1]].Time;
        CheckDuplicates(fingerprint, startUtc, finishUtc, knownAttempts, findings);

        var stages = new List<StageTime>();
        var prev = startUtc;
        for (int k = 0; k < arrivals.Length; k++)
        {
            var at = points[arrivals[k]].Time;
            stages.Add(new StageTime(StageName(checkpoints[k], checkpoints[k + 1]), prev, at));
            prev = at;
        }

        return new ValidationResult
        {
            Verdict = VerdictOf(findings),
            Variant = variant,
            Fingerprint = fingerprint,
            Findings = findings,
            StartUtc = startUtc,
            FinishUtc = finishUtc,
            Stages = variant == RouteVariant.Full ? stages : [],
            RouteLengthM = Math.Max(0, endAlong - startAlong),
            RecordedDistanceM = analysis.Cumulative[^1],
            CoveragePercent = coverage,
            PointCount = points.Count,
        };
    }

    // ---- input ----

    private static List<TrackPoint>? Merge(IEnumerable<GpxTrack> files, List<Finding> findings)
    {
        int total = 0, noTime = 0, invalid = 0;
        var timed = new List<TrackPoint>();
        foreach (var file in files)
        foreach (var p in file.Points)
        {
            total++;
            if (!p.Position.IsValid) { invalid++; continue; }
            if (p.Time is null) { noTime++; continue; }
            timed.Add(new TrackPoint(p.Position, p.Time.Value.ToUniversalTime(), p.Elevation));
        }

        if (total == 0)
        {
            findings.Add(new Finding(FindingCode.NoPoints, Severity.Reject, "GPX neobsahuje žádné body."));
            return null;
        }
        if (timed.Count == 0)
        {
            findings.Add(new Finding(FindingCode.MissingTimestamps, Severity.Reject,
                "Body nemají čas. Nejspíš jde o naplánovanou trasu, ne o záznam pohybu."));
            return null;
        }
        if (invalid + noTime > 0)
            findings.Add(new Finding(FindingCode.InvalidPointsDropped, Severity.Info,
                $"Vynecháno {invalid} bodů s neplatnou polohou a {noTime} bodů bez času."));

        // Several files (watch + phone) are merged by time; equal times keep the first point.
        var sorted = timed.OrderBy(p => p.Time).ToList();
        var merged = new List<TrackPoint>(sorted.Count);
        foreach (var p in sorted)
            if (merged.Count == 0 || p.Time > merged[^1].Time)
                merged.Add(p);
        return merged;
    }

    /// <summary>
    /// Drops single points that jump away and straight back faster than any vehicle,
    /// a typical GPS error. Otherwise they would look like a car ride.
    /// </summary>
    private List<TrackPoint> RemoveSpikes(List<TrackPoint> points, List<Finding> findings)
    {
        double limit = L.VehicleSpeedKmh / 3.6;
        double Speed(TrackPoint a, TrackPoint b) =>
            Geo.Distance(a.Position, b.Position) / (b.Time - a.Time).TotalSeconds;

        var result = new List<TrackPoint>(points.Count);
        int removed = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0 && i < points.Count - 1 && result.Count > 0)
            {
                var prev = result[^1];
                var next = points[i + 1];
                if (Speed(prev, points[i]) > limit && Speed(points[i], next) > limit && Speed(prev, next) <= limit)
                {
                    removed++;
                    continue;
                }
            }
            result.Add(points[i]);
        }

        if (removed > 0)
            findings.Add(new Finding(FindingCode.GpsGlitch, Severity.Info,
                $"Vynecháno {removed} ojedinělých skoků GPS."));
        return result;
    }

    private void CheckFuture(List<TrackPoint> points, List<Finding> findings)
    {
        var limit = _time.GetUtcNow().AddMinutes(L.FutureToleranceMin);
        if (points[^1].Time > limit)
            findings.Add(new Finding(FindingCode.TimeInFuture, Severity.Reject,
                "Záznam obsahuje čas v budoucnosti.", points[^1].Time));
    }

    // ---- zones and order ----

    private Checkpoint[] CheckpointsFor(RouteVariant v) => v switch
    {
        RouteVariant.Full => [_config.Start, _config.Peklo, _config.Finish],
        RouteVariant.Stage1 => [_config.Start, _config.Peklo],
        RouteVariant.Stage2 => [_config.Peklo, _config.Finish],
        _ => throw new ArgumentOutOfRangeException(nameof(v)),
    };

    private FindingCode MissingCode(Checkpoint cp) =>
        cp == _config.Start ? FindingCode.StartNotReached :
        cp == _config.Peklo ? FindingCode.PekloNotReached : FindingCode.FinishNotReached;

    /// <summary>
    /// Departure = last point in the first zone before heading to the next one.
    /// Arrival at each next zone = first point inside it, in order.
    /// </summary>
    private (int Departure, int[] Arrivals)? FindZoneVisits(
        List<TrackPoint> points, Checkpoint[] cps, List<Finding> findings)
    {
        int first = points.FindIndex(p => cps[0].Contains(p.Position));
        if (first < 0)
        {
            findings.Add(new Finding(MissingCode(cps[0]), Severity.Reject,
                $"Záznam neprochází zónou {cps[0].Name} ({cps[0].RadiusM:0} m)."));
            return null;
        }

        var arrivals = new int[cps.Length - 1];
        int from = first;
        for (int k = 1; k < cps.Length; k++)
        {
            var cp = cps[k];
            int arr = points.FindIndex(from + 1, p => cp.Contains(p.Position));
            if (arr < 0)
            {
                bool before = points.Take(first).Any(p => cp.Contains(p.Position));
                findings.Add(before
                    ? new Finding(FindingCode.WrongDirection, Severity.Reject,
                        $"Zóna {cp.Name} je v záznamu dřív než {cps[0].Name}. Trasa se jde jen jedním směrem.")
                    : new Finding(MissingCode(cp), Severity.Reject,
                        $"Záznam po startu neprochází zónou {cp.Name} ({cp.RadiusM:0} m)."));
                return null;
            }
            arrivals[k - 1] = arr;
            from = arr;
        }

        int departure = first;
        for (int i = arrivals[0] - 1; i >= first; i--)
            if (cps[0].Contains(points[i].Position)) { departure = i; break; }

        return (departure, arrivals);
    }

    private double AlongOf(GeoPoint p, double fallback) =>
        _route.ProjectNearest(p, 5000)?.AlongM ?? fallback;

    private static string StageName(Checkpoint from, Checkpoint to) => $"{from.Name} → {to.Name}";

    // ---- per-point analysis ----

    private sealed record Analysis(
        bool[] OnRoute,
        double?[] Along,
        double[] OffRouteM,
        double[] Cumulative);

    private Analysis Analyze(List<TrackPoint> slice, double startAlong, List<Finding> findings)
    {
        int n = slice.Count;
        var onRoute = new bool[n];
        var along = new double?[n];
        var off = new double[n];
        var cum = new double[n];
        double progress = startAlong;
        Checkpoint[] all = [_config.Start, _config.Peklo, _config.Finish];

        for (int i = 0; i < n; i++)
        {
            var p = slice[i].Position;
            if (i > 0) cum[i] = cum[i - 1] + Geo.Distance(slice[i - 1].Position, p);

            var proj = _route.ProjectNear(p, progress, L.CorridorM);
            bool inZone = all.Any(cp => cp.Contains(p));
            if (proj is null && inZone)
                proj = _route.ProjectNearest(p, 1000);

            onRoute[i] = proj is not null && (inZone || proj.Value.DistanceFromRouteM <= L.CorridorM);
            if (onRoute[i])
            {
                along[i] = proj!.Value.AlongM;
                progress = proj.Value.AlongM;
            }
            else
            {
                off[i] = _route.ProjectNearest(p, 5000)?.DistanceFromRouteM ?? double.PositiveInfinity;
            }
        }
        return new Analysis(onRoute, along, off, cum);
    }

    // ---- checks ----

    private void CheckDensity(List<TrackPoint> slice, List<Finding> findings)
    {
        if (slice.Count < 2) return;
        var intervals = new double[slice.Count - 1];
        for (int i = 1; i < slice.Count; i++)
            intervals[i - 1] = (slice[i].Time - slice[i - 1].Time).TotalSeconds;
        Array.Sort(intervals);
        double median = intervals[intervals.Length / 2];
        if (median > L.MaxPointIntervalS)
            findings.Add(new Finding(FindingCode.LowPointDensity, Severity.Reject,
                $"Body jsou zaznamenané v průměru po {median:0} s. Pravidla chtějí bod aspoň každých {L.MaxPointIntervalS:0} s."));
    }

    private void CheckGaps(List<TrackPoint> slice, Analysis a, List<Finding> findings)
    {
        int pauses = 0, listed = 0;
        TimeSpan pauseTotal = TimeSpan.Zero;
        for (int i = 1; i < slice.Count; i++)
        {
            var dt = slice[i].Time - slice[i - 1].Time;
            if (dt.TotalSeconds <= L.MaxPointIntervalS) continue;

            double d = a.Cumulative[i] - a.Cumulative[i - 1];
            if (d <= L.PauseMaxMoveM)
            {
                pauses++;
                pauseTotal += dt;
                continue;
            }

            if (listed++ >= MaxListedFindingsPerCode) continue;
            double kmh = d / dt.TotalSeconds * 3.6;
            findings.Add(new Finding(FindingCode.RecordingGap, Severity.ManualReview,
                $"Díra v záznamu {Duration(dt)}, posun {d:0} m (průměr {kmh:0.0} km/h).",
                slice[i - 1].Time, Km(a.Along[i - 1])));
        }

        if (listed > MaxListedFindingsPerCode)
            findings.Add(new Finding(FindingCode.RecordingGap, Severity.ManualReview,
                $"A dalších {listed - MaxListedFindingsPerCode} děr v záznamu."));
        if (pauses > 0)
            findings.Add(new Finding(FindingCode.Pause, Severity.Info,
                $"{pauses}× pauza bez pohybu, celkem {Duration(pauseTotal)}. Čas běží dál."));
    }

    private void CheckDetours(List<TrackPoint> slice, Analysis a, List<Finding> findings)
    {
        int i = 0, n = slice.Count;
        while (i < n)
        {
            if (a.OnRoute[i]) { i++; continue; }

            int startRun = i;
            while (i < n && !a.OnRoute[i]) i++;
            int endRun = i - 1;

            // The slice starts and ends inside zones, so neighbours on the route exist.
            int prev = Math.Max(0, startRun - 1), next = Math.Min(n - 1, endRun + 1);
            var duration = slice[next].Time - slice[prev].Time;
            double length = a.Cumulative[next] - a.Cumulative[prev];
            double maxOff = 0;
            for (int k = startRun; k <= endRun; k++) maxOff = Math.Max(maxOff, a.OffRouteM[k]);
            int count = endRun - startRun + 1;
            var at = slice[startRun].Time;
            var km = Km(a.Along[prev]);
            string desc = $"{count} bodů mimo koridor, {Duration(duration)}, {length:0} m, nejdál {Far(maxOff)} od trasy.";

            if (count == 1 && duration.TotalSeconds <= 2 * L.MaxPointIntervalS)
                findings.Add(new Finding(FindingCode.GpsGlitch, Severity.Info,
                    $"Ojedinělý bod {Far(maxOff)} od trasy, nejspíš chyba GPS.", at, km));
            else if (duration.TotalMinutes <= L.ShortDetourMaxDurationMin &&
                     length <= L.ShortDetourMaxLengthM && maxOff <= L.ShortDetourMaxDistanceM)
                findings.Add(new Finding(FindingCode.ShortDetour, Severity.ManualReview,
                    "Krátká odbočka: " + desc, at, km));
            else
                findings.Add(new Finding(FindingCode.LongDetour, Severity.Reject,
                    "Opuštění koridoru: " + desc, at, km));
        }
    }

    /// <summary>
    /// Speed is measured as progress along the official route, not as the length of the
    /// recorded line. GPS jitter makes the recorded line longer, but does not move the
    /// runner forward along the route, so it cannot fake a fast pace.
    /// </summary>
    private void CheckSpeed(List<TrackPoint> slice, Analysis a, List<Finding> findings)
    {
        var progress = new double[slice.Count];
        double origin = a.Along.FirstOrDefault(x => x is not null) ?? 0;
        double max = origin;
        for (int i = 0; i < slice.Count; i++)
        {
            if (a.Along[i] is { } here && here > max) max = here;
            progress[i] = max - origin;
        }

        double Kmh(int s, int e) => (progress[e] - progress[s]) / (slice[e].Time - slice[s].Time).TotalSeconds * 3.6;

        var vehicle = FastWindows(slice, progress, L.VehicleWindowM, L.VehicleSpeedKmh);
        foreach (var (s, e) in vehicle)
            findings.Add(new Finding(FindingCode.VehicleSpeed, Severity.Reject,
                $"Rychlost {Kmh(s, e):0} km/h na {progress[e] - progress[s]:0} m trasy. Odpovídá autu, vlaku nebo kolu.",
                slice[s].Time, Km(a.Along[s])));

        var suspicious = FastWindows(slice, progress, L.SuspiciousWindowM, L.SuspiciousSpeedKmh);
        foreach (var (s, e) in suspicious)
        {
            if (vehicle.Any(v => v.Start <= e && s <= v.End)) continue;
            findings.Add(new Finding(FindingCode.SuspiciousSpeed, Severity.ManualReview,
                $"Podezřelé tempo {Kmh(s, e):0.0} km/h na {(progress[e] - progress[s]) / 1000:0.0} km trasy.",
                slice[s].Time, Km(a.Along[s])));
        }
    }

    /// <summary>Merged index ranges where the speed over at least windowM of distance is above the limit.</summary>
    private static List<(int Start, int End)> FastWindows(
        List<TrackPoint> slice, double[] cum, double windowM, double limitKmh)
    {
        var ranges = new List<(int Start, int End)>();
        int j = 0;
        for (int i = 0; i < slice.Count; i++)
        {
            if (j < i) j = i;
            while (j < slice.Count && cum[j] - cum[i] < windowM) j++;
            if (j >= slice.Count) break;

            double secs = (slice[j].Time - slice[i].Time).TotalSeconds;
            double kmh = (cum[j] - cum[i]) / secs * 3.6;
            if (kmh <= limitKmh) continue;

            if (ranges.Count > 0 && ranges[^1].End >= i)
                ranges[^1] = (ranges[^1].Start, Math.Max(ranges[^1].End, j));
            else
                ranges.Add((i, j));
        }
        return ranges;
    }

    /// <summary>Marks 100 m bins of the route that the record walked through. Returns coverage in %.</summary>
    private double CheckCoverage(Analysis a, double rangeStart, double rangeEnd, List<Finding> findings)
    {
        if (rangeEnd <= rangeStart) return 100;
        int bins = (int)Math.Ceiling((rangeEnd - rangeStart) / CoverageBinM);
        var covered = new bool[bins];

        void Mark(double from, double to)
        {
            int b0 = (int)Math.Floor((from - rangeStart) / CoverageBinM);
            int b1 = (int)Math.Floor((to - rangeStart) / CoverageBinM);
            for (int b = Math.Max(0, b0); b <= Math.Min(bins - 1, b1); b++) covered[b] = true;
        }

        int last = -1;
        for (int i = 0; i < a.Along.Length; i++)
        {
            if (a.Along[i] is not { } here) continue;
            Mark(here, here);
            if (last >= 0 && a.Along[last] is { } before)
            {
                double delta = here - before;
                double walked = a.Cumulative[i] - a.Cumulative[last];
                // Only fill between points that plausibly walked the route in between.
                if (delta >= 0 && delta <= 500 && delta <= walked * 1.5 + 100)
                    Mark(before, here);
            }
            last = i;
        }

        int listed = 0;
        for (int b = 0; b < bins; b++)
        {
            if (covered[b]) continue;
            int s = b;
            while (b < bins && !covered[b]) b++;
            double len = (b - s) * CoverageBinM;
            if (len > L.MaxUncoveredStretchM && listed++ < MaxListedFindingsPerCode)
            {
                double fromKm = (rangeStart + s * CoverageBinM) / 1000;
                double toKm = Math.Min(rangeEnd, rangeStart + b * CoverageBinM) / 1000;
                findings.Add(new Finding(FindingCode.UncoveredStretch, Severity.ManualReview,
                    $"Úsek trasy km {fromKm:0.0}–{toKm:0.0} ({len / 1000:0.0} km) není v záznamu pokrytý.",
                    RouteKm: fromKm));
            }
        }

        double pct = covered.Count(c => c) * 100.0 / bins;
        if (pct < L.MinCoveragePercent)
            findings.Add(new Finding(FindingCode.LowCoverage, Severity.Reject,
                $"Záznam pokrývá jen {pct:0.0} % trasy (minimum {L.MinCoveragePercent:0} %)."));
        return pct;
    }

    private void CheckDuplicates(string fingerprint, DateTimeOffset? start, DateTimeOffset? finish,
        IEnumerable<KnownAttempt>? known, List<Finding> findings)
    {
        if (known is null) return;
        foreach (var k in known)
        {
            if (k.Fingerprint == fingerprint)
            {
                findings.Add(new Finding(FindingCode.DuplicateRecord, Severity.Reject,
                    $"Stejný záznam už byl nahraný (pokus {k.Id})."));
                continue;
            }
            if (start is { } s && finish is { } f &&
                Math.Abs((k.StartUtc - s).TotalSeconds) <= L.SimilarAttemptToleranceS &&
                Math.Abs((k.FinishUtc - f).TotalSeconds) <= L.SimilarAttemptToleranceS)
                findings.Add(new Finding(FindingCode.SimilarAttempt, Severity.ManualReview,
                    $"Pokus {k.Id} má skoro stejný start i cíl. Může jít o skupinu, nebo o cizí záznam."));
        }
    }

    // ---- helpers ----

    private static Verdict VerdictOf(IEnumerable<Finding> findings) =>
        findings.Any(f => f.Severity == Severity.Reject) ? Verdict.Rejected :
        findings.Any(f => f.Severity == Severity.ManualReview) ? Verdict.ManualReview :
        Verdict.Accepted;

    private static ValidationResult Fail(RouteVariant v, string fingerprint, Finding finding, int count) =>
        Fail(v, fingerprint, [finding], count);

    private static ValidationResult Fail(RouteVariant v, string fingerprint, List<Finding> findings, int count) =>
        new()
        {
            Verdict = VerdictOf(findings),
            Variant = v,
            Fingerprint = fingerprint,
            Findings = findings,
            PointCount = count,
        };

    private static double? Km(double? alongM) => alongM / 1000;

    private static string Far(double m) => double.IsInfinity(m) ? "víc než 5 km" : $"{m:0} m";

    private static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" :
        t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds} s" :
        $"{t.Seconds} s";
}
