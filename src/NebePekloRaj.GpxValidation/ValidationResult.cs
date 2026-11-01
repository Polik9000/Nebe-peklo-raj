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
    BeforeLaunch,
    MultipleFiles,
    GpsGlitch,
    Detour,
    OffRouteShortcut,
    Shortcut,
    Pause,
    RecordingGap,
    LongGap,
    LowPointDensity,
    UncoveredStretch,
    LowCoverage,
    SuspiciousSpeed,
    VehicleSpeed,
    DuplicateRecord,
    SimilarAttempt,
    /// <summary>Stretch far faster than the runner's own pace (a bike at 15 km/h).</summary>
    FastForRunner,
    /// <summary>Same shape and splits as another record, even if the file differs.</summary>
    CopiedTrack,
    /// <summary>Two files recorded at the same time in different places.</summary>
    FilesDisagree,
    /// <summary>The time would be a record (also a stage record from a full-route attempt).</summary>
    RecordCandidate,
    /// <summary>A longer gap in a record attempt: the reviewer asks how the night was spent.</summary>
    RecordPause,
}

/// <summary>One thing the validator noticed. Messages are in Czech for the reviewer.</summary>
public sealed record Finding(
    FindingCode Code,
    Severity Severity,
    string Message,
    DateTimeOffset? At = null,
    double? RouteKm = null);

/// <summary>Extra input from the caller.</summary>
/// <param name="KnownAttempts">Attempts already in the system, for duplicate checks.</param>
/// <param name="IsRecordCandidate">The time would be 1st in its variant and style. Records get stricter checks.</param>
/// <param name="IsRecordTime">
/// Asked with each measured time (the variant and, for the full route, both stages):
/// would it be 1st in the leaderboard for the runner's style and category?
/// Lets the validator spot a stage record inside a full-route attempt.
/// </param>
/// <param name="DetourExplained">
/// The runner sent a note about a closure or another reason for leaving the route.
/// A detour that shortens the route then goes to manual review instead of being rejected.
/// </param>
public sealed record ValidationOptions(
    IEnumerable<KnownAttempt>? KnownAttempts = null,
    bool IsRecordCandidate = false,
    Func<RouteVariant, TimeSpan, bool>? IsRecordTime = null,
    bool DetourExplained = false);

/// <summary>An attempt already in the system, for duplicate checks.</summary>
/// <param name="Shape">The stored <see cref="ValidationResult.Shape"/>, for spotting copies of a record.</param>
public sealed record KnownAttempt(string Id, string Fingerprint, DateTimeOffset StartUtc, DateTimeOffset FinishUtc,
    TrackShape? Shape = null);

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

    /// <summary>Time of leaving the start zone (first GPX point when the start was not reached). Decides which rules apply.</summary>
    public DateTimeOffset? AttemptDateUtc { get; init; }
    /// <summary>The rules version the attempt was judged by.</summary>
    public string? RulesVersion { get; init; }

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
    /// <summary>Splits and positions every few hundred metres of the route. Store it to spot copies later.</summary>
    public TrackShape? Shape { get; init; }
    /// <summary>Variants whose time from this attempt would be a record (needs a person to approve).</summary>
    public IReadOnlyList<RouteVariant> RecordVariants { get; init; } = [];
    /// <summary>Route metres skipped by shortcuts inside the corridor.</summary>
    public double ShortcutSavedM { get; init; }

    /// <summary>
    /// For a full-route attempt that was not finished or not accepted: the Stage 1 result,
    /// when Stage 1 on its own passes. Stage 1 then goes to the Stage 1 leaderboard.
    /// </summary>
    public ValidationResult? Stage1Fallback { get; init; }
}
