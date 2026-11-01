using System.Globalization;
using System.Xml;

namespace NebePekloRaj.GpxValidation;

/// <summary>Points read from one GPX file.</summary>
public sealed record GpxTrack(IReadOnlyList<GpxPoint> Points);

/// <summary>A raw GPX point. Time is missing in planned routes.</summary>
public readonly record struct GpxPoint(GeoPoint Position, DateTimeOffset? Time, double? Elevation);

/// <summary>
/// Reads GPX 1.0 and 1.1 files from watches, phone apps and route planners.
/// Takes track points (trkpt) and route points (rtept); vendor extensions are ignored.
/// </summary>
public static class GpxReader
{
    public static GpxTrack Read(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        var points = new List<GpxPoint>();
        using var reader = XmlReader.Create(stream, settings);
        try
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName is "trkpt" or "rtept")
                    points.Add(ReadPoint(reader));
            }
        }
        catch (XmlException ex)
        {
            throw new GpxFormatException("Soubor není platné GPX (XML): " + ex.Message, ex);
        }

        return new GpxTrack(points);
    }

    public static GpxTrack Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    private static GpxPoint ReadPoint(XmlReader reader)
    {
        double lat = ParseDouble(reader.GetAttribute("lat"));
        double lon = ParseDouble(reader.GetAttribute("lon"));
        DateTimeOffset? time = null;
        double? ele = null;

        if (!reader.IsEmptyElement)
        {
            int depth = reader.Depth;
            reader.Read();
            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Depth == depth + 1)
                {
                    // ReadElementContentAsString already moves to the next node.
                    if (reader.LocalName == "time")
                    {
                        time = ParseTime(reader.ReadElementContentAsString());
                        continue;
                    }
                    if (reader.LocalName == "ele")
                    {
                        var e = ParseDouble(reader.ReadElementContentAsString());
                        ele = double.IsFinite(e) ? e : null;
                        continue;
                    }
                }
                reader.Read();
            }
        }

        return new GpxPoint(new GeoPoint(lat, lon), time, ele);
    }

    private static double ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

    private static DateTimeOffset? ParseTime(string s) =>
        DateTimeOffset.TryParse(s.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : null;
}

public sealed class GpxFormatException(string message, Exception? inner = null) : Exception(message, inner);
