using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only root whose single and list parameters are exclusive, with no <c>Multiple</c> flag (ADR 08 Amendments
/// 2026-11-18, the shared exclusive-triple check; SliderRoot's shape): the library's check first in the parameter set. It
/// renders no element.
/// </summary>
public sealed class TestExclusiveRoot : ComponentBase
{
    [Parameter]
    public double? Value { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<double>> OnValueChange { get; set; }

    [Parameter]
    public IReadOnlyList<double>? Values { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<double>>> OnValuesChange { get; set; }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ListValueParameters.CheckExclusive(parameters, nameof(TestExclusiveRoot));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}
