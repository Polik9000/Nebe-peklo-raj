
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
        ValidationOptions? options = null)
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
        return Validate(tracks, variant, options);
    }

    /// <summary>
    /// Validates the attempt. For an unfinished or rejected full-route attempt that reached Peklo,
    /// Stage 1 is validated as well and returned in <see cref="ValidationResult.Stage1Fallback"/>.
    /// </summary>
    public ValidationResult Validate(IEnumerable<GpxTrack> gpxFiles, RouteVariant variant,
        ValidationOptions? options = null)
    {
        var files = gpxFiles.ToList();
        options ??= new ValidationOptions();
        var result = ValidateVariant(files, variant, options);
        if (variant == RouteVariant.Full && result.Verdict == Verdict.Rejected)
        {
            // IsRecordCandidate is about the full-route time; Stage 1 is asked through IsRecordTime.
            var stage1 = ValidateVariant(files, RouteVariant.Stage1, options with { IsRecordCandidate = false });
            if (stage1.Verdict != Verdict.Rejected)
                result = result with { Stage1Fallback = stage1 };
        }
        return result;
    }

    private ValidationResult ValidateVariant(List<GpxTrack> files, RouteVariant variant, ValidationOptions options)
    {
        var findings = new List<Finding>();
        var points = Merge(files, findings);
        if (points is null)
            return Fail(variant, "", findings, 0);

        var fingerprint = TrackFingerprint.Compute(points);
        CheckFuture(points, findings);
        int fileCount = files.Count(f => f.Points.Count > 0);
        if (fileCount > 1)
        {
            findings.Add(new Finding(FindingCode.MultipleFiles, Severity.ManualReview,
                $"Záznam je složený z {fileCount} souborů. Pravidla chtějí ruční schválení."));
            CheckFilesAgree(files, findings);
        }
        points = RemoveSpikes(points, findings);

        var checkpoints = CheckpointsFor(variant);
        var zones = FindZoneVisits(points, checkpoints, findings);
        if (zones is null)
        {
            // Without a start there is no departure; the first point is the best guess.
            var firstDate = points[0].Time;
            CheckLaunch(firstDate, findings);
            CheckDuplicates(fingerprint, null, null, null, options.KnownAttempts, findings);
            return Fail(variant, fingerprint, findings, points.Count) with
            {
                AttemptDateUtc = firstDate, RulesVersion = RulesFor(firstDate),
            };
        }

        var (departure, arrivals) = zones.Value;
        var slice = points.GetRange(departure, arrivals[^1] - departure + 1);
        double startAlong = AlongOf(checkpoints[0].Position, 0);
        double endAlong = AlongOf(checkpoints[^1].Position, _route.LengthM);

        // The attempt starts when the runner leaves the start zone, not when the watch was switched on.
        var startUtc = points[departure].Time;
        var finishUtc = points[arrivals[^1]].Time;
        var attemptDate = startUtc;
        var rules = RulesFor(attemptDate);
        CheckLaunch(attemptDate, findings);

        // Each stage runs like a standalone attempt: from leaving one zone to entering the next.
        // Time spent inside an intermediate zone (a rest in Peklo) counts for the full route only.
        var stages = new List<StageTime>();
        var prev = startUtc;
        for (int k = 0; k < arrivals.Length; k++)
        {
            var at = points[arrivals[k]].Time;
            stages.Add(new StageTime(StageName(checkpoints[k], checkpoints[k + 1]), prev, at));
            if (k + 1 < arrivals.Length)
                prev = points[LastInside(points, checkpoints[k + 1], arrivals[k], arrivals[k + 1])].Time;
        }
        if (variant != RouteVariant.Full) stages.Clear();

        var records = RecordVariants(variant, finishUtc - startUtc, stages, options);
        bool strict = records.Count > 0;
        if (strict)
            findings.Add(new Finding(FindingCode.RecordCandidate, Severity.ManualReview,
                $"Čas by byl rekord ({string.Join(", ", records.Select(VariantName))}). Rekordy schvaluje člověk."));

        var analysis = Analyze(slice, startAlong, findings);
        CheckDensity(slice, findings);
        CheckGaps(slice, analysis, strict, findings);
        CheckDetours(slice, analysis, options.DetourExplained, findings);
        double shortcutSaved = CheckShortcuts(slice, analysis, strict, findings);
        CheckSpeed(slice, analysis, findings);
        double coverage = CheckCoverage(analysis, startAlong + checkpoints[0].RadiusM,
            endAlong - checkpoints[^1].RadiusM, findings);

        var shape = ShapeOf(slice, analysis, startAlong, endAlong);
        CheckDuplicates(fingerprint, startUtc, finishUtc, shape, options.KnownAttempts, findings);

        return new ValidationResult
        {
            Verdict = VerdictOf(findings),
            Variant = variant,
            Fingerprint = fingerprint,
            Findings = findings,
            AttemptDateUtc = attemptDate,
            RulesVersion = rules,
            ShortcutSavedM = shortcutSaved,
            StartUtc = startUtc,
            FinishUtc = finishUtc,
            Stages = stages,
            RouteLengthM = Math.Max(0, endAlong - startAlong),
            RecordedDistanceM = analysis.Cumulative[^1],
            CoveragePercent = coverage,
            PointCount = points.Count,
            Shape = shape,
            RecordVariants = records,
        };
    }

    /// <summary>Which of the measured times would be records.</summary>
    private static List<RouteVariant> RecordVariants(RouteVariant variant, TimeSpan elapsed,
        List<StageTime> stages, ValidationOptions options)
    {
        var list = new List<RouteVariant>();
        if (options.IsRecordCandidate || options.IsRecordTime?.Invoke(variant, elapsed) == true)
            list.Add(variant);
        if (options.IsRecordTime is { } isRecord && stages.Count == 2)
        {
            if (isRecord(RouteVariant.Stage1, stages[0].Elapsed)) list.Add(RouteVariant.Stage1);
            if (isRecord(RouteVariant.Stage2, stages[1].Elapsed)) list.Add(RouteVariant.Stage2);
        }
        return list;
    }

    private static string VariantName(RouteVariant v) => v switch
    {
        RouteVariant.Full => "celá trasa",
        RouteVariant.Stage1 => "Etapa 1",
        _ => "Etapa 2",
    };

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
    /// Watch and phone may overlap in time, but then they must be in the same place.
    /// Two files from different places at the same time are two people or a made-up record.
    /// </summary>
    private void CheckFilesAgree(List<GpxTrack> files, List<Finding> findings)
    {
        var timed = files
            .Select(f => f.Points
                .Where(p => p.Position.IsValid && p.Time is not null)
                .Select(p => new TrackPoint(p.Position, p.Time!.Value.ToUniversalTime()))
                .OrderBy(p => p.Time)
                .ToList())
            .Where(l => l.Count > 0)
            .ToList();

        for (int a = 0; a < timed.Count; a++)
        for (int b = a + 1; b < timed.Count; b++)
        {
            var A = timed[a];
            var B = timed[b];
            double worst = 0;
            DateTimeOffset? at = null;
            int j = 0;
            foreach (var p in B)
            {
                if (p.Time < A[0].Time || p.Time > A[^1].Time) continue;
                while (j + 1 < A.Count && A[j + 1].Time <= p.Time) j++;
                // Nearest point of A in time, at most one recording interval away.
                var q = j + 1 < A.Count && (A[j + 1].Time - p.Time) < (p.Time - A[j].Time) ? A[j + 1] : A[j];
                if (Math.Abs((q.Time - p.Time).TotalSeconds) > L.MaxPointIntervalS) continue;
                double d = Geo.Distance(p.Position, q.Position);
                if (d > worst) { worst = d; at = p.Time; }
            }
            if (worst > L.FilesMaxApartM)
                findings.Add(new Finding(FindingCode.FilesDisagree, Severity.ManualReview,
                    $"Soubory {a + 1} a {b + 1} se ve stejný čas liší o {worst:0} m. Nahrálo je jedno zařízení na jednom člověku?",
                    at));
        }
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

    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    /// <summary>The attempt's date in Czech local time, taken from the first GPX point.</summary>
    private static DateOnly LocalDate(DateTimeOffset t) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, Prague).DateTime);

    /// <summary>The rules version in force on the attempt's start day.</summary>
    private string? RulesFor(DateTimeOffset attemptDate)
    {
        var day = LocalDate(attemptDate);
        return _config.Rules?
            .Where(r => r.From <= day)
            .OrderBy(r => r.From)
            .LastOrDefault()?.Version;
    }

    private void CheckLaunch(DateTimeOffset attemptDate, List<Finding> findings)
    {
        if (_config.AcceptFrom is { } from && LocalDate(attemptDate) < from)
            findings.Add(new Finding(FindingCode.BeforeLaunch, Severity.Reject,
                $"Pokus začal {LocalDate(attemptDate):d. M. yyyy}, uznáváme pokusy od {from:d. M. yyyy}.", attemptDate));
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

    /// <summary>Last point inside the zone between arriving there and arriving at the next zone.</summary>
    private static int LastInside(List<TrackPoint> points, Checkpoint cp, int arrival, int nextArrival)
    {
        for (int i = nextArrival - 1; i > arrival; i--)
            if (cp.Contains(points[i].Position)) return i;
        return arrival;
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

    private void CheckGaps(List<TrackPoint> slice, Analysis a, bool strict, List<Finding> findings)
    {
        int recordPauses = 0;
        int pauses = 0, listed = 0;
        TimeSpan pauseTotal = TimeSpan.Zero;
        var maxGap = TimeSpan.FromHours(L.MaxGapHours);
        for (int i = 1; i < slice.Count; i++)
        {
            var dt = slice[i].Time - slice[i - 1].Time;
            if (dt.TotalSeconds <= L.MaxPointIntervalS) continue;

            // Sleep, auto-pause or a dead battery: fine if the record resumes where it stopped.
            double d = a.Cumulative[i] - a.Cumulative[i - 1];
            if (d <= L.ResumeMaxDistanceM)
            {
                if (dt <= maxGap)
                {
                    pauses++;
                    pauseTotal += dt;
                    // A car ride home with the recording off looks exactly like this. For a record,
                    // the reviewer asks where the runner spent the time.
                    if (strict && dt.TotalMinutes >= L.RecordPauseReviewMin && recordPauses++ < MaxListedFindingsPerCode)
                        findings.Add(new Finding(FindingCode.RecordPause, Severity.ManualReview,
                            $"Rekordní pokus: mezera v záznamu {Duration(dt)} s návratem na stejné místo. Potřebujeme krátký popis, kde jsi byl (nocleh, pauza).",
                            slice[i - 1].Time, Km(a.Along[i - 1])));
                }
                else
                {
                    findings.Add(new Finding(FindingCode.LongGap, Severity.ManualReview,
                        $"Mezera v záznamu {Duration(dt)} je delší než {L.MaxGapHours:0} h. Záznam pokračuje {d:0} m od místa, kde skončil.",
                        slice[i - 1].Time, Km(a.Along[i - 1])));
                }
                continue;
            }

            if (listed++ >= MaxListedFindingsPerCode) continue;
            double kmh = d / dt.TotalSeconds * 3.6;
            findings.Add(new Finding(FindingCode.RecordingGap, Severity.ManualReview,
                $"Mezera v záznamu {Duration(dt)} a záznam pokračuje {d:0} m od místa, kde skončil (víc než {L.ResumeMaxDistanceM:0} m, průměr {kmh:0.0} km/h).",
                slice[i - 1].Time, Km(a.Along[i - 1])));
        }

        if (listed > MaxListedFindingsPerCode)
            findings.Add(new Finding(FindingCode.RecordingGap, Severity.ManualReview,
                $"A dalších {listed - MaxListedFindingsPerCode} mezer v záznamu."));
        if (pauses > 0)
            findings.Add(new Finding(FindingCode.Pause, Severity.Info,
                $"{pauses}× mezera v záznamu s návratem na stejné místo, celkem {Duration(pauseTotal)}. Čas běží dál."));
    }

    private void CheckDetours(List<TrackPoint> slice, Analysis a, bool explained, List<Finding> findings)
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

            // Detours on foot may be any length, but the runner must come back where they left.
            double exit = a.Along[prev] ?? 0, back = a.Along[next] ?? exit;
            double skipped = back - exit;

            if (count == 1 && duration.TotalSeconds <= 2 * L.MaxPointIntervalS)
                findings.Add(new Finding(FindingCode.GpsGlitch, Severity.Info,
                    $"Ojedinělý bod {Far(maxOff)} od trasy, nejspíš chyba GPS.", at, km));
            else if (skipped <= L.DetourReturnToleranceM)
                findings.Add(new Finding(FindingCode.Detour, Severity.ManualReview,
                    "Odbočka s návratem na stejné místo: " + desc, at, km));
            else if (length >= skipped)
                findings.Add(new Finding(FindingCode.Detour, Severity.ManualReview,
                    $"Odbočka s návratem o {skipped:0} m dál po trase, ale ušel víc, než přeskočil: " + desc, at, km));
            else if (explained)
                findings.Add(new Finding(FindingCode.OffRouteShortcut, Severity.ManualReview,
                    $"Objížďka, která zkracuje trasu: přeskočeno {skipped:0} m trasy, ušel jen {length:0} m. Běžec poslal vysvětlení, posoudí člověk. " + desc, at, km));
            else
                findings.Add(new Finding(FindingCode.OffRouteShortcut, Severity.Reject,
                    $"Zkratka mimo koridor: přeskočeno {skipped:0} m trasy, ušel jen {length:0} m. " + desc, at, km));
        }
    }

    /// <summary>
    /// Shortcuts inside the corridor (a cut switchback): the record moves further along the route
    /// than it walked, and faster than the runner's usual pace. One such place is usually GPS noise; repeated ones matter for records.
    /// Returns the total metres saved.
    /// </summary>
    private double CheckShortcuts(List<TrackPoint> slice, Analysis a, bool isRecordCandidate, List<Finding> findings)
    {
        // The runner's usual speed along the route. Sparse GPS points cut corners of a curvy
        // route, so walked distance alone would look like a shortcut; progress much faster than
        // the usual pace cannot be explained that way.
        var paces = new List<double>();
        for (int i = 1; i < slice.Count; i++)
        {
            double dt = (slice[i].Time - slice[i - 1].Time).TotalSeconds;
            if (dt <= 0 || dt > L.MaxPointIntervalS) continue;
            if (a.Along[i] is not { } x1 || a.Along[i - 1] is not { } x0) continue;
            if ((a.Cumulative[i] - a.Cumulative[i - 1]) / dt < 0.5) continue; // standing
            paces.Add(Math.Max(0, x1 - x0) / dt);
        }
        paces.Sort();
        double usualPace = paces.Count > 0 ? paces[paces.Count / 2] : 0;

        var events = new List<(int Start, int End, double Saved)>();
        int j = 0;
        for (int i = 0; i < slice.Count; i++)
        {
            if (a.Along[i] is not { } from) continue;
            if (j < i) j = i;
            while (j + 1 < slice.Count && (slice[j + 1].Time - slice[i].Time).TotalSeconds <= L.ShortcutWindowS) j++;

            // Furthest point in the window that is still on the route.
            int k = j;
            while (k > i && a.Along[k] is null) k--;
            if (k == i || a.Along[k] is not { } to) continue;

            double gain = to - from;
            double walked = a.Cumulative[k] - a.Cumulative[i];
            double secs = (slice[k].Time - slice[i].Time).TotalSeconds;
            double explained = Math.Max(walked, usualPace * secs * L.ShortcutPaceTolerance);
            double saved = gain - explained;
            if (gain > 3000 || saved < L.ShortcutMinSavedM) continue;

            if (events.Count > 0 && events[^1].End >= i)
                events[^1] = (events[^1].Start, Math.Max(events[^1].End, k), Math.Max(events[^1].Saved, saved));
            else
                events.Add((i, k, saved));
        }

        double total = events.Sum(e => e.Saved);
        bool repeated = events.Count >= L.RepeatedShortcutCount;
        foreach (var (s, _, saved) in events.Take(MaxListedFindingsPerCode))
        {
            // For a record even one big cut matters: GPS noise rarely saves that much.
            bool big = saved >= L.RecordShortcutM;
            var severity = isRecordCandidate && (repeated || big) ? Severity.ManualReview : Severity.Info;
            findings.Add(new Finding(FindingCode.Shortcut, severity,
                repeated ? $"Opakované zkracování v koridoru: tady o {saved:0} m." :
                big ? $"Velké zkrácení v koridoru o {saved:0} m." :
                $"Ojedinělé zkrácení o {saved:0} m, nejspíš nepřesnost GPS.",
                slice[s].Time, Km(a.Along[s])));
        }
        return total;
    }

    /// <summary>Flags paces of a vehicle (reject) and of an unusually fast runner (manual review).</summary>
    private void CheckSpeed(List<TrackPoint> slice, Analysis a, List<Finding> findings)
    {
        // Each step counts the smaller of route progress and walked distance: GPS jitter
        // lengthens the walked line but not the progress, a shortcut does the opposite.
        // Off the route only the walked distance is known, so a car on a parallel road still shows.
        var progress = new double[slice.Count];
        double max = a.Along.FirstOrDefault(x => x is not null) ?? 0;
        for (int i = 1; i < slice.Count; i++)
        {
            double walked = a.Cumulative[i] - a.Cumulative[i - 1];
            double gain = 0;
            if (a.Along[i] is { } here && here > max)
            {
                gain = here - max;
                max = here;
            }
            progress[i] = progress[i - 1] + (a.OnRoute[i] ? Math.Min(gain, walked) : walked);
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

        // A bike at 15 km/h is a normal pace for a runner, but not for someone who walks 5 km/h.
        double usual = UsualMovingKmh(slice, progress);
        double ownLimit = Math.Max(L.OwnPaceMinKmh, usual * L.OwnPaceFactor);
        if (usual <= 0 || ownLimit >= L.SuspiciousSpeedKmh) return;
        int listed = 0;
        foreach (var (s, e) in FastWindows(slice, progress, L.SuspiciousWindowM, ownLimit))
        {
            if (vehicle.Any(v => v.Start <= e && s <= v.End) || suspicious.Any(v => v.Start <= e && s <= v.End)) continue;
            if (listed++ >= MaxListedFindingsPerCode) break;
            findings.Add(new Finding(FindingCode.FastForRunner, Severity.ManualReview,
                $"Tempo {Kmh(s, e):0.0} km/h na {(progress[e] - progress[s]) / 1000:0.0} km trasy je víc než {L.OwnPaceFactor:0.#}× rychlejší než tvoje obvyklé tempo {usual:0.0} km/h.",
                slice[s].Time, Km(a.Along[s])));
        }
    }

    /// <summary>Median speed over 10-minute pieces of the record that were not standing still.</summary>
    private static double UsualMovingKmh(List<TrackPoint> slice, double[] progress)
    {
        var speeds = new List<double>();
        int s = 0;
        for (int i = 1; i < slice.Count; i++)
        {
            double secs = (slice[i].Time - slice[s].Time).TotalSeconds;
            if (secs < 600) continue;
            double kmh = (progress[i] - progress[s]) / secs * 3.6;
            if (kmh >= 1.5) speeds.Add(kmh); // slower than that is a rest
            s = i;
        }
        if (speeds.Count == 0) return 0;
        speeds.Sort();
        return speeds[speeds.Count / 2];
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

    /// <summary>
    /// Time and place where the record first got past each bin boundary of the route, interpolated
    /// between the two points around it so that moving the points a little changes little. Boundaries
    /// sit at whole multiples of the bin, so a stage attempt lines up with a full-route attempt.
    /// </summary>
    private TrackShape ShapeOf(List<TrackPoint> slice, Analysis a, double startAlong, double endAlong)
    {
        double bin = L.ShapeBinM;
        double from = Math.Ceiling(startAlong / bin) * bin;
        int count = Math.Max(0, (int)Math.Floor((endAlong - from) / bin) + 1);
        var samples = new ShapeSample?[count];
        var t0 = slice[0].Time;
        int next = 0, last = -1;
        for (int i = 0; i < slice.Count && next < count; i++)
        {
            if (a.Along[i] is not { } here) continue;
            while (next < count && here >= from + next * bin)
            {
                double boundary = from + next * bin;
                if (last >= 0 && a.Along[last] is { } before && before < boundary &&
                    (slice[i].Time - slice[last].Time).TotalSeconds <= 2 * L.MaxPointIntervalS)
                {
                    double f = (boundary - before) / (here - before);
                    var p0 = slice[last];
                    var p1 = slice[i];
                    double secs = (p0.Time - t0).TotalSeconds + f * (p1.Time - p0.Time).TotalSeconds;
                    samples[next] = new ShapeSample((int)Math.Round(secs),
                        Math.Round(p0.Position.Lat + f * (p1.Position.Lat - p0.Position.Lat), 6),
                        Math.Round(p0.Position.Lon + f * (p1.Position.Lon - p0.Position.Lon), 6));
                }
                next++;
            }
            if (last < 0 || a.Along[last] is not { } max || here > max) last = i;
        }
        return new TrackShape(from, bin, samples);
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
        TrackShape? shape, IEnumerable<KnownAttempt>? known, List<Finding> findings)
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
            {
                findings.Add(new Finding(FindingCode.SimilarAttempt, Severity.ManualReview,
                    $"Pokus {k.Id} má skoro stejný start i cíl. Může jít o skupinu, nebo o cizí záznam."));
                continue;
            }
            // A copy with shifted times or moved points has a new hash but the same shape.
            if (shape is not null && k.Shape is not null &&
                shape.MatchPercent(k.Shape, L.CopyTimeToleranceS, L.CopyDistanceToleranceM) is { } pct &&
                pct >= L.CopyMatchPercent)
                findings.Add(new Finding(FindingCode.CopiedTrack, Severity.ManualReview,
                    $"Stopa i mezičasy se na {pct:0} % trasy shodují s pokusem {k.Id}, i když soubor je jiný. Může jít o upravenou kopii cizího záznamu."));
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
