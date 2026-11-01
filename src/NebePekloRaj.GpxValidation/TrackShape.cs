using System.Globalization;
using System.Text;

namespace NebePekloRaj.GpxValidation;

/// <summary>Where the record was and how long it took, when it first got past a route bin.</summary>
public readonly record struct ShapeSample(int ElapsedS, double Lat, double Lon)
{
    public GeoPoint Position => new(Lat, Lon);
}

/// <summary>
/// The shape of a record: for every <see cref="BinM"/> metres of route from the start, the time since
/// leaving the start zone and the position. A copied record keeps its shape even when its times are
/// shifted or its points moved by a metre, so a hash would not catch it but this does.
/// </summary>
/// <param name="FromM">Route distance of the first bin.</param>
/// <param name="Samples">One entry per bin, null where the record has no point.</param>
public sealed record TrackShape(double FromM, double BinM, IReadOnlyList<ShapeSample?> Samples)
{
    /// <summary>A compact text form for the database.</summary>
    public string Encode()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{FromM:0}|{BinM:0}");
        foreach (var s in Samples)
        {
            sb.Append('|');
            if (s is { } v)
                sb.Append(CultureInfo.InvariantCulture, $"{v.ElapsedS},{v.Lat:F6},{v.Lon:F6}");
        }
        return sb.ToString();
    }

    public static TrackShape Decode(string text)
    {
        var parts = text.Split('|');
        if (parts.Length < 2) throw new FormatException("Neplatný tvar stopy.");
        double from = double.Parse(parts[0], CultureInfo.InvariantCulture);
        double bin = double.Parse(parts[1], CultureInfo.InvariantCulture);
        var samples = new List<ShapeSample?>(parts.Length - 2);
        foreach (var p in parts.Skip(2))
        {
            if (p.Length == 0) { samples.Add(null); continue; }
            var f = p.Split(',');
            samples.Add(new ShapeSample(
                int.Parse(f[0], CultureInfo.InvariantCulture),
                double.Parse(f[1], CultureInfo.InvariantCulture),
                double.Parse(f[2], CultureInfo.InvariantCulture)));
        }
        return new TrackShape(from, bin, samples);
    }

    /// <summary>
    /// Share (0–100) of the bins both records have where splits and positions agree within the tolerances,
    /// or null when the records share too few bins to say. Splits are compared after removing the usual
    /// difference, so a record cut to start a few minutes later still matches.
    /// </summary>
    public double? MatchPercent(TrackShape other, double timeToleranceS, double distanceToleranceM)
    {
        if (BinM != other.BinM) return null;
        int offset = (int)Math.Round((other.FromM - FromM) / BinM);
        var pairs = new List<(ShapeSample A, ShapeSample B)>();
        for (int i = 0; i < Samples.Count; i++)
        {
            int j = i - offset;
            if (j < 0 || j >= other.Samples.Count) continue;
            if (Samples[i] is { } a && other.Samples[j] is { } b) pairs.Add((a, b));
        }
        if (pairs.Count < 10) return null;

        var shifts = pairs.Select(p => p.A.ElapsedS - p.B.ElapsedS).Order().ToList();
        int shift = shifts[shifts.Count / 2];
        int same = pairs.Count(p =>
            Math.Abs(p.A.ElapsedS - p.B.ElapsedS - shift) <= timeToleranceS &&
            Geo.Distance(p.A.Position, p.B.Position) <= distanceToleranceM);
        return same * 100.0 / pairs.Count;
    }
}
