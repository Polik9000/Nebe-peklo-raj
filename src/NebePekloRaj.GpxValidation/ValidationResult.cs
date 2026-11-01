namespace NebePekloRaj.GpxValidation;

public enum RouteVariant
{
    /// <summary>Nebe → Peklo → Ráj.</summary>
    Full,
    /// <summary>Nebe → Peklo.</summary>
    Stage1,
    /// <summary>Peklo → Ráj.</summary>
    Stage2,
}

public enum Verdict
{
    Accepted,
    ManualReview,
    Rejected,
}

public enum Severity
{
    /// <summary>Shown to the reviewer, does not affect the verdict.</summary>
    Info,
    /// <summary>A person has to look at it.</summary>
    ManualReview,
    /// <summary>The attempt does not count.</summary>
    Reject,
}

public enum FindingCode
{
    InvalidGpx,
    NoPoints,
    MissingTimestamps,
    InvalidPointsDropped,
    TimeInFuture,
    StartNotReached,
    PekloNotReached,
    FinishNotReached,
    WrongDirection,
    GpsGlitch,
    ShortDetour,
    LongDetour,
    Pause,
    RecordingGap,
    LowPointDensity,
    UncoveredStretch,
    LowCoverage,
    SuspiciousSpeed,
    VehicleSpeed,
    DuplicateRecord,
    SimilarAttempt,
}

/// <summary>One thing the validator noticed. Messages are in Czech for the reviewer.</summary>
public sealed record Finding(
    FindingCode Code,
    Severity Severity,
    string Message,
    DateTimeOffset? At = null,
    double? RouteKm = null);

/// <summary>An attempt already in the system, for duplicate checks.</summary>
public sealed record KnownAttempt(string Id, string Fingerprint, DateTimeOffset StartUtc, DateTimeOffset FinishUtc);

public sealed record StageTime(string Name, DateTimeOffset StartUtc, DateTimeOffset FinishUtc)
{
    public TimeSpan Elapsed => FinishUtc - StartUtc;
}

public sealed record ValidationResult
{
    public required Verdict Verdict { get; init; }
    public required RouteVariant Variant { get; init; }
    public required string Fingerprint { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }

    public DateTimeOffset? StartUtc { get; init; }
    public DateTimeOffset? FinishUtc { get; init; }
    public TimeSpan? Elapsed => StartUtc is { } s && FinishUtc is { } f ? f - s : null;

    /// <summary>Stage splits. For the full route: stage 1 and stage 2.</summary>
    public IReadOnlyList<StageTime> Stages { get; init; } = [];

    /// <summary>Length of the official route part for this variant.</summary>
    public double RouteLengthM { get; init; }
    /// <summary>Distance actually recorded between start and finish.</summary>
    public double RecordedDistanceM { get; init; }
    /// <summary>Share of the official route covered by the record, 0–100.</summary>
    public double CoveragePercent { get; init; }
    public int PointCount { get; init; }
}
