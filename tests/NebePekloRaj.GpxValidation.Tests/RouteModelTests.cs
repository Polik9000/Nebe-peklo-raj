using Xunit;

namespace NebePekloRaj.GpxValidation.Tests;

public class RouteModelTests
{
    [Fact]
    public void Preliminary_route_matches_published_numbers()
    {
        var route = RealRoute.Route;
        var cfg = RealRoute.Config;

        Assert.InRange(route.LengthM, 162_000, 163_200);

        var start = route.ProjectNearest(cfg.Start.Position)!.Value;
        Assert.True(start.DistanceFromRouteM < 50, $"start {start.DistanceFromRouteM} m");

        var peklo = route.ProjectNearest(cfg.Peklo.Position)!.Value;
        Assert.True(peklo.DistanceFromRouteM < 50, $"peklo {peklo.DistanceFromRouteM} m");
        Assert.InRange(peklo.AlongM, 47_700, 48_700);

        // Known issue: the preliminary GPX ends about 185 m before the finish point.
        var finish = route.ProjectNearest(cfg.Finish.Position)!.Value;
        Assert.InRange(finish.DistanceFromRouteM, 180, 240);
        Assert.Equal(route.LengthM, finish.AlongM, 0);
    }

    [Fact]
    public void Projection_on_straight_route()
    {
        var p = StraightRoute.Route.ProjectNearest(StraightRoute.At(2_500, 120))!.Value;

        Assert.Equal(120, p.DistanceFromRouteM, 0);
        Approx.Near(2_500, p.AlongM, 10);
    }

    [Fact]
    public void Out_and_back_resolves_by_progress()
    {
        // 2 km east, then back west 50 m further north.
        var outLeg = Enumerable.Range(0, 21).Select(i => StraightRoute.At(i * 100));
        var backLeg = Enumerable.Range(0, 21).Select(i => StraightRoute.At(2_000 - i * 100, 50));
        var route = new RouteModel(outLeg.Concat(backLeg));
        var p = StraightRoute.At(1_000, 25);

        var early = route.ProjectNear(p, progressM: 900, corridorM: 150)!.Value;
        var late = route.ProjectNear(p, progressM: 3_000, corridorM: 150)!.Value;

        Approx.Near(1_000, early.AlongM, 10);
        Approx.Near(3_050, late.AlongM, 10);
    }

    [Fact]
    public void Outside_corridor_has_no_projection()
    {
        Assert.Null(StraightRoute.Route.ProjectNear(StraightRoute.At(3_000, 400), 3_000, 150));
    }
}
