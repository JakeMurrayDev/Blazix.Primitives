using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// SliderRoot's union shape (decision 12 (a), the "if" branch). Elementless: it renders a marker of what it received. When
// a ValueChanged or an OnValueChange is bound it invokes it once in OnInitializedAsync with a value of the case it
// received (a number 50, or the list [10, 20]), so the harness shows the bind setter writing the consumer's field.
public sealed class SliderRoot : ComponentBase
{
    private string delivered = "none";

    [Parameter]
    public SliderValues? Value { get; set; }

    [Parameter]
    public EventCallback<SliderValues?> ValueChanged { get; set; }

    [Parameter]
    public SliderValues? DefaultValue { get; set; }

    [Parameter]
    public Expression<Func<SliderValues?>>? ValueExpression { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<SliderValues>> OnValueChange { get; set; }

    [CascadingParameter]
    public EditContext? EditContext { get; set; }

    protected override async Task OnInitializedAsync()
    {
        SliderValues? current = Value ?? DefaultValue;
        SliderValues next = current is { IsList: true } ? new SliderValues([10, 20]) : new SliderValues(50);
        if (OnValueChange.HasDelegate)
        {
            await OnValueChange.InvokeAsync(new ValueChangeEventArgs<SliderValues>(next));
            delivered = next.ToString();
        }

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(next);
            delivered = next.ToString();
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        string field = EditContext is null ? "no EditContext" : Show.Field(ValueExpression);
        builder.AddContent(0, $"[SliderRoot Value={Value?.ToString() ?? "null"} DefaultValue={DefaultValue?.ToString() ?? "null"} ValueChanged={(ValueChanged.HasDelegate ? "bound" : "none")} ValueExpression={Show.Expression(ValueExpression)} Field={field} OnValueChange={(OnValueChange.HasDelegate ? "bound" : "none")} delivered={delivered}]");
    }
}
