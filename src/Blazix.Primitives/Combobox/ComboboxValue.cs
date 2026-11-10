using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Combobox.Value (combobox/value/ComboboxValue.tsx; CMB §3.10): no element of its own. Base UI's one `children` prop,
// a node or a function of the selected value, is ChildContent beside a template per mode (design point 2 of M5 track 10,
// settled by the orchestrator 2026-11-04): ValueContent receives the single value, ValuesContent the values.
/// <summary>Represents the selected value of a combobox, Base UI's <c>Combobox.Value</c>, rendered without an element of its own.</summary>
/// <typeparam name="TValue">The type of the values; inferred from the enclosing root.</typeparam>
public sealed class ComboboxValue<TValue> : ComponentBase, IDisposable
{
    private ComboboxValueBehavior<TValue>? behavior;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    /// <summary>Gets or sets content rendered in place of the selected value's label.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the template of a single root, called with the selected value, or the default value when none is selected.</summary>
    [Parameter]
    public RenderFragment<TValue>? ValueContent { get; set; }

    /// <summary>Gets or sets the template of a multiple root, called with the selected values, an empty list when none are selected.</summary>
    [Parameter]
    public RenderFragment<IReadOnlyList<TValue>>? ValuesContent { get; set; }

    /// <summary>Gets or sets the content rendered while no value is selected and no other content is given.</summary>
    [Parameter]
    public RenderFragment? Placeholder { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxValueBehavior<TValue>(StateHasChanged);
        behavior.Subscribe(Root, "Combobox");
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => behavior?.Dispose();
}
