using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// An item of the list (SelectItem<TValue>'s shape): `Value` told omitted by whether the parameter was supplied
// (ADR 08 Amendments 2026-11-03 (2), the `Payload` precedent), and the root it found.
public sealed class ListItem<TValue> : ComponentBase
{
    private bool valueSupplied;

    [CascadingParameter]
    private ListRoot<TValue>? Root { get; set; }

    [Parameter]
    public TValue Value { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        return base.SetParametersAsync(parameters);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "list-item");
        builder.AddAttribute(1, "tvalue", Probe.TypeName.Of<TValue>());
        builder.AddAttribute(2, "value-supplied", valueSupplied);
        builder.AddAttribute(3, "value", valueSupplied ? Value?.ToString() ?? "null" : "omitted");
        builder.AddAttribute(4, "root", Root is not null);
        builder.AddContent(5, ChildContent);
        builder.CloseElement();
    }
}
