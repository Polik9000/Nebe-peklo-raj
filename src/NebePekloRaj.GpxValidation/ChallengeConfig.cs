using System.Text.Json;

namespace NebePekloRaj.GpxValidation;

/// <summary>A checkpoint zone: a point and a radius.</summary>
public sealed record Checkpoint(string Name, double Lat, double Lon, double RadiusM = 150)
{
    public GeoPoint Position => new(Lat, Lon);
    public bool Contains(GeoPoint p) => Geo.Distance(Position, p) <= RadiusM;
}

/// <summary>Tunable thresholds. Defaults follow the rules v1 draft.</summary>
public sealed record ValidationLimits
{
    public double CorridorM { get; init; } = 150;
    public double MaxPointIntervalS { get; init; } = 60;
    /// <summary>A gap is fine when the record resumes this close to where it stopped.</summary>
    public double ResumeMaxDistanceM { get; init; } = 100;
    /// <summary>Longer gaps (with resume in place) go to manual review.</summary>
    public double MaxGapHours { get; init; } = 12;
    /// <summary>Coming back within this distance along the route counts as "the same place".</summary>
    public double DetourReturnToleranceM { get; init; } = 200;
    /// <summary>A place where the record saves at least this much route distance is a shortcut.</summary>
    public double ShortcutMinSavedM { get; init; } = 150;
    public double ShortcutWindowS { get; init; } = 120;
    /// <summary>Progress up to this multiple of the usual pace is not a shortcut.</summary>
    public double ShortcutPaceTolerance { get; init; } = 1.3;
    /// <summary>From this many shortcuts on, a record attempt goes to manual review.</summary>
    public int RepeatedShortcutCount { get; init; } = 2;
    public double MinCoveragePercent { get; init; } = 90;
    public double MaxUncoveredStretchM { get; init; } = 500;
    public double SuspiciousSpeedKmh { get; init; } = 18;
    public double SuspiciousWindowM { get; init; } = 3000;
    public double VehicleSpeedKmh { get; init; } = 30;
    public double VehicleWindowM { get; init; } = 500;
    public double SimilarAttemptToleranceS { get; init; } = 120;
    public double FutureToleranceMin { get; init; } = 60;
}

/// <summary>A rules version and the first day (Czech time) it applies to.</summary>
public sealed record RulesVersion(string Version, DateOnly From);

/// <summary>Everything the validator needs to know about the challenge.</summary>
/// <param name="AcceptFrom">First day attempts count (launch). Earlier attempts are rejected.</param>
/// <param name="Rules">Rules versions; each attempt is judged by the one in force on its start day.</param>
public sealed record ChallengeConfig(
    Checkpoint Start,
    Checkpoint Peklo,
    Checkpoint Finish,
    ValidationLimits Limits,
    string? RouteGpx = null,
    DateOnly? AcceptFrom = null,
    IReadOnlyList<RulesVersion>? Rules = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ChallengeConfig FromJson(string json)
    {
        var cfg = JsonSerializer.Deserialize<ChallengeConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException("Prázdná konfigurace.");
        return cfg with { Limits = cfg.Limits ?? new ValidationLimits() };
    }

    public static ChallengeConfig Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>
    /// Loads challenge.json and the official route GPX it points to (path relative to the JSON file).
    /// </summary>
    public static (ChallengeConfig Config, RouteModel Route) LoadWithRoute(string configPath)
    {
        var cfg = Load(configPath);
        if (string.IsNullOrWhiteSpace(cfg.RouteGpx))
            throw new InvalidDataException("V konfiguraci chybí routeGpx.");
        var dir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var route = RouteModel.FromGpx(GpxReader.Read(Path.Combine(dir, cfg.RouteGpx)));
        return (cfg, route);
    }
}
