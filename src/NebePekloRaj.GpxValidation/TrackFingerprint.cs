using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NebePekloRaj.GpxValidation;

/// <summary>
/// A stable hash of a record: positions rounded to about 1 m and times to whole seconds.
/// It ignores file names, metadata, extensions and how the record is split into files,
/// so the same run uploaded twice gets the same fingerprint.
/// </summary>
public static class TrackFingerprint
{
    public static string Compute(IReadOnlyList<TrackPoint> sortedPoints)
    {
        var sb = new StringBuilder(sortedPoints.Count * 32);
        foreach (var p in sortedPoints)
        {
            sb.Append(p.Position.Lat.ToString("F5", CultureInfo.InvariantCulture)).Append(',')
              .Append(p.Position.Lon.ToString("F5", CultureInfo.InvariantCulture)).Append(',')
              .Append(p.Time.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}
