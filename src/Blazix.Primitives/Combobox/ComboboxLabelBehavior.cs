using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// combobox/label/ComboboxLabel.tsx (CMB §3.2, STR-10, FFI-PTR-01): a div with id {rootId}-label and the `form` module's label
// role, Native = false, ControlId the Trigger's (or the Input's); the development warning while an Input outside the popup
// is registered (STR-10 restored without the owner stack, ADR 12 Amendments 2026-11-03 item (5)). Combobox only.
/// <summary>The behavior of a combobox label: its id, its label role and its development warning.</summary>
internal sealed class ComboboxLabelBehavior : IAsyncDisposable
{
    /// <summary>Initializes a new instance of the <see cref="ComboboxLabelBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxLabelBehavior(ComboboxPartServices services) => throw new NotImplementedException();

    /// <summary>Gets the field's state the label projects.</summary>
    public FieldStateSnapshot Field => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new NotImplementedException();

    /// <summary>Reads the root and the field and subscribes the part.</summary>
    /// <param name="store">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="field">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public void Subscribe(ComboboxStore? store, FieldRootContext? field, Action requestRender) => throw new NotImplementedException();

    /// <summary>Applies the id the part renders.</summary>
    /// <param name="id">The consumer's id, or <see langword="null"/> for <c>{rootId}-label</c>.</param>
    public void Apply(string? id) => throw new NotImplementedException();

    /// <summary>Adds the label's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes) => throw new NotImplementedException();

    /// <summary>Attaches the label role and warns once per input element change.</summary>
    /// <param name="element">The label's element.</param>
    /// <returns>A task that completes when the role has its options.</returns>
    public Task AfterRenderAsync(ElementReference? element) => throw new NotImplementedException();
}
