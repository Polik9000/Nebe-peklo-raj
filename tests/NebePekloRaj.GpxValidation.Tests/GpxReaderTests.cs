using System.Text;
using Xunit;

namespace NebePekloRaj.GpxValidation.Tests;

public class GpxReaderTests
{
    private static GpxTrack ReadString(string xml) => GpxReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void Reads_official_route_without_times()
    {
        var track = GpxReader.Read(Path.Combine(AppContext.BaseDirectory, "data", "trasa.gpx"));

        Assert.Equal(7325, track.Points.Count);
        Assert.All(track.Points, p => Assert.Null(p.Time));
        Assert.Equal(50.187937, track.Points[0].Position.Lat, 6);
        Assert.Equal(16.420252, track.Points[0].Position.Lon, 6);
        Assert.Equal(567.958435, track.Points[0].Elevation!.Value, 4);
    }

    [Fact]
    public void Reads_watch_export_with_extensions_and_any_child_order()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx creator="Garmin Connect" version="1.1" xmlns="http://www.topografix.com/GPX/1/1"
                 xmlns:ns3="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
              <metadata><time>2027-05-01T03:59:00Z</time></metadata>
              <trk><name>Ranní běh</name><trkseg>
                <trkpt lat="50.18885" lon="16.41996">
                  <time>2027-05-01T04:00:00Z</time>
                  <ele>560.2</ele>
                  <extensions><ns3:TrackPointExtension><ns3:hr>120</ns3:hr><ns3:time>junk</ns3:time></ns3:TrackPointExtension></extensions>
                </trkpt>
                <trkpt lat="50.18890" lon="16.42000"><ele>561</ele><time>2027-05-01T06:00:05+02:00</time></trkpt>
                <trkpt lat="50.18895" lon="16.42005"/>
              </trkseg></trk>
            </gpx>
            """;

        var track = ReadString(xml);

        Assert.Equal(3, track.Points.Count);
        Assert.Equal(new DateTimeOffset(2027, 5, 1, 4, 0, 0, TimeSpan.Zero), track.Points[0].Time);
        Assert.Equal(560.2, track.Points[0].Elevation);
        Assert.Equal(new DateTimeOffset(2027, 5, 1, 4, 0, 5, TimeSpan.Zero), track.Points[1].Time);
        Assert.Equal(TimeSpan.Zero, track.Points[1].Time!.Value.Offset);
        Assert.Null(track.Points[2].Time);
    }

    [Fact]
    public void Reads_gpx_1_0_and_route_points()
    {
        const string xml = """
            <gpx version="1.0" xmlns="http://www.topografix.com/GPX/1/0">
              <rte><rtept lat="50.1" lon="16.1"><time>2027-05-01T04:00:00Z</time></rtept></rte>
            </gpx>
            """;

        var track = ReadString(xml);

        Assert.Single(track.Points);
        Assert.NotNull(track.Points[0].Time);
    }

    [Fact]
    public void Rejects_broken_xml()
    {
        Assert.Throws<GpxFormatException>(() => ReadString("<gpx><trk><trkpt lat=\"1\" lon=\"2\"></gpx>"));
    }

    [Fact]
    public void Rejects_dtd_to_prevent_entity_attacks()
    {
        const string xml = """<?xml version="1.0"?><!DOCTYPE gpx [<!ENTITY x "y">]><gpx>&x;</gpx>""";
        Assert.Throws<GpxFormatException>(() => ReadString(xml));
    }

    [Fact]
    public void Round_trips_builder_output()
    {
        var built = StraightRoute.Walker().Walk(StraightRoute.Line(0, 500), 10).Build();
        var read = ReadString(TrackBuilder.ToGpxXml(built.Points));

        Assert.Equal(built.Points.Count, read.Points.Count);
        Assert.Equal(built.Points[^1].Position.Lat, read.Points[^1].Position.Lat, 6);
    }
}
