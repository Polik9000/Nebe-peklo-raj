using Xunit;
using static NebePekloRaj.GpxValidation.Tests.StraightRoute;

namespace NebePekloRaj.GpxValidation.Tests;

/// <summary>Checks that close gaps between what the rules promise and what the automat catches.</summary>
public class StricterChecksTests
{
    private static ValidationResult Check(TrackBuilder b, ValidationOptions? o = null, RouteVariant v = RouteVariant.Full) =>
        Validator().Validate([b.Build()], v, o);

    private static bool Has(ValidationResult r, FindingCode c) => r.Findings.Any(f => f.Code == c);

    private static TrackBuilder Run(double kmh) => Walker().Stand(TimeSpan.FromMinutes(1)).Walk(Line(0, LengthM), kmh);

    // ---- speed against the runner's own pace ----

    [Fact]
    public void Walker_on_a_bike_at_15_kmh_needs_manual_review()
    {
        var b = Walker().Walk(Line(0, 3_000), 5).Walk(Line(3_000, 7_000), 15).Walk(Line(7_000, LengthM), 5);

        var r = Check(b);

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.True(Has(r, FindingCode.FastForRunner), Review.Describe(r));
        Assert.False(Has(r, FindingCode.SuspiciousSpeed));
    }

    [Fact]
    public void Runner_speeding_up_downhill_is_fine()
    {
        var b = Walker().Walk(Line(0, 3_000), 10).Walk(Line(3_000, 7_000), 14).Walk(Line(7_000, LengthM), 10);

        var r = Check(b);

        Assert.Equal(Verdict.Accepted, r.Verdict);
    }

    [Fact]
    public void Steady_walker_is_fine()
    {
        Assert.Equal(Verdict.Accepted, Check(Run(5)).Verdict);
    }

    // ---- copied records ----

    private static KnownAttempt Known(ValidationResult r, string id) =>
        new(id, r.Fingerprint, r.StartUtc!.Value, r.FinishUtc!.Value, r.Shape);

    [Fact]
    public void Copy_with_shifted_time_and_moved_points_needs_manual_review()
    {
        var original = Check(Run(10));
        var copy = Run(10);
        for (int i = 0; i < copy.Points.Count; i++)
            copy.Points[i] = copy.Points[i] with
            {
                Position = Geo.Offset(copy.Points[i].Position, 1, 1),
                Time = copy.Points[i].Time!.Value.AddDays(3).AddSeconds(1),
            };

        var r = Check(copy, new ValidationOptions([Known(original, "A-1")]));

        Assert.NotEqual(original.Fingerprint, r.Fingerprint);
        Assert.False(Has(r, FindingCode.DuplicateRecord));
        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.True(Has(r, FindingCode.CopiedTrack), Review.Describe(r));
    }

    [Fact]
    public void Another_runner_on_another_day_is_not_a_copy()
    {
        var original = Check(Run(10));
        var other = new TrackBuilder(At(0), T0.AddDays(2), intervalS: 7).Walk(Line(0, LengthM), 10.6);
        var rnd = new Random(7);
        for (int i = 0; i < other.Points.Count; i++)
            other.Points[i] = other.Points[i] with
            {
                Position = Geo.Offset(other.Points[i].Position, rnd.NextDouble() * 10 - 5, rnd.NextDouble() * 10 - 5),
            };

        var r = Check(other, new ValidationOptions([Known(original, "A-1")]));

        Assert.Equal(Verdict.Accepted, r.Verdict);
    }

    [Fact]
    public void Shape_survives_storage_and_lines_up_with_a_stage()
    {
        var full = Check(Run(10));
        var stage2 = Check(new TrackBuilder(At(PekloM), T0).Walk(Line(PekloM, LengthM), 10), v: RouteVariant.Stage2);

        var decoded = TrackShape.Decode(full.Shape!.Encode());

        Assert.Equal(full.Shape.Samples.Count, decoded.Samples.Count);
        Assert.Equal(100, decoded.MatchPercent(full.Shape, 0, 1));
        // Stage 2 bins sit on the same route metres as the full route's.
        Assert.Equal(0, (stage2.Shape!.FromM - full.Shape.FromM) % full.Shape.BinM);
    }

    // ---- several files ----

    [Fact]
    public void Files_recorded_at_the_same_time_in_different_places_need_review()
    {
        var mine = Run(10).Build();
        var friend = new GpxTrack(mine.Points
            .Select(p => p with { Position = Geo.Offset(p.Position, 0, 1_000) })
            .Skip(100).Take(200).ToList());

        var r = Validator().Validate([mine, friend], RouteVariant.Full);

        Assert.True(Has(r, FindingCode.FilesDisagree), Review.Describe(r));
    }

    [Fact]
    public void Overlapping_watch_and_phone_in_one_place_agree()
    {
        var all = Run(10).Build().Points;
        var watch = new GpxTrack(all.Take(all.Count / 2 + 30).ToList());
        var phone = new GpxTrack(all.Skip(all.Count / 2)
            .Select(p => p with { Position = Geo.Offset(p.Position, 6, -4), Time = p.Time!.Value.AddSeconds(3) }).ToList());

        var r = Validator().Validate([watch, phone], RouteVariant.Full);

        Assert.False(Has(r, FindingCode.FilesDisagree), Review.Describe(r));
    }

    // ---- records ----

    private static TrackBuilder Overnight() =>
        Walker().Walk(Line(0, 6_000), 10).Skip(TimeSpan.FromHours(8)).Stand(TimeSpan.FromSeconds(10)).Walk(Line(6_000, LengthM), 10);

    [Fact]
    public void Record_with_a_night_gap_asks_where_the_runner_slept()
    {
        var r = Check(Overnight(), new ValidationOptions(IsRecordCandidate: true));

        Assert.True(Has(r, FindingCode.RecordPause), Review.Describe(r));
    }

    [Fact]
    public void Night_gap_without_a_record_is_fine()
    {
        var r = Check(Overnight());

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Assert.False(Has(r, FindingCode.RecordPause));
    }

    [Fact]
    public void Stage_record_inside_a_full_route_attempt_goes_to_a_person()
    {
        var r = Check(Run(10), new ValidationOptions(IsRecordTime: (v, _) => v == RouteVariant.Stage1));

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.Equal([RouteVariant.Stage1], r.RecordVariants);
        Assert.True(Has(r, FindingCode.RecordCandidate));
    }

    [Fact]
    public void Stage_record_check_gets_the_stage_times()
    {
        var asked = new List<(RouteVariant, TimeSpan)>();
        var r = Check(Run(10), new ValidationOptions(IsRecordTime: (v, t) => { asked.Add((v, t)); return false; }));

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Assert.Contains((RouteVariant.Full, r.Elapsed!.Value), asked);
        Assert.Contains((RouteVariant.Stage1, r.Stages[0].Elapsed), asked);
        Assert.Contains((RouteVariant.Stage2, r.Stages[1].Elapsed), asked);
    }

    [Fact]
    public void Stage_record_from_an_unfinished_full_route_goes_to_a_person()
    {
        var r = Check(Walker().Walk(Line(0, 7_000), 10),
            new ValidationOptions(IsRecordTime: (v, _) => v == RouteVariant.Stage1));

        Assert.Equal(Verdict.ManualReview, r.Stage1Fallback!.Verdict);
        Assert.Contains(r.Stage1Fallback.Findings, f => f.Code == FindingCode.RecordCandidate);
    }
}
