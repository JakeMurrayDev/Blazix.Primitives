using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

/// <summary>Represents the state of a progress track, which the part passes to its class and style functions and projects as a status data attribute.</summary>
/// <param name="Status">The status derived from the value.</param>
[PartState]
public sealed partial record ProgressTrackState(ProgressStatus Status);

/// <summary>Provides the names of the data attributes a progress track renders.</summary>
public static class ProgressTrackDataAttributes
{
    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Complete"/>.</summary>
    public const string Complete = "data-complete";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Indeterminate"/>.</summary>
    public const string Indeterminate = "data-indeterminate";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Progressing"/>.</summary>
    public const string Progressing = "data-progressing";
}

// Base UI's Progress.Track (progress/track/ProgressTrack.tsx); the contract is docs/components/progress.md. PRG-STR-03:
// structural, only the status attribute (the mapping) and the consumer's; unlike Meter's it requires a root (PRG-STR-02),
// because it reads the status from it.
/// <summary>
/// Represents the track of a progress bar, which holds the indicator and stands for the whole range: Base UI's
/// <c>Progress.Track</c>, rendered as a <c>div</c>. It belongs inside a <see cref="ProgressRoot"/>.
/// </summary>
/// <remarks>The track adds nothing to its element but the status data attribute; it carries the attributes the consumer supplies.</remarks>
public sealed class ProgressTrack : PartBase<ProgressTrackState>
{
    // 1. constants
    private const string DefaultTag = "div";

    // 2. fields
    private static readonly StateAttributesMapping<ProgressTrackState> s_mapping = ProgressStatusMapping.Create<ProgressTrackState>();

    private static readonly ProgressTrackState[] s_states =
    [
        new(ProgressStatus.Indeterminate),
        new(ProgressStatus.Progressing),
        new(ProgressStatus.Complete),
    ];

    private readonly Action onStatusChanged;
    private ProgressRootContext? context;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="ProgressTrack"/> class.</summary>
    public ProgressTrack()
    {
        onStatusChanged = StateHasChanged;
    }

    // 3. private properties
    [CascadingParameter]
    private ProgressRootContext? RootContext { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ProgressTrackState State => s_states[(int)context!.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<ProgressTrackState>? StateAttributesMapping => s_mapping;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The track is not inside a <see cref="ProgressRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ProgressRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddStatusListener(onStatusChanged);
    }

    // 8. dispose
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveStatusListener(onStatusChanged);
        }

        base.Dispose(disposing);
    }
}
