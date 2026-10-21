using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> rows of docs/components/collapsible.md that <c>CollapsiblePanel.test.tsx</c> covers: <c>keepMounted</c> and
/// the hidden attribute (COL-API-08, COL-DOM-11/13), the render function's single close request (COL-EVT-06), the style C#
/// renders before the handover (COL-ANI-14, COL-DOM-18) and the <c>hiddenUntilFound</c> warning (COL-EDGE-02). Titles are
/// Base UI's; the `until-found` literal and every motion are the same titles at L4.
/// </summary>
public sealed class CollapsiblePanelTests : IDisposable
{
    private const string PanelContent = "This is panel content";

    private readonly CollapsibleTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "does not unmount the panel when true")]
    [Trait("Requirement", "COL-API-08")]
    [Trait("Requirement", "COL-DOM-11")]
    [Trait("Requirement", "COL-DOM-13")]
    [Trait("Requirement", "COL-ARIA-02")]
    [Trait("Requirement", "COL-DOM-04")]
    public void Does_not_unmount_the_panel_when_true()
    {
        IRenderedComponent<CollapsibleConsumers> cut = Context.Render<CollapsibleConsumers>(p => p
            .Add(x => x.Mode, CollapsibleConsumers.ConsumerMode.Applying)
            .Add(x => x.KeepMounted, true));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-panel]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-panel]").HasAttribute("data-closed"));

        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Equal(cut.Find("[data-panel]").GetAttribute("id"), cut.Find("button").GetAttribute("aria-controls"));
        Assert.False(cut.Find("[data-panel]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-panel]").HasAttribute("data-open"));
        Assert.True(cut.Find("button").HasAttribute("data-panel-open"));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.False(cut.Find("button").HasAttribute("aria-controls"));
        Assert.True(cut.Find("[data-panel]").HasAttribute("data-closed"));
        host.CompletePanelExit(cut);
        Assert.True(cut.Find("[data-panel]").HasAttribute("hidden"));
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "hides the panel after external controlled close when true and no animations are applied")]
    [Trait("Requirement", "COL-DOM-13")]
    public void Hides_the_panel_after_external_controlled_close_when_true_and_no_animations_are_applied()
    {
        // COL-DOM-13's C# clause: the boolean hidden exactly while closed and not mounted; a consumer's hidden wins.
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Open, false)
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "panel"))
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "shown").AddUnmatched("hidden", false)));

        Assert.True(cut.Find("[data-testid=panel]").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-testid=shown]").HasAttribute("hidden"));

        cut.Render(p => p.Add(x => x.Open, true));
        Assert.False(cut.Find("[data-testid=panel]").HasAttribute("hidden"));

        cut.Render(p => p.Add(x => x.Open, false));
        // Closed but still mounted until the module reports the end of the exit.
        Assert.False(cut.Find("[data-testid=panel]").HasAttribute("hidden"));
        host.CompletePanelExit(cut);
        Assert.True(cut.Find("[data-testid=panel]").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-testid=panel]").HasAttribute("data-ending-style"));
    }

    [Fact(DisplayName = "finishes closing when the render function removes the panel element")]
    [Trait("Requirement", "COL-EVT-06")]
    public void Finishes_closing_when_the_render_function_removes_the_panel_element()
    {
        List<bool> requested = [];
        RenderFragment<RenderProps<CollapsiblePanelState>> removable = props => builder =>
        {
            if (props.State.Open)
            {
                builder.OpenElement(0, "div");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.AddContent(2, props.ChildContent);
                builder.CloseElement();
            }
        };
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) => requested.Add(args.Value))
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Trigger"))
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.RenderFunction, removable).AddChildContent(PanelContent)));

        Assert.True(cut.Find("div > div").HasAttribute("data-open"));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
        // COL-EVT-06: one request, and none for the end of the close.
        Assert.Equal([false], requested);
    }

    [Fact(DisplayName = "suppresses the initial keyframe animation when rendered open")]
    [Trait("Requirement", "COL-ANI-14")]
    [Trait("Requirement", "COL-DOM-17")]
    [Trait("Requirement", "COL-DOM-18")]
    public void Suppresses_the_initial_keyframe_animation_when_rendered_open()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("class", "animation-test-panel").AddUnmatched("data-testid", "panel")));

        IReadOnlyDictionary<string, string> style = Declarations(cut.Find("[data-testid=panel]").GetAttribute("style"));
        Assert.Equal("none", style["animation-name"]);
        Assert.Equal("auto", style["--collapsible-panel-height"]);
        Assert.Equal("auto", style["--collapsible-panel-width"]);
    }

    [Fact(DisplayName = "suppresses the initial keyframe animation from inline styles when rendered open")]
    [Trait("Requirement", "COL-ANI-14")]
    [Trait("Requirement", "COL-DOM-18")]
    public void Suppresses_the_initial_keyframe_animation_from_inline_styles_when_rendered_open()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsiblePanel>(panel => panel
                .AddUnmatched("data-testid", "panel")
                .AddUnmatched("style", "animation-duration: 100ms; animation-name: panel-slide-down; animation-timing-function: linear")));

        IReadOnlyDictionary<string, string> style = Declarations(cut.Find("[data-testid=panel]").GetAttribute("style"));
        Assert.Equal("none", style["animation-name"]);
        Assert.Equal("100ms", style["animation-duration"]);
    }

    [Fact(DisplayName = "a closed panel renders no animation-name suppression")]
    [Trait("Requirement", "COL-ANI-13")]
    public void A_closed_panel_renders_no_animation_name_suppression()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "panel")));

        Assert.False(Declarations(cut.Find("[data-testid=panel]").GetAttribute("style")).ContainsKey("animation-name"));
    }

    [Fact(DisplayName = "warns when hiddenUntilFound overrides keepMounted={false}")]
    [Trait("Requirement", "COL-EDGE-02")]
    [Trait("Requirement", "COL-DOM-14")]
    public void Warns_when_hiddenUntilFound_overrides_keepMounted_false()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsiblePanel>(panel => panel
                .Add(x => x.HiddenUntilFound, true)
                .Add(x => x.KeepMounted, false)
                .AddChildContent(PanelContent)));
        cut.Render();

        (LogLevel level, string message) = Assert.Single(host.PanelLogger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "Blazix: The `KeepMounted` parameter set to `false` on `CollapsiblePanel` is ignored when `HiddenUntilFound` is enabled, since the panel must remain mounted while closed.",
            message);
        // The panel stays in the document, hidden; the module writes the `until-found` literal (L4).
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("div > div").HasAttribute("hidden"));
    }

    private static IReadOnlyDictionary<string, string> Declarations(string? style)
    {
        Dictionary<string, string> declarations = new(StringComparer.Ordinal);
        foreach (string declaration in (style ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = declaration.IndexOf(':', StringComparison.Ordinal);
            declarations[declaration[..colon].Trim()] = declaration[(colon + 1)..].Trim();
        }

        return declarations;
    }
}
