using System.Linq.Expressions;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only list root of <c>SelectRoot&lt;TValue&gt;</c> and <c>ComboboxRoot&lt;TValue&gt;</c>'s value shape (ADR 08
/// Amendments 2026-11-03 (1), the probe's finding of 2026-11-04): both triples, <see cref="Multiple"/>, the library's
/// mismatch check first in the parameter set, and a fixed cascade of itself to its items. It renders no element.
/// </summary>
[CascadingTypeParameter(nameof(TValue))]
public sealed class TestListRoot<TValue> : ComponentBase
{
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
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ListValueParameters.Check(parameters, "TestListRoot");
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<TestListRoot<TValue>>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TestListRoot<TValue>>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<TestListRoot<TValue>>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TestListRoot<TValue>>.ChildContent), ChildContent);
        builder.CloseComponent();
    }
}

/// <summary>
/// A test-only item of <see cref="TestListRoot{TValue}"/>: it tells an omitted <see cref="Value"/> from
/// <c>default(TValue)</c> by whether the parameter was supplied (ADR 08 Amendments 2026-11-03 (2)), and renders a marker.
/// </summary>
public sealed class TestListItem<TValue> : ComponentBase
{
    [CascadingParameter]
    internal TestListRoot<TValue>? Root { get; set; }

    [Parameter]
    public TValue Value { get; set; } = default!;

    internal bool ValueSupplied { get; private set; }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ValueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-list-item", ValueSupplied ? Value?.ToString() : "omitted");
        builder.CloseElement();
    }
}
