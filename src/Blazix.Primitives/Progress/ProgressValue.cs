using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

/// <summary>Represents the state of a progress value, which the part passes to its class and style functions and projects as a status data attribute.</summary>
/// <param name="Status">The status derived from the value.</param>
[PartState]
public sealed partial record ProgressValueState(ProgressStatus Status);

/// <summary>Provides the names of the data attributes a progress value renders.</summary>
public static class ProgressValueDataAttributes
{
    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Complete"/>.</summary>
    public const string Complete = "data-complete";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Indeterminate"/>.</summary>
    public const string Indeterminate = "data-indeterminate";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Progressing"/>.</summary>
    public const string Progressing = "data-progressing";
}

// Base UI's Progress.Value (progress/value/ProgressValue.tsx); the contract is docs/components/progress.md. PRG-API-10:
// Base UI's `children` function is the template ValueContent; its first argument is the literal 'indeterminate' while
// indeterminate. PRG-API-11 / PRG-DOM-06: a plain ChildContent is discarded (this part replaces it with the fragment below);
// the text is the formatted value while determinate and nothing while indeterminate. PRG-DOM-07: aria-hidden="true" in every
// status unless the consumer overrides it. The text changes when the status, the raw value or the formatted value does, which
// reaches this part through the context's listener (the cascade is fixed); PRG-EVT-02: the template runs on every render.
/// <summary>
/// Represents the text that shows the progress value: Base UI's <c>Progress.Value</c>, rendered as a <c>span</c>. It belongs
/// inside a <see cref="ProgressRoot"/>.
/// </summary>
/// <remarks>
/// The part shows the root's formatted value, and nothing while the progress is indeterminate. It carries
/// <c>aria-hidden="true"</c>, because assistive technology reads the root's <c>aria-valuetext</c> instead. Use
/// <see cref="ValueContent"/> to render something else; plain child content is ignored.
/// </remarks>
public sealed class ProgressValue : PartBase<ProgressValueState>
{
    // 1. constants
    private const string DefaultTag = "span";

    // 2. fields
    private static readonly StateAttributesMapping<ProgressValueState> s_mapping = ProgressStatusMapping.Create<ProgressValueState>();

    private static readonly ProgressValueState[] s_states =
    [
        new(ProgressStatus.Indeterminate),
        new(ProgressStatus.Progressing),
        new(ProgressStatus.Complete),
    ];

    private readonly Action onValueChanged;
    private readonly RenderFragment content;
    private ProgressRootContext? context;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="ProgressValue"/> class.</summary>
    public ProgressValue()
    {
        onValueChanged = StateHasChanged;
        content = RenderContent;
    }

    // 3. private properties
    [CascadingParameter]
    private ProgressRootContext? RootContext { get; set; }

    // 4. parameter properties
    // PRG-API-10, PRG-EVT-02; ADR 08 Amendments 2026-10-20: a template parameter is named for what it renders and used with Context.
    /// <summary>Gets or sets the template that renders the content of the element.</summary>
    /// <value>
    /// A template that receives the formatted value and the raw value and renders the element's content, such as
    /// <c>&lt;ValueContent Context="value"&gt;@value.FormattedValue&lt;/ValueContent&gt;</c>; or <see langword="null"/> to
    /// render the formatted value. While the progress is indeterminate the formatted value is the text
    /// <c>indeterminate</c>. The template runs on every render of the part, in every status.
    /// </value>
    [Parameter]
    public RenderFragment<ProgressValueArguments>? ValueContent { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ProgressValueState State => s_states[(int)context!.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<ProgressValueState>? StateAttributesMapping => s_mapping;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The value is not inside a <see cref="ProgressRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ProgressRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddValueListener(onValueChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // PRG-API-11: the consumer's ChildContent is discarded.
        ChildContent = content;
    }

    // 8. dispose
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveValueListener(onValueChanged);
        }

        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    // PRG-DOM-07: the value is announced through the root's aria-valuetext instead.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("aria-hidden", "true");
    }

    // 11. private methods
    private void RenderContent(RenderTreeBuilder builder)
    {
        ProgressRootContext root = context!;
        bool indeterminate = root.Status == ProgressStatus.Indeterminate;
        if (ValueContent is { } template)
        {
            string formatted = indeterminate ? ProgressRootContext.IndeterminateArgument : root.FormattedValue;
            builder.AddContent(0, template(new ProgressValueArguments(formatted, root.Value)));
        }
        else if (!indeterminate)
        {
            builder.AddContent(1, root.FormattedValue);
        }
    }
}
