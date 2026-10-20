using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

public enum Orientation
{
    Horizontal,
    Vertical,
}

/// <summary>The state of <see cref="SampleRoot"/>: a bool, an enum and a string.</summary>
[PartState]
public sealed partial record SampleRootState(bool Open, Orientation Orientation, string? Label);

/// <summary>
/// The sample part the conformance suite and the pipeline tests run against (issue 31, deliverable 8): a <c>div</c>
/// with <c>role="group"</c>, a part-supplied class and style, a generated id, and one click handler that flips
/// <see cref="Open"/> unless <see cref="Locked"/>. It follows the member order of ADR 08 §5.
/// </summary>
public sealed class SampleRoot : PartBase<SampleRootState>
{
    public const string PartClass = "sample-root";
    public const string PartStyle = "padding: 10px";

    private readonly EventCallback onClick;
    private SampleRootState? state;
    private bool open;

    public SampleRoot()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
    }

    [Parameter]
    public bool DefaultOpen { get; set; }

    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    [Parameter]
    public string? Label { get; set; }

    /// <summary>When set, the click handler runs but changes nothing and requests no render (ADR 16 rule 8).</summary>
    [Parameter]
    public bool Locked { get; set; }

    /// <summary>When set, the click handler throws (the exception route of ADR 16 rule 8).</summary>
    [Parameter]
    public bool ThrowOnClick { get; set; }

    public bool Open => open;

    public int HandlerRuns { get; private set; }

    public int ClassValueCalls { get; private set; }

    /// <summary>The resolved id (RND-DOM-32 (2)–(3)), exposed for the id tests.</summary>
    public string? ResolvedId => Id;

    protected override string DefaultTagName => "div";

    protected override bool UsesGeneratedId => true;

    protected override SampleRootState State => state ??= new SampleRootState(open, Orientation, Label);

    protected override void OnInitialized()
    {
        open = DefaultOpen;
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        state = null;
    }

    /// <summary>Requests a render with the current parameters, for render-count and allocation tests.</summary>
    public void Refresh() => StateHasChanged();

    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        attributes.Set("class", PartClass);
        attributes.Set("style", PartStyle);
        attributes.Set("onclick", onClick);
    }

    private Task HandleClickAsync(MouseEventArgs e)
    {
        HandlerRuns++;
        if (ThrowOnClick)
        {
            throw new InvalidOperationException("SampleRoot click failed on purpose.");
        }

        if (Locked)
        {
            return Task.CompletedTask;
        }

        open = !open;
        state = null;
        StateHasChanged();
        return Task.CompletedTask;
    }
}

/// <summary>A part that supplies no attributes and has an empty state (RND-EDGE-06).</summary>
public sealed class MinimalPart : PartBase<EmptyState>
{
    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;
}

/// <summary>A part whose default element is a <c>button</c> (RND-STR-05).</summary>
public sealed class ButtonPart : PartBase<EmptyState>
{
    protected override string DefaultTagName => "button";

    protected override EmptyState State => EmptyState.Instance;
}

/// <summary>A part whose default element is an <c>img</c> (RND-STR-06).</summary>
public sealed class ImagePart : PartBase<EmptyState>
{
    protected override string DefaultTagName => "img";

    protected override EmptyState State => EmptyState.Instance;
}
