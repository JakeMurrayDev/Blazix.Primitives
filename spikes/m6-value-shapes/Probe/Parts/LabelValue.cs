using System.Runtime.CompilerServices;

namespace Probe.Parts;

// A comparison for the Razor literal rule on this SDK: a union with a `string` case and a delegate case, the
// ClassValue<TState> shape (string | Func<TState, string?>), against SnapPointValue's `string | double`.
[Union]
public readonly struct LabelValue : IUnion
{
    private readonly object? value;

    public LabelValue(string value) => this.value = value;

    public LabelValue(Func<int, string> value) => this.value = value;

    public object? Value => value;

    public static implicit operator LabelValue(string value) => new(value);

    public override string ToString() => value switch
    {
        string text => "string:" + text,
        Func<int, string> => "func",
        _ => "none",
    };
}

public sealed class LabelPart : Microsoft.AspNetCore.Components.ComponentBase
{
    [Microsoft.AspNetCore.Components.Parameter]
    public LabelValue? Label { get; set; }

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
        builder.AddContent(0, $"[LabelPart Label={Label?.ToString() ?? "null"}]");
}
