using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The internal backdrop's markup (PSH-STR-09, PSH-DOM-14, PSH-ARIA-06; InternalBackdrop.tsx), written once in
/// <c>Popup/</c> for Dialog's Portal and Popover's Positioner (design point 3 (a) of M4 track 8, settled by the orchestrator
/// 2026-10-28): the element, its id, the marker the outside press reads, the style, and <c>inert</c> only when asked.
/// </summary>
public sealed class PopupInternalBackdropTests : IDisposable
{
    private readonly BunitContext context = new();

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: the internal backdrop renders its id, role presentation, the inert marker and the covering style")]
    [Trait("Requirement", "PSH-DOM-14")]
    [Trait("Requirement", "PSH-ARIA-06")]
    public void The_internal_backdrop_renders_its_markup()
    {
        IRenderedComponent<IComponent> cut = context.Render(builder => PopupInternalBackdrop.Render(builder, "backdrop-1", inert: false));

        IElement backdrop = cut.Find("div");
        Assert.Equal("backdrop-1", backdrop.Id);
        Assert.Equal("presentation", backdrop.GetAttribute("role"));
        Assert.Equal(string.Empty, backdrop.GetAttribute("data-base-ui-inert"));
        Assert.Equal("position: fixed; inset: 0; user-select: none; -webkit-user-select: none", backdrop.GetAttribute("style"));
        Assert.False(backdrop.HasAttribute("inert"));
    }

    [Fact(DisplayName = "port: the internal backdrop carries inert while its popup closes, and hands its element to the capture")]
    [Trait("Requirement", "PSH-DOM-14")]
    public void The_internal_backdrop_carries_inert_and_hands_its_element_to_the_capture()
    {
        ElementReference? captured = null;
        IRenderedComponent<IComponent> cut = context.Render(builder => PopupInternalBackdrop.Render(builder, "backdrop-2", inert: true, element => captured = element));

        Assert.True(cut.Find("div").HasAttribute("inert"));
        Assert.NotNull(captured);
    }
}
