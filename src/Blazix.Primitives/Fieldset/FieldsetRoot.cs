using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// FST-STA-01; fieldset/root/FieldsetRoot.tsx:30-32,60-65. ADR 08 §3: sealed, partial, [PartState]. Base UI exports no
// FieldsetRootDataAttributes (fieldset §3.3), so the port declares none.
/// <summary>Represents the state of a fieldset root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the fieldset is disabled, by its own parameter or an enclosing fieldset; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record FieldsetRootState(bool Disabled);

// Base UI's Fieldset.Root (fieldset/root/FieldsetRoot.tsx); the contract is docs/components/fieldset.md.
// FST-API-01/03, FST-DOM-01..04: the effective disabled state is the native `disabled` attribute, and the legend's id is
// `aria-labelledby`, which a consumer value replaces (the pipeline merges the consumer last). FST-STR-01, FST-DOM-09: the
// fieldset scope is cascaded around the element itself (the M2 contexts design point, pending the user), so a substituted
// root component receives `disabled` as a parameter and sees the scope (a RadioGroup reads the legend id, FST-FRM-03).
/// <summary>
/// Represents a group of related form controls under one caption: Base UI's <c>Fieldset.Root</c>, rendered as a
/// <c>fieldset</c>. Disabling it disables every native control inside it and every <see cref="FieldRoot"/> nested in it.
/// </summary>
/// <remarks>
/// The root takes its accessible name from a <see cref="FieldsetLegend"/> through <c>aria-labelledby</c>, unless the
/// consumer supplies <c>aria-labelledby</c> itself. A nested fieldset is disabled whenever an enclosing one is.
/// </remarks>
public sealed class FieldsetRoot : PartBase<FieldsetRootState>
{
    private const string DefaultTag = "fieldset";

    private static readonly FieldsetRootState s_enabled = new(Disabled: false);
    private static readonly FieldsetRootState s_disabled = new(Disabled: true);

    private readonly RenderFragment renderPart;
    private readonly Action contextChanged;
    private FieldsetRootContext? context;
    private bool renderedDisabled;
    private string? renderedLegendId;

    /// <summary>Initializes a new instance of the <see cref="FieldsetRoot"/> class.</summary>
    public FieldsetRoot()
    {
        renderPart = RenderPart;
        contextChanged = OnContextChanged;
    }

    [CascadingParameter]
    private FieldsetRootContext? ParentContext { get; set; }

    // FST-API-01
    /// <summary>Gets or sets a value indicating whether the fieldset is disabled.</summary>
    /// <value>
    /// <see langword="true"/> to disable the fieldset: the root renders the native <c>disabled</c> attribute and every
    /// nested fieldset, field and control is disabled; otherwise, <see langword="false"/>, the default. An enclosing
    /// disabled fieldset disables this one whatever this value is.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldsetRootState State => context!.Disabled ? s_disabled : s_enabled;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        context = new FieldsetRootContext(ParentContext);
        context.AddListener(contextChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        context!.SetDisabled(Disabled);
        renderedDisabled = context.Disabled;
        renderedLegendId = context.Legend.Id;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<FieldsetRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<FieldsetRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<FieldsetRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<FieldsetRootContext>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && context is not null)
        {
            context.RemoveListener(contextChanged);
            context.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (context!.Legend.Id is { } legendId)
        {
            attributes.Set("aria-labelledby", legendId);
        }

        if (context.Disabled)
        {
            attributes.Set("disabled", true);
        }
    }

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    // The root renders again only when its disabled state or the legend id changed (FST-DOM-01/03, FST-STA-03), and not for
    // the legend id while the consumer supplies its own aria-labelledby, which replaces it (FST-DOM-04): nothing would change.
    private void OnContextChanged()
    {
        bool legendShown = AdditionalAttributes is null || !AdditionalAttributes.ContainsKey("aria-labelledby");
        if (renderedDisabled == context!.Disabled && (!legendShown || string.Equals(renderedLegendId, context.Legend.Id, StringComparison.Ordinal)))
        {
            return;
        }

        renderedDisabled = context.Disabled;
        renderedLegendId = context.Legend.Id;
        StateHasChanged();
    }
}
