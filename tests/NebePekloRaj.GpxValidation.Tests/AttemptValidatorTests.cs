using Xunit;
using static NebePekloRaj.GpxValidation.Tests.StraightRoute;

namespace NebePekloRaj.GpxValidation.Tests;

/// <summary>Rule checks on the straight 10 km test route, walked at 10 km/h with a point every 10 s.</summary>
public class AttemptValidatorTests
{
    private const double Kmh = 10;

    private static ValidationResult Check(TrackBuilder b, RouteVariant v = RouteVariant.Full,
        IEnumerable<KnownAttempt>? known = null) =>
        Validator().Validate([b.Build()], v, known);

    private static TrackBuilder CleanRun() => Walker().Stand(TimeSpan.FromMinutes(1)).Walk(Line(0, LengthM), Kmh);

    private static bool Has(ValidationResult r, FindingCode c) => r.Findings.Any(f => f.Code == c);

    [Fact]
    public void Clean_run_is_accepted_with_times()
    {
        var r = Check(CleanRun());

        Assert.Equal(Verdict.Accepted, r.Verdict);
        // Timing runs from leaving the 150 m start zone to entering the 150 m finish zone: 9.7 km at 10 km/h.
        Approx.Near(TimeSpan.FromSeconds(9_700 / (Kmh / 3.6)).TotalSeconds, r.Elapsed!.Value.TotalSeconds, 100);
        Assert.Equal(2, r.Stages.Count);
        Approx.Near(3_850 / (Kmh / 3.6), r.Stages[0].Elapsed.TotalSeconds, 100);
        Assert.Equal(r.Elapsed, r.Stages[0].Elapsed + r.Stages[1].Elapsed);
        Approx.Near(LengthM, r.RouteLengthM, 10);
        Assert.True(r.CoveragePercent > 99);
    }

    [Fact]
    public void Waiting_at_start_does_not_count()
    {
        var quick = Check(CleanRun());
        var waited = Check(Walker().Stand(TimeSpan.FromMinutes(30)).Walk(Line(0, LengthM), Kmh));

        Assert.Equal(quick.Elapsed!.Value.TotalSeconds, waited.Elapsed!.Value.TotalSeconds, 0);
    }

    [Fact]
    public void Pause_on_route_counts_into_time_and_is_ok()
    {
        var b = Walker().Walk(Line(0, 6_000), Kmh).Skip(TimeSpan.FromMinutes(30)).Stand(TimeSpan.FromSeconds(10))
            .Walk(Line(6_000, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Assert.True(Has(r, FindingCode.Pause));
        Assert.True(r.Elapsed > Check(CleanRun()).Elapsed!.Value + TimeSpan.FromMinutes(29));
    }

    [Fact]
    public void Planned_route_without_times_is_rejected()
    {
        var plan = new GpxTrack(Line(0, LengthM).Select(p => new GpxPoint(p, null, null)).ToList());

        var r = Validator().Validate([plan], RouteVariant.Full);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.MissingTimestamps));
    }

    [Fact]
    public void Reverse_direction_is_rejected()
    {
        var b = new TrackBuilder(At(LengthM), T0).Walk(Line(LengthM, 0), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.WrongDirection));
    }

    [Fact]
    public void Missing_start_is_rejected()
    {
        var b = new TrackBuilder(At(1_000), T0).Walk(Line(1_000, LengthM), Kmh);

        Assert.True(Has(Check(b), FindingCode.StartNotReached));
    }

    [Fact]
    public void Missing_finish_is_rejected()
    {
        var r = Check(Walker().Walk(Line(0, 9_700), Kmh));

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.FinishNotReached));
    }

    [Fact]
    public void Skipping_peklo_is_rejected()
    {
        // Leaves the route before Peklo and rejoins after it, staying 300 m north.
        var b = Walker().Walk(Line(0, 3_500), Kmh)
            .Walk([At(3_600, 300), At(4_600, 300), At(4_700, 0)], Kmh)
            .Walk(Line(4_700, LengthM), Kmh);

        Assert.True(Has(Check(b), FindingCode.PekloNotReached));
    }

    [Fact]
    public void Short_detour_needs_manual_review()
    {
        var b = Walker().Walk(Line(0, 3_000), Kmh)
            .Walk([At(3_100, 400), At(3_200, 0)], Kmh)
            .Walk(Line(3_200, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        var f = Assert.Single(r.Findings, f => f.Code == FindingCode.ShortDetour);
        Assert.Equal(3.0, f.RouteKm!.Value, 0);
    }

    [Fact]
    public void Long_detour_is_rejected()
    {
        var b = Walker().Walk(Line(0, 5_000), Kmh)
            .Walk([At(5_500, 1_000), At(6_500, 1_000), At(7_000, 0)], Kmh)
            .Walk(Line(7_000, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.LongDetour));
    }

    [Fact]
    public void Single_gps_spike_is_ignored()
    {
        var b = CleanRun();
        int i = b.Points.Count / 2;
        b.Points[i] = b.Points[i] with { Position = Geo.Offset(b.Points[i].Position, 0, 800) };

        var r = Check(b);

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Assert.True(Has(r, FindingCode.GpsGlitch));
    }

    [Fact]
    public void Gap_while_moving_needs_manual_review()
    {
        var b = Walker().Walk(Line(0, 5_000), Kmh)
            .JumpTo(At(5_400), TimeSpan.FromSeconds(400 / (Kmh / 3.6)))
            .Walk(Line(5_400, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.Single(r.Findings, f => f.Code == FindingCode.RecordingGap);
    }

    [Fact]
    public void Big_gap_leaves_uncovered_route()
    {
        var b = Walker().Walk(Line(0, 5_000), Kmh)
            .JumpTo(At(5_800), TimeSpan.FromMinutes(8))
            .Walk(Line(5_800, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.True(Has(r, FindingCode.UncoveredStretch));
        Assert.InRange(r.CoveragePercent, 90, 95);
    }

    [Fact]
    public void Car_ride_is_rejected()
    {
        var b = Walker().Walk(Line(0, 5_000), Kmh)
            .Walk(Line(5_000, 7_000), 60)
            .Walk(Line(7_000, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        var f = Assert.Single(r.Findings, f => f.Code == FindingCode.VehicleSpeed);
        Assert.InRange(f.RouteKm!.Value, 4.4, 5.1);
    }

    [Fact]
    public void Car_ride_hidden_in_a_gap_is_rejected()
    {
        var b = Walker().Walk(Line(0, 4_300), Kmh)
            .JumpTo(At(9_000), TimeSpan.FromMinutes(5))
            .Walk(Line(9_000, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.VehicleSpeed));
    }

    [Fact]
    public void Very_fast_running_needs_manual_review()
    {
        var b = Walker().Walk(Line(0, 3_000), Kmh)
            .Walk(Line(3_000, 7_000), 21)
            .Walk(Line(7_000, LengthM), Kmh);

        var r = Check(b);

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.True(Has(r, FindingCode.SuspiciousSpeed));
        Assert.False(Has(r, FindingCode.VehicleSpeed));
    }

    [Fact]
    public void Sparse_recording_is_rejected()
    {
        var r = Check(Walker(intervalS: 120).Walk(Line(0, LengthM), Kmh));

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.LowPointDensity));
    }

    [Fact]
    public void Recording_every_60_s_is_enough()
    {
        var r = Check(Walker(intervalS: 60).Walk(Line(0, LengthM), Kmh));

        Assert.Equal(Verdict.Accepted, r.Verdict);
    }

    [Fact]
    public void Watch_and_phone_files_are_merged()
    {
        var all = CleanRun().Build().Points;
        int cut = all.Count / 2;
        var watch = new GpxTrack(all.Take(cut + 20).ToList());
        var phone = new GpxTrack(all.Skip(cut).ToList()); // 20 points overlap

        var single = Validator().Validate([new GpxTrack(all)], RouteVariant.Full);
        var merged = Validator().Validate([phone, watch], RouteVariant.Full);

        Assert.Equal(Verdict.Accepted, merged.Verdict);
        Assert.Equal(single.Elapsed, merged.Elapsed);
        Assert.Equal(single.Fingerprint, merged.Fingerprint);
    }

    [Fact]
    public void Same_record_uploaded_twice_is_rejected()
    {
        var first = Check(CleanRun());
        var known = new KnownAttempt("A-1", first.Fingerprint, first.StartUtc!.Value, first.FinishUtc!.Value);

        var r = Check(CleanRun(), known: [known]);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.DuplicateRecord));
    }

    [Fact]
    public void Same_start_and_finish_from_another_record_needs_review()
    {
        var first = Check(CleanRun());
        var known = new KnownAttempt("A-1", "other", first.StartUtc!.Value.AddSeconds(30), first.FinishUtc!.Value.AddSeconds(-40));

        var r = Check(CleanRun(), known: [known]);

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.True(Has(r, FindingCode.SimilarAttempt));
    }

    [Fact]
    public void Time_in_future_is_rejected()
    {
        var b = new TrackBuilder(At(0), new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)).Walk(Line(0, LengthM), Kmh);

        Assert.True(Has(Check(b), FindingCode.TimeInFuture));
    }

    [Fact]
    public void Stage1_ends_at_peklo()
    {
        var r = Check(Walker().Walk(Line(0, 4_200), Kmh), RouteVariant.Stage1);

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Approx.Near(3_700 / (Kmh / 3.6), r.Elapsed!.Value.TotalSeconds, 100);
        Assert.Empty(r.Stages);
    }

    [Fact]
    public void Stage2_starts_when_leaving_peklo()
    {
        var b = new TrackBuilder(At(PekloM), T0).Stand(TimeSpan.FromMinutes(20)).Walk(Line(PekloM, LengthM), Kmh);

        var r = Check(b, RouteVariant.Stage2);

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Approx.Near(5_700 / (Kmh / 3.6), r.Elapsed!.Value.TotalSeconds, 100);
        Approx.Near(6_000, r.RouteLengthM, 10);
    }

    [Fact]
    public void Invalid_xml_stream_is_rejected_not_thrown()
    {
        var r = Validator().Validate([new MemoryStream("not xml"u8.ToArray())], RouteVariant.Full);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(Has(r, FindingCode.InvalidGpx));
    }
}
