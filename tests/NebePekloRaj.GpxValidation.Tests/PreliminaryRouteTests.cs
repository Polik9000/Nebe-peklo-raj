using Xunit;

namespace NebePekloRaj.GpxValidation.Tests;

/// <summary>End-to-end checks on the preliminary route in data/trasa.gpx and data/challenge.json.</summary>
public class PreliminaryRouteTests
{
    private static readonly DateTimeOffset T0 = new(2027, 6, 1, 4, 0, 0, TimeSpan.Zero);
    private const double Kmh = 9;

    private static AttemptValidator Validator() =>
        new(RealRoute.Config, RealRoute.Route, new FixedTime(new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero)));

    private static IEnumerable<GeoPoint> Route(double from, double to) =>
        TrackBuilder.Along(RealRoute.Route, from, to);

    private static double PekloAlong => RealRoute.Route.ProjectNearest(RealRoute.Config.Peklo.Position)!.Value.AlongM;

    [Fact]
    public void Full_route_walked_to_finish_point_is_accepted()
    {
        var cfg = RealRoute.Config;
        var b = new TrackBuilder(cfg.Start.Position, T0)
            .Stand(TimeSpan.FromMinutes(2))
            .Walk(Route(0, RealRoute.Route.LengthM), Kmh)
            .Walk(cfg.Finish.Position, Kmh); // the last ~210 m beyond the end of the GPX

        var r = Validator().Validate([b.Build()], RouteVariant.Full);

        Assert.True(r.Verdict == Verdict.Accepted, string.Join("\n", r.Findings.Select(f => f.Message)));
        Assert.InRange(r.Elapsed!.Value.TotalHours, 17.5, 18.3); // ~162.6 km at 9 km/h
        Assert.InRange(r.Stages[0].Elapsed.TotalHours, 5.1, 5.5); // ~48 km
        Assert.InRange(r.RecordedDistanceM, 160_500, 163_000); // points every 25 m cut a few corners
        Assert.True(r.CoveragePercent > 99);
    }

    [Fact]
    public void Stopping_at_end_of_preliminary_gpx_misses_finish_zone()
    {
        // Documents the open question: the GPX ends ~210 m before the finish point, outside the 150 m zone.
        var b = new TrackBuilder(RealRoute.Config.Start.Position, T0).Walk(Route(0, RealRoute.Route.LengthM), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Full);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.Contains(r.Findings, f => f.Code == FindingCode.FinishNotReached);
    }

    [Fact]
    public void Stage1_on_real_route()
    {
        var b = new TrackBuilder(RealRoute.Config.Start.Position, T0).Walk(Route(0, PekloAlong), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Stage1);

        Assert.True(r.Verdict == Verdict.Accepted, string.Join("\n", r.Findings.Select(f => f.Message)));
        Assert.InRange(r.RouteLengthM, 47_700, 48_700);
    }

    [Fact]
    public void Stage2_with_train_ride_is_rejected()
    {
        var b = new TrackBuilder(RealRoute.Config.Peklo.Position, T0)
            .Walk(Route(PekloAlong, 80_000), Kmh)
            .Walk(Route(80_000, 100_000), 70)
            .Walk(Route(100_000, RealRoute.Route.LengthM), Kmh)
            .Walk(RealRoute.Config.Finish.Position, Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Stage2);

        Assert.Equal(Verdict.Rejected, r.Verdict);
        Assert.Contains(r.Findings, f => f.Code == FindingCode.VehicleSpeed);
    }

    [Fact]
    public void Full_route_with_only_stage1_recorded_is_rejected()
    {
        var b = new TrackBuilder(RealRoute.Config.Start.Position, T0).Walk(Route(0, PekloAlong + 2_000), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Full);

        Assert.Contains(r.Findings, f => f.Code == FindingCode.FinishNotReached);
        Assert.Equal(Verdict.Accepted, r.Stage1Fallback!.Verdict);
        Assert.InRange(r.Stage1Fallback.Elapsed!.Value.TotalHours, 5.1, 5.5);
    }

    [Fact]
    public void Real_config_judges_2027_attempts_by_current_rules()
    {
        var b = new TrackBuilder(RealRoute.Config.Start.Position, T0).Walk(Route(0, 5_000), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Full);

        Assert.Equal(T0, r.AttemptDateUtc);
        Assert.Equal("1.1", r.RulesVersion);
    }

    [Fact]
    public void Noisy_phone_gps_every_second_is_still_accepted()
    {
        var cfg = RealRoute.Config;
        var b = new TrackBuilder(cfg.Start.Position, T0, intervalS: 1)
            .Walk(Route(0, RealRoute.Route.LengthM), Kmh)
            .Walk(cfg.Finish.Position, Kmh);
        var rnd = new Random(42);
        for (int i = 0; i < b.Points.Count; i++)
            b.Points[i] = b.Points[i] with
            {
                Position = Geo.Offset(b.Points[i].Position, rnd.NextDouble() * 16 - 8, rnd.NextDouble() * 16 - 8),
            };

        var r = Validator().Validate([b.Build()], RouteVariant.Full, new ValidationOptions(IsRecordCandidate: true));

        Assert.True(r.Verdict == Verdict.Accepted, string.Join("\n", r.Findings.Select(f => f.Message)));
    }

    [Fact]
    public void Record_with_a_point_every_60_s_has_no_false_shortcuts()
    {
        var cfg = RealRoute.Config;
        var b = new TrackBuilder(cfg.Start.Position, T0, intervalS: 60)
            .Walk(Route(0, RealRoute.Route.LengthM), 12)
            .Walk(cfg.Finish.Position, 12);

        var r = Validator().Validate([b.Build()], RouteVariant.Full, new ValidationOptions(IsRecordCandidate: true));

        Assert.True(r.Verdict == Verdict.Accepted, string.Join("\n", r.Findings.Select(f => f.Message)));
    }

    [Fact]
    public void Real_config_rejects_attempt_before_launch()
    {
        var t = new DateTimeOffset(2027, 5, 8, 4, 0, 0, TimeSpan.Zero);
        var b = new TrackBuilder(RealRoute.Config.Start.Position, t).Walk(Route(0, 5_000), Kmh);

        var r = Validator().Validate([b.Build()], RouteVariant.Stage1);

        Assert.Contains(r.Findings, f => f.Code == FindingCode.BeforeLaunch);
    }
}
