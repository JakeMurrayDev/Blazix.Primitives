using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// A test-only consumer of the toolbar's context, read optionally as ToggleGroup and the popups read it: it renders the
/// toolbar's orientation, or <c>outside</c> without a toolbar. ToolbarRoot.test.tsx's <c>OptionalToolbarConsumer</c>
/// calls the internal <c>useToolbarRootContext(true)</c>.
/// </summary>
internal sealed class OptionalToolbarConsumer : ComponentBase
{
    [CascadingParameter]
    private ToolbarRootContext? Toolbar { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddContent(1, Toolbar is null ? "outside" : Toolbar.Orientation == Iodine.Headless.Orientation.Vertical ? "vertical" : "horizontal");
        builder.CloseElement();
    }
}
