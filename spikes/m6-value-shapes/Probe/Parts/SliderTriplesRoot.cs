using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// SliderRoot's fallback shape (decision 12 (a), the "otherwise" branch): two triples, `Value : double?` and
// `Values : IReadOnlyList<double>?`, with OnValueChange and OnValuesChange, checked at every parameter set by a
// ListValueParameters.Check-like check (no `Multiple`: supplying a parameter of both triples is the error). Elementless; it
// invokes a bound callback once in OnInitializedAsync (a number 50, or the list [10, 20]).
public sealed class SliderTriplesRoot : ComponentBase
{
    private string delivered = "none";

    [Parameter]
    public double? Value { get; set; }

    [Parameter]
    public EventCallback<double?> ValueChanged { get; set; }

    [Parameter]
    public double? DefaultValue { get; set; }

    [Parameter]
    public Expression<Func<double?>>? ValueExpression { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<double>> OnValueChange { get; set; }

    [Parameter]
    public IReadOnlyList<double>? Values { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<double>?> ValuesChanged { get; set; }

    [Parameter]
    public IReadOnlyList<double>? DefaultValues { get; set; }

    [Parameter]
    public Expression<Func<IReadOnlyList<double>?>>? ValuesExpression { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<double>>> OnValuesChange { get; set; }

    [CascadingParameter]
    public EditContext? EditContext { get; set; }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        string? single = null;
        string? many = null;
        foreach (ParameterValue parameter in parameters)
        {
            switch (parameter.Name)
            {
                case "Value" or "ValueChanged" or "DefaultValue" or "OnValueChange" or "ValueExpression":
                    single ??= parameter.Name;
                    break;
                case "Values" or "ValuesChanged" or "DefaultValues" or "OnValuesChange" or "ValuesExpression":
                    many ??= parameter.Name;
                    break;
            }
        }

        if (single is not null && many is not null)
        {
            throw new InvalidOperationException($"Blazix: `SliderRoot` received `{single}` and `{many}`. Use `Value`, `ValueChanged` (or `@bind-Value`), `DefaultValue` and `OnValueChange` for one thumb, or `Values`, `ValuesChanged` (or `@bind-Values`), `DefaultValues` and `OnValuesChange` for a range.");
        }

        return base.SetParametersAsync(parameters);
    }

    protected override async Task OnInitializedAsync()
    {
        if (OnValueChange.HasDelegate)
        {
            await OnValueChange.InvokeAsync(new ValueChangeEventArgs<double>(50));
            delivered = "number:50";
        }

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(50);
            delivered = "number:50";
        }

        IReadOnlyList<double> range = [10, 20];
        if (OnValuesChange.HasDelegate)
        {
            await OnValuesChange.InvokeAsync(new ValueChangeEventArgs<IReadOnlyList<double>>(range));
            delivered = "list:" + Show.List(range);
        }

        if (ValuesChanged.HasDelegate)
        {
            await ValuesChanged.InvokeAsync(range);
            delivered = "list:" + Show.List(range);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        string field = EditContext is null
            ? "no EditContext"
            : ValuesExpression is not null ? Show.Field(ValuesExpression) : Show.Field(ValueExpression);
        string value = Value is { } number ? Show.Number(number) : "null";
        string defaultValue = DefaultValue is { } fallback ? Show.Number(fallback) : "null";
        builder.AddContent(0, $"[SliderTriplesRoot Value={value} DefaultValue={defaultValue} Values={Show.List(Values)} DefaultValues={Show.List(DefaultValues)} ValueChanged={(ValueChanged.HasDelegate ? "bound" : "none")} ValuesChanged={(ValuesChanged.HasDelegate ? "bound" : "none")} ValueExpression={Show.Expression(ValueExpression)} ValuesExpression={Show.Expression(ValuesExpression)} Field={field} delivered={delivered}]");
    }
}
