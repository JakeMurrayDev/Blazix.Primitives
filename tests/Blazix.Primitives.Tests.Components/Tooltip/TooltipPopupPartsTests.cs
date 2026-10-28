using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The Portal, Positioner, Popup and Arrow at L2 (TooltipPortal.test.tsx, TooltipPositioner.test.tsx, TooltipPopup.test.tsx,
/// TooltipArrow.test.tsx): the misuse errors, the portal's mounting, the positioner's defaults and inert style, the popup's
/// children and the arrow's attributes.
/// </summary>
public sealed class TooltipPopupPartsTests : IDisposable
{
    private readonly TooltipTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Tooltip.Root>")]
    [Trait("Requirement", "TIP-EDGE-02")]
    [Trait("Requirement", "TIP-STR-05")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        const string message = "Blazix: `TooltipRootContext` is missing. Tooltip parts must be placed within `<TooltipRoot>`.";

        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => host.Context.Render<TooltipPositioner>()).Message);
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => host.Context.Render<TooltipPortal>()).Message);
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => host.Context.Render<TooltipPopup>()).Message);
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => host.Context.Render<TooltipArrow>()).Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Tooltip.Portal>")]
    [Trait("Requirement", "TIP-EDGE-03")]
    [Trait("Requirement", "TIP-STR-04")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_portal()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RenderOpen(builder =>
        {
            builder.OpenComponent<TooltipPositioner>(0);
            builder.CloseComponent();
        }));

        Assert.Equal("Blazix: `<TooltipPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Tooltip.Positioner>")]
    [Trait("Requirement", "TIP-EDGE-04")]
    [Trait("Requirement", "TIP-STR-06")]
    [Trait("Requirement", "TIP-STR-07")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_positioner()
    {
        const string message = "Blazix: `TooltipPositionerContext` is missing. `TooltipPositioner` parts must be placed within `<TooltipPositioner>`.";

        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => RenderOpen(builder =>
        {
            builder.OpenComponent<TooltipPopup>(0);
            builder.CloseComponent();
        })).Message);
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => RenderOpen(builder =>
        {
            builder.OpenComponent<TooltipArrow>(0);
            builder.CloseComponent();
        })).Message);
    }

    [Fact(DisplayName = "should render the children")]
    [Trait("Requirement", "TIP-STR-06")]
    public void Should_render_the_children()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        Assert.Contains("Content", cut.Find("[data-testid=\"popup\"]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "unmounts the closed popup by default")]
    [Trait("Requirement", "TIP-API-21")]
    [Trait("Requirement", "TIP-DOM-06")]
    [Trait("Requirement", "TIP-STA-17")]
    public async Task Unmounts_the_closed_popup_by_default()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));
        Assert.NotNull(cut.Find("[data-base-ui-portal]"));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());
        await cut.InvokeAsync(cut.Instance.Root!.Unmount);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-portal]")));
        Assert.Empty(cut.FindAll("[data-testid=\"positioner\"]"));
    }

    [Fact(DisplayName = "renders the closed popup as hidden instead of unmounting it")]
    [Trait("Requirement", "TIP-API-21")]
    [Trait("Requirement", "TIP-DOM-06")]
    [Trait("Requirement", "TIP-DOM-07")]
    public void Renders_the_closed_popup_as_hidden_instead_of_unmounting_it()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.KeepMounted, true));

        AngleSharp.Dom.IElement positioner = cut.Find("[data-testid=\"positioner\"]");
        Assert.True(positioner.HasAttribute("hidden"));
        Assert.True(positioner.HasAttribute("data-closed"));
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
        Assert.Equal(string.Empty, cut.Find("[data-base-ui-portal]").GetAttribute("data-base-ui-portal"));
    }

    [Fact(DisplayName = "port: the positioner renders role presentation, Tooltip's default side, and sends Tooltip's paddings")]
    [Trait("Requirement", "TIP-POS-01")]
    [Trait("Requirement", "TIP-ARIA-04")]
    [Trait("Requirement", "TIP-DOM-08")]
    public void The_positioner_renders_Tooltips_defaults()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement positioner = cut.Find("[data-testid=\"positioner\"]");
        Assert.Equal("presentation", positioner.GetAttribute("role"));
        Assert.Equal("top", positioner.GetAttribute("data-side"));
        Assert.Equal("center", positioner.GetAttribute("data-align"));
        Assert.True(positioner.HasAttribute("data-open"));
        Assert.False(positioner.HasAttribute("hidden"));
        Assert.DoesNotContain("pointer-events", positioner.GetAttribute("style"), StringComparison.Ordinal);
        JsonElement options = PositioningOptions();
        Assert.Equal("top", options.GetProperty("side").GetString());
        Assert.Equal(5, options.GetProperty("collisionPadding").GetDouble());
        Assert.Equal(5, options.GetProperty("arrowPadding").GetDouble());
        Assert.Equal("trigger", options.GetProperty("triggerId").GetString());
        Assert.Equal(cut.Instance.Root!.Store.SessionToken, options.GetProperty("sessionToken").GetString());
    }

    [Theory(DisplayName = "port: the positioner of an open popup that cannot be hovered renders pointer-events: none ahead of the consumer's style")]
    [InlineData(true, TooltipTrackCursorAxis.None, true)]
    [InlineData(false, TooltipTrackCursorAxis.Both, true)]
    [InlineData(false, TooltipTrackCursorAxis.X, false)]
    [Trait("Requirement", "TIP-DOM-07")]
    [Trait("Requirement", "TIP-API-10")]
    [Trait("Requirement", "TIP-API-11")]
    public void The_positioner_of_an_unhoverable_popup_renders_pointer_events_none(bool disableHoverablePopup, TooltipTrackCursorAxis axis, bool inert)
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.DisableHoverablePopup, disableHoverablePopup).Add(x => x.TrackCursorAxis, axis));

        string style = cut.Find("[data-testid=\"positioner\"]").GetAttribute("style")!;
        Assert.Equal(inert, style.Contains("pointer-events: none", StringComparison.Ordinal));
        Assert.Equal(inert, PositioningOptions().TryGetProperty("style", out JsonElement sent) && sent.GetString()!.StartsWith("pointer-events: none;", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "is hidden from assistive technology and mirrors the resolved side")]
    [Trait("Requirement", "TIP-ARIA-03")]
    [Trait("Requirement", "TIP-DOM-15")]
    [Trait("Requirement", "TIP-STA-13")]
    public void Is_hidden_from_assistive_technology_and_mirrors_the_resolved_side()
    {
        IRenderedComponent<TooltipBasicHost> cut = host.Context.Render<TooltipBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement arrow = cut.Find("[data-testid=\"arrow\"]");
        Assert.Equal("true", arrow.GetAttribute("aria-hidden"));
        Assert.True(arrow.HasAttribute("data-open"));
        Assert.Equal("top", arrow.GetAttribute("data-side"));
        Assert.Contains("position: absolute", arrow.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(arrow.Id, PositioningOptions().GetProperty("arrowId").GetString());
    }

    private JsonElement PositioningOptions()
    {
        JSRuntimeInvocation attach = host.Context.JSInterop.Invocations.Last(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"openSelector\"", StringComparison.Ordinal) is false && json.Contains("\"collisionPadding\"", StringComparison.Ordinal));
        string last = (string)attach.Arguments[1]!;
        foreach (JSRuntimeInvocation update in host.Context.JSInterop.Invocations.Where(invocation => invocation.Identifier == "update" && invocation.Arguments.Count > 0 && invocation.Arguments[0] is string json && json.Contains("\"collisionPadding\"", StringComparison.Ordinal)))
        {
            last = (string)update.Arguments[0]!;
        }

        return JsonDocument.Parse(last).RootElement;
    }

    private IRenderedComponent<TooltipRoot<NoPayload>> RenderOpen(RenderFragment content) =>
        host.Context.Render<TooltipRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.ChildContent, content));
}
