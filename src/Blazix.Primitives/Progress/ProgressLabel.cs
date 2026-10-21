using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

/// <summary>Represents the state of a progress label, which the part passes to its class and style functions and projects as a status data attribute.</summary>
/// <param name="Status">The status derived from the value.</param>
[PartState]
public sealed partial record ProgressLabelState(ProgressStatus Status);

/// <summary>Provides the names of the data attributes a progress label renders.</summary>
public static class ProgressLabelDataAttributes
{
    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Complete"/>.</summary>
    public const string Complete = "data-complete";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Indeterminate"/>.</summary>
    public const string Indeterminate = "data-indeterminate";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Progressing"/>.</summary>
    public const string Progressing = "data-progressing";
}

// Base UI's Progress.Label (progress/label/ProgressLabel.tsx) and useRegisteredLabelId; the contract is
// docs/components/progress.md. PRG-API-12: the id is the consumer's or a generated one. PRG-DOM-08: role="presentation".
// PRG-STA-05 / FFI-ARIA-08: the label publishes its id to the root's slot and withdraws it on dispose or id change only
// while it is still the tracked id. PRG-ARIA-07 (docs §14): the label publishes while its parameters are set, inside the
// render batch that creates or updates it, so the root's aria-labelledby lands with the label itself on both render modes (the
// markup of a prerender has it too, where PRG-EDGE-06 describes a client-only publication; a port note in the document).
/// <summary>
/// Represents the accessible name of a progress bar: Base UI's <c>Progress.Label</c>, rendered as a <c>span</c>. It belongs
/// inside a <see cref="ProgressRoot"/>, which then references its id from <c>aria-labelledby</c>.
/// </summary>
/// <remarks>
/// The label takes the <c>id</c> the consumer supplies, or a generated one. The root's <c>aria-labelledby</c> follows the
/// most recently published id and is removed when the label is removed or disabled.
/// </remarks>
public sealed class ProgressLabel : PartBase<ProgressLabelState>
{
    // 1. constants
    private const string DefaultTag = "span";

    // 2. fields
    private static readonly StateAttributesMapping<ProgressLabelState> s_mapping = ProgressStatusMapping.Create<ProgressLabelState>();

    private static readonly ProgressLabelState[] s_states =
    [
        new(ProgressStatus.Indeterminate),
        new(ProgressStatus.Progressing),
        new(ProgressStatus.Complete),
    ];

    private readonly Action onStatusChanged;
    private ProgressRootContext? context;
    private string? publishedId;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="ProgressLabel"/> class.</summary>
    public ProgressLabel()
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
    protected override ProgressLabelState State => s_states[(int)context!.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<ProgressLabelState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is not inside a <see cref="ProgressRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ProgressRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddStatusListener(onStatusChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A disabled label renders no element, so it publishes no id.
        string? id = Enabled ? Id : null;
        if (string.Equals(id, publishedId, StringComparison.Ordinal))
        {
            return;
        }

        // A new id replaces the old one in the slot; withdrawing happens only when the label has no id left to publish.
        if (id is not null)
        {
            context!.Label.Publish(id);
        }
        else if (publishedId is not null)
        {
            context!.Label.Withdraw(publishedId);
        }

        publishedId = id;
    }

    // 8. dispose
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveStatusListener(onStatusChanged);
            if (publishedId is not null)
            {
                context!.Label.Withdraw(publishedId);
                publishedId = null;
            }
        }

        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    // PRG-DOM-08: the id is added by the base from the generated id, and the consumer's id replaces it.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
    }
}
