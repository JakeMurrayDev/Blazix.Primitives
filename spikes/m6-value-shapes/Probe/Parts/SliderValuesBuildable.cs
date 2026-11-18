using System.Collections;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// An option beside the brief's shape, for the demo's `DefaultValue="@([25, 45])"` (CS9174 on SliderValues): the same union
// made a collection type, [CollectionBuilder] plus IEnumerable<double> (a number enumerates as its one element).
[Union, CollectionBuilder(typeof(SliderValuesBuildable), nameof(Create))]
public readonly struct SliderValuesBuildable : IUnion, IEnumerable<double>
{
    private readonly double[]? values;
    private readonly double value;

    public SliderValuesBuildable(double value) => this.value = value;

    public SliderValuesBuildable(IReadOnlyList<double> values) => this.values = [.. values];

    public object? Value => values is null ? value : values;

    public static SliderValuesBuildable Create(ReadOnlySpan<double> values) => new(values.ToArray());

    public static implicit operator SliderValuesBuildable(double value) => new(value);

    public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)(values ?? [value])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => values is null ? "number:" + Show.Number(value) : "list:" + Show.List(values);
}

public sealed class SliderBuildableRoot : ComponentBase
{
    [Parameter]
    public SliderValuesBuildable? Value { get; set; }

    [Parameter]
    public SliderValuesBuildable? DefaultValue { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, $"[SliderBuildableRoot Value={Value?.ToString() ?? "null"} DefaultValue={DefaultValue?.ToString() ?? "null"}]");
}
