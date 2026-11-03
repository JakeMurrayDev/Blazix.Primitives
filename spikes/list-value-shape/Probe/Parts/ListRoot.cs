using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// SelectRoot<TValue> and ComboboxRoot<TValue>'s value shape (ADR 08 Amendments 2026-11-03 (1)): the single-mode triple,
// the multiple-mode triple over IReadOnlyList<TValue>, `Multiple`, a typed `Items` list, and child content. Elementless:
// it renders a marker of what it received and cascades itself.
[CascadingTypeParameter(nameof(TValue))]
public sealed class ListRoot<TValue> : ComponentBase
{
    private static readonly string[] s_singleNames = [nameof(Value), nameof(ValueChanged), nameof(DefaultValue), nameof(OnValueChange), nameof(ValueExpression)];
    private static readonly string[] s_multipleNames = [nameof(Values), nameof(ValuesChanged), nameof(DefaultValues), nameof(OnValuesChange), nameof(ValuesExpression)];

    private string? singleSupplied;
    private string? multipleSupplied;

    [Parameter]
    public TValue Value { get; set; } = default!;

    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    [Parameter]
    public TValue DefaultValue { get; set; } = default!;

    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    [Parameter]
    public IReadOnlyList<TValue>? Values { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<TValue>> ValuesChanged { get; set; }

    [Parameter]
    public IReadOnlyList<TValue>? DefaultValues { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValuesChange { get; set; }

    [Parameter]
    public Expression<Func<IReadOnlyList<TValue>>>? ValuesExpression { get; set; }

    [Parameter]
    public bool Multiple { get; set; }

    [Parameter]
    public IReadOnlyList<ProbeItem<TValue>>? Items { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // Question 6: the parameter set tells which triple a use supplied, so `Multiple` and the triple are checked at every
    // parameter set, before any parameter is assigned.
    public override Task SetParametersAsync(ParameterView parameters)
    {
        singleSupplied = FirstSupplied(parameters, s_singleNames);
        multipleSupplied = FirstSupplied(parameters, s_multipleNames);
        bool multiple = parameters.TryGetValue<bool>(nameof(Multiple), out bool value) && value;
        if (multiple && singleSupplied is { } single)
        {
            throw new InvalidOperationException($"Blazix: `ListRoot` received `{single}` while `Multiple` is true. In multiple mode, use `Values`, `ValuesChanged` (or `@bind-Values`), `DefaultValues` and `OnValuesChange`.");
        }

        if (!multiple && multipleSupplied is { } many)
        {
            throw new InvalidOperationException($"Blazix: `ListRoot` received `{many}` while `Multiple` is false. Set `Multiple=\"true\"`, or use `Value`, `ValueChanged` (or `@bind-Value`), `DefaultValue` and `OnValueChange` for one value.");
        }

        return base.SetParametersAsync(parameters);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "list-root");
        builder.AddAttribute(1, "tvalue", Probe.TypeName.Of<TValue>());
        builder.AddAttribute(2, "single", singleSupplied ?? "none");
        builder.AddAttribute(3, "multiple", multipleSupplied ?? "none");
        builder.AddAttribute(4, "value-expression", ValueExpression is not null);
        builder.AddAttribute(5, "values-expression", ValuesExpression is not null);
        builder.AddAttribute(6, "items", Items?.Count ?? -1);
        builder.OpenComponent<CascadingValue<ListRoot<TValue>>>(7);
        builder.AddComponentParameter(8, nameof(CascadingValue<ListRoot<TValue>>.Value), this);
        builder.AddComponentParameter(9, nameof(CascadingValue<ListRoot<TValue>>.IsFixed), true);
        builder.AddComponentParameter(10, nameof(CascadingValue<ListRoot<TValue>>.ChildContent), ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
    }

    private static string? FirstSupplied(ParameterView parameters, string[] names)
    {
        foreach (ParameterValue parameter in parameters)
        {
            if (Array.IndexOf(names, parameter.Name) >= 0)
            {
                return parameter.Name;
            }
        }

        return null;
    }
}
