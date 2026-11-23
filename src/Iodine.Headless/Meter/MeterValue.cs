using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Meter.Value (meter/value/MeterValue.tsx); the contract is docs/components/meter.md. MTR-API-10: Base UI's
// `children` function is the template ValueContent; MTR-API-11: a plain ChildContent is discarded (this part replaces it
// with the fragment below, as Base UI discards a non-function children). MTR-DOM-06: aria-hidden="true" unless the consumer
// overrides it. MTR-STA-01: the state is {}. The text changes when the root's raw or formatted value does, which reaches
// this part through the context's listener (the cascade is fixed); MTR-EVT-02: the template runs on every render.
/// <summary>
/// Represents the text that shows the meter's value: Base UI's <c>Meter.Value</c>, rendered as a <c>span</c>. It belongs
/// inside a <see cref="MeterRoot"/>.
/// </summary>
/// <remarks>
/// The part shows the root's formatted value and carries <c>aria-hidden="true"</c>, because assistive technology reads the
/// root's <c>aria-valuetext</c> instead. Use <see cref="ValueContent"/> to render something else; plain child content is
/// ignored.
/// </remarks>
public sealed class MeterValue : PartBase<EmptyState>
{
    // 1. constants
    private const string DefaultTag = "span";

    // 2. fields
    private readonly Action onValueChanged;
    private readonly RenderFragment content;
    private MeterRootContext? context;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="MeterValue"/> class.</summary>
    public MeterValue()
    {
        onValueChanged = StateHasChanged;
        content = RenderContent;
    }

    // 3. private properties
    [CascadingParameter]
    private MeterRootContext? RootContext { get; set; }

    // 4. parameter properties
    // MTR-API-10, MTR-EVT-02; ADR 08 Amendments 2026-10-20: a template parameter is named for what it renders and used with Context.
    /// <summary>Gets or sets the template that renders the content of the element.</summary>
    /// <value>
    /// A template that receives the formatted value and the raw value and renders the element's content, such as
    /// <c>&lt;ValueContent Context="value"&gt;@value.FormattedValue&lt;/ValueContent&gt;</c>; or <see langword="null"/> to
    /// render the formatted value. The template runs on every render of the part.
    /// </value>
    [Parameter]
    public RenderFragment<MeterValueArguments>? ValueContent { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The value is not inside a <see cref="MeterRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = MeterRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddValueListener(onValueChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // MTR-API-11: the consumer's ChildContent is discarded.
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
    // MTR-DOM-06: the value is announced through the root's aria-valuetext instead.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("aria-hidden", "true");
    }

    // 11. private methods
    private void RenderContent(RenderTreeBuilder builder)
    {
        MeterRootContext root = context!;
        if (ValueContent is { } template)
        {
            builder.AddContent(0, template(new MeterValueArguments(root.FormattedValue, root.Value)));
        }
        else
        {
            builder.AddContent(1, root.FormattedValue);
        }
    }
}
