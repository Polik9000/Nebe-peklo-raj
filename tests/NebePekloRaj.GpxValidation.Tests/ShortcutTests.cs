using Xunit;
using static NebePekloRaj.GpxValidation.Tests.StraightRoute;

namespace NebePekloRaj.GpxValidation.Tests;

/// <summary>Shortcuts on a route with a corner and two hairpins.</summary>
public class ShortcutTests
{
    private const double Kmh = 10;

    // East 3 km, hairpin 1, east 2 km, hairpin 2, east 2 km. Each hairpin is 500 m of route
    // but moves only 200 m north, so it can be cut inside the 150 m corridor.
    private static readonly GeoPoint[] Shape =
    [
        At(0), At(3_000), At(3_150), At(3_150, 100), At(3_000, 100), At(3_000, 200),
        At(5_000, 200), At(5_150, 200), At(5_150, 300), At(5_000, 300), At(5_000, 400),
        At(7_000, 400),
    ];

    private static readonly RouteModel Route = new(Densify(Shape));

    private static readonly ChallengeConfig Config = StraightRoute.Config with
    {
        Peklo = new Checkpoint("Peklo", At(1_500).Lat, At(1_500).Lon),
        Finish = new Checkpoint("Cíl", At(7_000, 400).Lat, At(7_000, 400).Lon),
    };

    private static AttemptValidator Validator() =>
        new(Config, Route, new FixedTime(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)));

    private static IEnumerable<GeoPoint> Densify(IEnumerable<GeoPoint> pts)
    {
        GeoPoint? prev = null;
        foreach (var p in pts)
        {
            if (prev is { } a)
            {
                int n = (int)Math.Ceiling(Geo.Distance(a, p) / 50);
                for (int i = 1; i < n; i++)
                    yield return new GeoPoint(a.Lat + (p.Lat - a.Lat) * i / n, a.Lon + (p.Lon - a.Lon) * i / n);
            }
            yield return p;
            prev = p;
        }
    }

    private static IEnumerable<GeoPoint> R(double from, double to) => Route.Between(from, to);

    [Fact]
    public void Following_the_hairpins_is_clean()
    {
        var b = Walker().Walk(R(0, Route.LengthM), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Full, new ValidationOptions(IsRecordCandidate: true));

        // A record always goes to a person, but nothing else is flagged.
        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.Contains(r.Findings, f => f.Code == FindingCode.RecordCandidate);
        Assert.Empty(Review.Problems(r));
        Assert.DoesNotContain(r.Findings, f => f.Code == FindingCode.Shortcut);
    }

    [Fact]
    public void One_cut_hairpin_is_only_noted()
    {
        var b = Walker().Walk(R(0, 3_000), Kmh).Walk(At(3_000, 200), Kmh).Walk(R(3_500, Route.LengthM), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Full, new ValidationOptions(IsRecordCandidate: true));

        Assert.Empty(Review.Problems(r));
        var f = Assert.Single(r.Findings, f => f.Code == FindingCode.Shortcut);
        Assert.Equal(Severity.Info, f.Severity);
        Assert.InRange(r.ShortcutSavedM, 150, 350);
    }

    private static TrackBuilder CutBoth() =>
        Walker().Walk(R(0, 3_000), Kmh).Walk(At(3_000, 200), Kmh)
            .Walk(R(3_500, 5_500), Kmh).Walk(At(5_000, 400), Kmh)
            .Walk(R(6_000, Route.LengthM), Kmh);

    [Fact]
    public void Repeated_cuts_need_manual_review_for_a_record()
    {
        var r = Validator().Validate([CutBoth().Build()], RouteVariant.Full, new ValidationOptions(IsRecordCandidate: true));

        Assert.Equal(Verdict.ManualReview, r.Verdict);
        Assert.Equal(2, r.Findings.Count(f => f.Code == FindingCode.Shortcut && f.Severity == Severity.ManualReview));
    }

    [Fact]
    public void Repeated_cuts_are_only_noted_when_not_a_record()
    {
        var r = Validator().Validate([CutBoth().Build()], RouteVariant.Full);

        Assert.Equal(Verdict.Accepted, r.Verdict);
        Assert.Equal(2, r.Findings.Count(f => f.Code == FindingCode.Shortcut));
        Assert.InRange(r.ShortcutSavedM, 300, 700);
    }

    [Fact]
    public void Cutting_a_corner_outside_the_corridor_is_rejected()
    {
        // Leaves at km 2.5, passes both hairpins well outside the corridor and rejoins at km 6.
        var b = Walker().Walk(R(0, 2_500), Kmh)
            .Walk([At(2_500, 550), At(5_000, 550), At(5_000, 400)], Kmh)
            .Walk(R(6_000, Route.LengthM), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Full);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.True(r.Findings.Any(f => f.Code == FindingCode.OffRouteShortcut), string.Join("\n", r.Findings.Select(f => f.Message)));
    }

    [Fact]
    public void One_big_cut_needs_manual_review_for_a_record()
    {
        var cfg = Config with { Limits = new ValidationLimits { RecordShortcutM = 150 } };
        var validator = new AttemptValidator(cfg, Route, new FixedTime(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        var b = Walker().Walk(R(0, 3_000), Kmh).Walk(At(3_000, 200), Kmh).Walk(R(3_500, Route.LengthM), Kmh);

        var record = validator.Validate([b.Build()], RouteVariant.Full, new ValidationOptions(IsRecordCandidate: true));
        var normal = validator.Validate([b.Build()], RouteVariant.Full);

        Assert.Contains(record.Findings, f => f.Code == FindingCode.Shortcut && f.Severity == Severity.ManualReview);
        Assert.Equal(Verdict.Accepted, normal.Verdict);
    }

    [Fact]
    public void Shortening_detour_with_a_note_goes_to_manual_review()
    {
        // Around hairpin 2 outside the corridor: leaves at route km 4.8, rejoins at km 6.0, walks about 1.1 km.
        // Coverage is relaxed so that only the detour decides.
        var cfg = Config with { Limits = new ValidationLimits { MinCoveragePercent = 50 } };
        var validator = new AttemptValidator(cfg, Route, new FixedTime(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        var b = Walker().Walk(R(0, 4_800), Kmh)
            .Walk([At(4_500, 570), At(5_000, 570), At(5_000, 400)], Kmh)
            .Walk(R(6_000, Route.LengthM), Kmh);

        var silent = validator.Validate([b.Build()], RouteVariant.Full);
        var noted = validator.Validate([b.Build()], RouteVariant.Full, new ValidationOptions(DetourExplained: true));

        Assert.Equal(Verdict.Rejected, silent.Verdict);
        Assert.True(silent.Findings.Any(f => f.Code == FindingCode.OffRouteShortcut), Review.Describe(silent));
        Assert.True(noted.Verdict == Verdict.ManualReview, Review.Describe(noted));
        Assert.Contains(noted.Findings, f => f.Code == FindingCode.OffRouteShortcut && f.Severity == Severity.ManualReview);
    }
}
