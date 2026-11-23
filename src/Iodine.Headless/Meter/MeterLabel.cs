using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Meter.Label (meter/label/MeterLabel.tsx) and useRegisteredLabelId; the contract is docs/components/meter.md.
// MTR-API-12: the id is the consumer's or a generated one. MTR-DOM-07: role="presentation". MTR-STA-03 / FFI-ARIA-08: the
// label publishes its id to the root's slot and withdraws it on dispose or id change only while it is still the tracked id.
// MTR-ARIA-07 (docs §14): the label publishes while its parameters are set, inside the render batch that creates or updates
// it, so the root's aria-labelledby lands with the label itself on both render modes (the markup of a prerender has it too,
// where MTR-EDGE-06 describes a client-only publication; a port note in the document). MTR-STA-01: the state is {}.
/// <summary>
/// Represents the accessible name of a meter: Base UI's <c>Meter.Label</c>, rendered as a <c>span</c>. It belongs inside a
/// <see cref="MeterRoot"/>, which then references its id from <c>aria-labelledby</c>.
/// </summary>
/// <remarks>
/// The label takes the <c>id</c> the consumer supplies, or a generated one. The root's <c>aria-labelledby</c> follows the
/// most recently published id and is removed when the label is removed or disabled.
/// </remarks>
public sealed class MeterLabel : PartBase<EmptyState>
{
    // 1. constants
    private const string DefaultTag = "span";

    // 2. fields
    private MeterRootContext? context;
    private string? publishedId;

    // 3. private properties
    [CascadingParameter]
    private MeterRootContext? RootContext { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is not inside a <see cref="MeterRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = MeterRootContext.Require(RootContext);
        base.OnInitialized();
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
        if (disposing && publishedId is not null)
        {
            context!.Label.Withdraw(publishedId);
            publishedId = null;
        }

        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    // MTR-DOM-07: the id is added by the base from the generated id, and the consumer's id replaces it.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
    }
}
