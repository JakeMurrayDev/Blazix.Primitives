using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// FST-STA-02; fieldset/legend/FieldsetLegend.tsx:37-42. ADR 08 §3: sealed, partial, [PartState].
/// <summary>Represents the state of a fieldset legend, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the enclosing fieldset is disabled; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record FieldsetLegendState(bool Disabled);

// FST-DOM-06; fieldset/legend/FieldsetLegendDataAttributes.ts.
/// <summary>Provides the names of the data attributes a fieldset legend renders.</summary>
public static class FieldsetLegendDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the fieldset is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Fieldset.Legend (fieldset/legend/FieldsetLegend.tsx); the contract is docs/components/fieldset.md.
// FST-API-04, FST-EDGE-04: the id is the consumer's verbatim (even empty) or a generated one. FST-DOM-07, FST-EDGE-03,
// FFI-ARIA-08: it publishes the id to the root's slot while its parameters are set, inside the render batch that creates
// or updates it (ADR 01 Amendments 2026-10-22), and withdraws it only while it is still the tracked id. FST-ARIA-02: a
// div, no role or aria attribute. FST-STR-02, FST-EDGE-01: outside a FieldsetRoot it throws (`Blazix: ` wording).
/// <summary>
/// Represents the caption of a fieldset: Base UI's <c>Fieldset.Legend</c>, rendered as a <c>div</c>. It belongs inside a
/// <see cref="FieldsetRoot"/>, which references its id from <c>aria-labelledby</c>.
/// </summary>
/// <remarks>
/// The legend takes the <c>id</c> the consumer supplies, or a generated one. The root's <c>aria-labelledby</c> follows the
/// most recently published id and is removed when the legend is removed or disabled.
/// </remarks>
public sealed class FieldsetLegend : PartBase<FieldsetLegendState>
{
    private const string DefaultTag = "div";

    private static readonly FieldsetLegendState s_enabled = new(Disabled: false);
    private static readonly FieldsetLegendState s_disabled = new(Disabled: true);

    private readonly Action contextChanged;
    private FieldsetRootContext? context;
    private string? publishedId;
    private bool renderedDisabled;

    /// <summary>Initializes a new instance of the <see cref="FieldsetLegend"/> class.</summary>
    public FieldsetLegend()
    {
        contextChanged = OnContextChanged;
    }

    [CascadingParameter]
    private FieldsetRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldsetLegendState State => context!.Disabled ? s_disabled : s_enabled;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The legend is not inside a <see cref="FieldsetRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = FieldsetRootContext.Require(RootContext);
        context.AddListener(contextChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        renderedDisabled = context!.Disabled;

        // A disabled legend renders no element, so it publishes no id.
        string? id = Enabled ? Id : null;
        if (string.Equals(id, publishedId, StringComparison.Ordinal))
        {
            return;
        }

        if (id is not null)
        {
            context.Legend.Publish(id);
        }
        else if (publishedId is not null)
        {
            context.Legend.Withdraw(publishedId);
        }

        publishedId = id;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && context is not null)
        {
            context.RemoveListener(contextChanged);
            if (publishedId is not null)
            {
                context.Legend.Withdraw(publishedId);
                publishedId = null;
            }
        }

        base.Dispose(disposing);
    }

    private void OnContextChanged()
    {
        if (renderedDisabled == context!.Disabled)
        {
            return;
        }

        renderedDisabled = context.Disabled;
        StateHasChanged();
    }
}
