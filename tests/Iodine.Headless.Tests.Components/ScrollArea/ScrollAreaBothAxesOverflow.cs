using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only stand-in for the measurement report, for the Corner's conformance suite, which renders the part fresh in
/// every test: placed inside a root before its content, it applies "both axes overflow" to the root's state as the root's
/// twin does on the module's report (ScrollAreaMeasurementTests drive the twin itself). Its content renders only after the
/// root re-rendered with that state, so the part under test renders once (bUnit's capture markup is lost on a re-render).
/// </summary>
public sealed class ScrollAreaBothAxesOverflow : ComponentBase
{
    [CascadingParameter]
    internal ScrollAreaRootContext? RootContext { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private bool applied;

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            applied = true;
            RootContext!.ApplyOverflow(new ScrollAreaOverflow(true, true, false, true, false, true), measured: true);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        if (applied)
        {
            builder.AddContent(0, ChildContent);
        }
    }
}
