using System.Text.Json;
using AngleSharp.Dom;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// What C# projects from the <c>scroll-area</c> module's reports (SCR §4.2-§4.4, §5; ADR 04 Amendments 2026-11-18: the
/// overflow attributes, <c>tabindex</c> and the scrollbars' and the corner's presence stay C#-projected from the overflow
/// report), and what each part's handle receives. The module is bUnit's interop mock; the tests report as the module does.
/// The browser halves (the measurement itself, <c>data-scrolling</c>, <c>data-hovering</c>, the measured sizes) are L4.
/// </summary>
public sealed class ScrollAreaMeasurementTests : IDisposable
{
    private readonly ScrollAreaTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "applies data attributes on root, viewport and scrollbars based on overflow and edges")]
    [Trait("Requirement", "SCR-API-01")]
    [Trait("Requirement", "SCR-STA-02")]
    [Trait("Requirement", "SCR-DOM-03")]
    [Trait("Requirement", "SCR-DOM-10")]
    [Trait("Requirement", "SCR-DOM-14")]
    [Trait("Requirement", "SCR-DOM-16")]
    public async Task Applies_data_attributes_on_root_viewport_and_scrollbars_based_on_overflow_and_edges()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>();

        // In the middle of content that overflows on both axes: all six attributes on the four parts, none on the thumbs.
        await test.OverflowAsync(cut, hasX: true, hasY: true, xStart: true, xEnd: true, yStart: true, yEnd: true);

        string[] expected = ["data-has-overflow-x", "data-has-overflow-y", "data-overflow-x-start", "data-overflow-x-end", "data-overflow-y-start", "data-overflow-y-end"];
        foreach (string part in new[] { "root", "viewport", "content", "scrollbar-y", "scrollbar-x" })
        {
            IElement element = cut.Find($"[data-testid={part}]");
            Assert.All(expected, name => Assert.Equal(string.Empty, element.GetAttribute(name)));
        }

        Assert.DoesNotContain(cut.Find("[data-testid=thumb-y]").Attributes, a => a.Name.StartsWith("data-overflow", StringComparison.Ordinal) || a.Name.StartsWith("data-has", StringComparison.Ordinal));

        // At the start: only the end edges.
        await test.OverflowAsync(cut, hasX: true, hasY: true, xEnd: true, yEnd: true);

        IElement root = cut.Find("[data-testid=root]");
        Assert.False(root.HasAttribute("data-overflow-x-start"));
        Assert.False(root.HasAttribute("data-overflow-y-start"));
        Assert.True(root.HasAttribute("data-overflow-y-end"));
        Assert.False(root.HasAttribute("data-cornerhidden"));
    }

    [Fact(DisplayName = "does not add state attributes when content does not overflow")]
    [Trait("Requirement", "SCR-API-07")]
    [Trait("Requirement", "SCR-DOM-03")]
    public async Task Does_not_add_state_attributes_when_content_does_not_overflow()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.KeepMounted, true));

        await test.OverflowAsync(cut);

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("presentation", root.GetAttribute("role"));
        Assert.DoesNotContain(root.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal) && a.Name != "data-testid");
        Assert.Single(cut.FindAll("[data-testid=scrollbar-y]"));
        Assert.Single(cut.FindAll("[data-testid=scrollbar-x]"));
        Assert.Empty(cut.FindAll("[data-testid=corner]"));
    }

    [Fact(DisplayName = "measures content mounted after the viewport initial measurement")]
    [Trait("Requirement", "SCR-STA-08")]
    [Trait("Requirement", "SCR-DOM-08")]
    public async Task Measures_content_mounted_after_the_viewport_initial_measurement()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>();
        await test.OverflowAsync(cut);
        Assert.Equal("-1", cut.Find("[data-testid=viewport]").GetAttribute("tabindex"));
        Assert.False(cut.Find("[data-testid=viewport]").HasAttribute("data-has-overflow-y"));

        await test.OverflowAsync(cut, hasY: true, yEnd: true);

        Assert.Equal("0", cut.Find("[data-testid=viewport]").GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: a consumer tabindex replaces the computed one after a measurement too")]
    [Trait("Requirement", "SCR-API-05")]
    [Trait("Requirement", "SCR-DOM-08")]
    public async Task A_consumer_tabindex_replaces_the_computed_one()
    {
        IRenderedComponent<ScrollAreaRoot> cut = test.Context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaViewport>(v => v.AddUnmatched("tabindex", "-1").AddUnmatched("data-testid", "viewport")));

        await test.OverflowAsync(cut, hasY: true, yEnd: true);

        IElement viewport = cut.Find("[data-testid=viewport]");
        Assert.True(viewport.HasAttribute("data-has-overflow-y"));
        Assert.Equal("-1", viewport.GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "recomputes corner size when content starts overflowing")]
    [Trait("Requirement", "SCR-DOM-15")]
    [Trait("Requirement", "SCR-DOM-26")]
    [Trait("Requirement", "SCR-DOM-27")]
    public async Task Recomputes_corner_size_when_content_starts_overflowing()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.CornerStyle, "background: red"));
        Assert.Empty(cut.FindAll("[data-testid=scrollbar-y]"));

        // Half size: one axis, no corner.
        await test.OverflowAsync(cut, hasY: true, yEnd: true);
        Assert.Single(cut.FindAll("[data-testid=scrollbar-y]"));
        Assert.Empty(cut.FindAll("[data-testid=scrollbar-x]"));
        Assert.Empty(cut.FindAll("[data-testid=corner]"));

        // Full size: both scrollbars and the corner, which appears at 0px; the module sizes it (SCR-ANI-09, L4).
        await test.OverflowAsync(cut, hasX: true, hasY: true, xEnd: true, yEnd: true);
        IElement corner = cut.Find("[data-testid=corner]");
        Assert.Single(cut.FindAll("[data-testid=scrollbar-x]"));
        Assert.Equal("position: absolute; bottom: 0; inset-inline-end: 0; width: 0px; height: 0px; background: red;", corner.GetAttribute("style"));
        Assert.Equal("true", corner.GetAttribute("aria-hidden"));
        Assert.DoesNotContain(corner.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal) && a.Name != "data-testid");
    }

    [Fact(DisplayName = "clears corner, overflow attributes, and metrics when content stops overflowing")]
    [Trait("Requirement", "SCR-ANI-10")]
    [Trait("Requirement", "SCR-DOM-26")]
    [Trait("Requirement", "SCR-DOM-15")]
    public async Task Clears_corner_overflow_attributes_and_metrics_when_content_stops_overflowing()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>();
        await test.OverflowAsync(cut, hasX: true, hasY: true, xEnd: true, yEnd: true);
        Assert.Single(cut.FindAll("[data-testid=corner]"));

        // Removed in the commit that changes the overflow state: no transition status, no deferred unmount.
        await test.OverflowAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=corner]"));
        Assert.Empty(cut.FindAll("[data-testid=scrollbar-y]"));
        Assert.Empty(cut.FindAll("[data-testid=scrollbar-x]"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-has-overflow-x"));
        Assert.DoesNotContain(cut.FindAll("[data-starting-style], [data-ending-style]"), _ => true);
    }

    [Fact(DisplayName = "registers after the horizontal scrollbar becomes visible")]
    [Trait("Requirement", "SCR-DOM-15")]
    public async Task Registers_after_the_horizontal_scrollbar_becomes_visible()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.Corner, false));
        Assert.Empty(test.AttachesOf("scrollbar"));

        await test.OverflowAsync(cut, hasX: true, xEnd: true);

        JsonElement attach = Assert.Single(test.AttachesOf("scrollbar"));
        Assert.Equal("horizontal", attach.GetProperty("orientation").GetString());
        Assert.Equal(0, attach.GetProperty("rootRef").GetInt32());
    }

    [Fact(DisplayName = "port: no part renders a role or an aria attribute but the root's and content's presentation role and the scrollbars' and corner's aria-hidden")]
    [Trait("Requirement", "SCR-ARIA-06")]
    [Trait("Requirement", "SCR-ARIA-03")]
    public async Task No_part_renders_another_role_or_aria_attribute()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>();

        await test.OverflowAsync(cut, hasX: true, hasY: true, xEnd: true, yEnd: true);

        string[] allowed = ["root:role", "content:role", "scrollbar-y:aria-hidden", "scrollbar-x:aria-hidden", "corner:aria-hidden"];
        List<string> found = [];
        foreach (AngleSharp.Dom.IElement element in cut.FindAll("[data-testid]"))
        {
            foreach (AngleSharp.Dom.IAttr attribute in element.Attributes)
            {
                if (attribute.Name == "role" || attribute.Name.StartsWith("aria-", StringComparison.Ordinal))
                {
                    found.Add($"{element.GetAttribute("data-testid")}:{attribute.Name}");
                }
            }
        }

        Assert.Equal(allowed.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = "port: the corner's state is its empty record")]
    [Trait("Requirement", "SCR-STA-05")]
    public async Task The_corners_state_is_its_empty_record()
    {
        ScrollAreaCornerState? seen = null;
        IRenderedComponent<ScrollAreaRoot> cut = test.Context.Render<ScrollAreaRoot>(p => p.AddChildContent<ScrollAreaCorner>(c => c
            .Add(x => x.ClassValue, s =>
            {
                seen = s;
                return null;
            })));

        await test.OverflowAsync(cut, hasX: true, hasY: true);

        Assert.Same(ScrollAreaCornerState.Instance, seen);
        Assert.DoesNotContain(typeof(ScrollAreaCornerState).GetProperties(), p => p.GetMethod?.IsStatic == false && p.Name != "EqualityContract");
    }

    [Fact(DisplayName = "is hidden from the accessibility tree by default")]
    [Trait("Requirement", "SCR-ARIA-05")]
    public async Task The_corner_is_hidden_from_the_accessibility_tree_by_default()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>();

        await test.OverflowAsync(cut, hasX: true, hasY: true);

        Assert.Equal("true", cut.Find("[data-testid=corner]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "allows overriding aria-hidden")]
    [Trait("Requirement", "SCR-ARIA-05")]
    public async Task The_corner_allows_overriding_aria_hidden()
    {
        // Base UI removes it with `aria-hidden={undefined}`; the port's override is a value (the MenuGroupLabel precedent).
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.CornerAriaHidden, "false"));

        await test.OverflowAsync(cut, hasX: true, hasY: true);

        Assert.Equal("false", cut.Find("[data-testid=corner]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "shows keepMounted scrollbar track and thumb after mount compute")]
    [Trait("Requirement", "SCR-DOM-19")]
    [Trait("Requirement", "SCR-DOM-23")]
    [Trait("Requirement", "SCR-API-07")]
    public async Task Shows_kept_mounted_scrollbar_track_and_thumb_after_mount_compute()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.KeepMounted, true));
        Assert.StartsWith("visibility: hidden", test.LastOptionsOf("thumb").GetProperty("style").GetString());
        Assert.DoesNotContain("visibility", test.LastOptionsOf("scrollbar").GetProperty("style").GetString());

        // A measurement that finds no overflow still measured: C#'s declarations, sent through `update`, drop the
        // visibility (the handover: the rendered style stays frozen and the module applies the new string).
        await test.OverflowAsync(cut);

        Assert.Equal(
            ["height: var(--scroll-area-thumb-height)", "width: var(--scroll-area-thumb-width)"],
            test.UpdatesOf("thumb").Select(o => o.GetProperty("style").GetString()).Order(StringComparer.Ordinal));
        Assert.StartsWith("visibility: hidden", cut.Find("[data-testid=thumb-y]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: a non-kept-mounted scrollbar shown by a measurement carries no visibility")]
    [Trait("Requirement", "SCR-DOM-19")]
    public async Task A_scrollbar_shown_by_a_measurement_carries_no_visibility()
    {
        // §16 Q3: the hidden branch of a non-kept-mounted track is unreachable, kept as the source has it.
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>();

        await test.OverflowAsync(cut, hasY: true, yEnd: true);

        Assert.DoesNotContain("visibility", cut.Find("[data-testid=scrollbar-y]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the root sends the normalized thresholds")]
    [Trait("Requirement", "SCR-API-01")]
    [Trait("Requirement", "SCR-API-02")]
    [Trait("Requirement", "SCR-API-03")]
    public void The_root_sends_the_normalized_thresholds()
    {
        test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.Threshold, 20));
        test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.Threshold, new ScrollAreaOverflowEdges(XStart: 20, YStart: 5)));
        test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.Threshold, new ScrollAreaOverflowEdges(XStart: -5, XEnd: double.NaN, YStart: 3)));
        test.Context.Render<ScrollAreaHost>();

        IReadOnlyList<JsonElement> roots = test.AttachesOf("root");
        Assert.Equal("""{"xStart":20,"xEnd":20,"yStart":20,"yEnd":20}""", roots[0].GetProperty("thresholds").GetRawText());
        Assert.Equal("""{"xStart":20,"xEnd":0,"yStart":5,"yEnd":0}""", roots[1].GetProperty("thresholds").GetRawText());
        Assert.Equal("""{"xStart":0,"xEnd":0,"yStart":3,"yEnd":0}""", roots[2].GetProperty("thresholds").GetRawText());
        Assert.Equal("""{"xStart":0,"xEnd":0,"yStart":0,"yEnd":0}""", roots[3].GetProperty("thresholds").GetRawText());
    }

    [Fact(DisplayName = "port: a threshold given as a Razor literal binds")]
    [Trait("Requirement", "SCR-API-02")]
    public void A_threshold_given_as_a_razor_literal_binds()
    {
        // The probe's binding (ADR 08 Amendments 2026-11-18): `OverflowEdgeThreshold="20"` is an int literal through double.
        IRenderedComponent<ScrollAreaThresholdLiteralHost> cut = test.Context.Render<ScrollAreaThresholdLiteralHost>();

        Assert.Equal(new OverflowEdgeThresholdValue(20), cut.FindComponent<ScrollAreaRoot>().Instance.OverflowEdgeThreshold);
        Assert.Equal(20, test.AttachesOf("root")[0].GetProperty("thresholds").GetProperty("yEnd").GetDouble());
    }

    [Fact(DisplayName = "correctly handles RTL")]
    [Trait("Requirement", "SCR-I18N-01")]
    public void Correctly_handles_rtl()
    {
        // The reading direction is the nearest DirectionProvider's, never CSS: the root sends it to the module.
        test.Context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent<ScrollAreaRoot>(r => r.AddUnmatched("style", "direction: ltr")));
        test.Context.Render<ScrollAreaRoot>(r => r.AddUnmatched("dir", "rtl"));

        Assert.True(test.AttachesOf("root")[0].GetProperty("rtl").GetBoolean());
        Assert.False(test.AttachesOf("root")[1].GetProperty("rtl").GetBoolean());
    }

    [Fact(DisplayName = "recomputes horizontal overflow edges when direction changes")]
    [Trait("Requirement", "SCR-I18N-01")]
    [Trait("Requirement", "SCR-ANI-02")]
    public void Recomputes_horizontal_overflow_edges_when_direction_changes()
    {
        IRenderedComponent<DirectionProvider> cut = test.Context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Ltr)
            .AddChildContent<ScrollAreaRoot>());

        cut.Render(p => p.Add(x => x.Direction, TextDirection.Rtl));

        // The module measures again on the update that carries the new direction (L3, L4).
        Assert.True(test.UpdatesOf("root")[^1].GetProperty("rtl").GetBoolean());
    }

    [Fact(DisplayName = "port: the scrolling and hovering reports reach the state functions, and C# renders no data-scrolling or data-hovering")]
    [Trait("Requirement", "SCR-STA-01")]
    [Trait("Requirement", "SCR-STA-03")]
    [Trait("Requirement", "SCR-STA-04")]
    [Trait("Requirement", "SCR-STA-12")]
    public async Task The_scrolling_and_hovering_reports_reach_the_state_functions()
    {
        ScrollAreaRootState? root = null;
        ScrollAreaScrollbarState? scrollbar = null;
        ScrollAreaThumbState? thumb = null;
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.RootClass, s => (root = s).Scrolling ? "scrolling" : null)
            .Add(x => x.ScrollbarClass, s => (scrollbar = s).Hovering ? "hovering" : null)
            .Add(x => x.ThumbClass, s => (thumb = s).Scrolling ? "scrolling" : null));

        await test.ScrollingAsync(cut, x: true, y: false);
        Assert.True(root!.Scrolling);
        Assert.False(scrollbar!.Scrolling);
        Assert.False(thumb!.Scrolling);

        await test.ScrollingAsync(cut, x: false, y: true);
        await test.HoveringAsync(cut, true);

        Assert.True(scrollbar!.Scrolling);
        Assert.True(scrollbar.Hovering);
        Assert.Equal(Primitives.Orientation.Vertical, scrollbar.Orientation);
        Assert.True(thumb!.Scrolling);
        Assert.Equal("hovering", cut.Find("[data-testid=scrollbar-y]").GetAttribute("class"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-scrolling"));
        Assert.False(cut.Find("[data-testid=scrollbar-y]").HasAttribute("data-hovering"));
    }

    [Fact(DisplayName = "port: each part attaches its own handle with its part, the root as its reference and its axis")]
    [Trait("Requirement", "SCR-STR-01")]
    [Trait("Requirement", "SCR-STR-04")]
    [Trait("Requirement", "SCR-STR-05")]
    public void Each_part_attaches_its_own_handle()
    {
        test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.KeepMounted, true));

        Dictionary<string, int> counts = test.Attaches
            .GroupBy(json => JsonDocument.Parse(json).RootElement.GetProperty("part").GetString()!)
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(new Dictionary<string, int> { ["root"] = 1, ["viewport"] = 1, ["content"] = 1, ["scrollbar"] = 2, ["thumb"] = 2 }, counts);
        Assert.False(test.AttachesOf("root")[0].TryGetProperty("rootRef", out _));
        Assert.Equal(["vertical", "horizontal"], test.AttachesOf("scrollbar").Select(o => o.GetProperty("orientation").GetString()).Order(StringComparer.Ordinal).Reverse().ToArray());
        foreach (JSRuntimeInvocation invocation in test.Module.Invocations["attach"])
        {
            string part = JsonDocument.Parse((string)invocation.Arguments[1]!).RootElement.GetProperty("part").GetString()!;
            object[]? references = invocation.Arguments.Count > 3 ? invocation.Arguments[3] as object[] : null;
            Assert.Equal(part == "root" ? 0 : 1, references?.Length ?? 0);
        }
    }

    [Fact(DisplayName = "port: a part freezes the style it rendered at the attach and sends its own declarations and the consumer's")]
    [Trait("Requirement", "SCR-DOM-02")]
    [Trait("Requirement", "SCR-DOM-20")]
    [Trait("Requirement", "SCR-DOM-07")]
    public void A_part_freezes_its_rendered_style_and_sends_its_declarations_and_the_consumers()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.ViewportStyle, "height: 10px")
            .Add(x => x.ScrollbarStyle, "width: 10px"));

        // The C# → JS cells: the corner and thumb-size variables are rendered at 0px but never sent; the module owns them.
        Assert.Equal("position: relative", test.AttachesOf("root")[0].GetProperty("style").GetString());
        Assert.Equal("overflow: scroll; height: 10px;", test.AttachesOf("viewport")[0].GetProperty("style").GetString());
        Assert.DoesNotContain("--scroll-area-thumb", test.AttachesOf("scrollbar")[0].GetProperty("style").GetString());

        cut.Render(p => p.Add(x => x.ViewportStyle, "height: 20px"));

        Assert.Equal("overflow: scroll; height: 10px;", cut.Find("[data-testid=viewport]").GetAttribute("style"));
        Assert.Equal("overflow: scroll; height: 20px;", test.UpdatesOf("viewport")[^1].GetProperty("style").GetString());
        Assert.Contains("--scroll-area-thumb-height: 0px", cut.Find("[data-testid=scrollbar-y]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the content's handle sends no style, which stays C#'s")]
    [Trait("Requirement", "SCR-DOM-13")]
    public void The_contents_handle_sends_no_style()
    {
        IRenderedComponent<ScrollAreaRoot> cut = test.Context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaViewport>(v => v
                .AddChildContent<ScrollAreaContent>(c => c.AddUnmatched("style", "padding: 4px").AddUnmatched("data-testid", "content"))));

        Assert.False(Assert.Single(test.AttachesOf("content")).TryGetProperty("style", out _));
        Assert.Equal("min-width: fit-content; padding: 4px;", cut.Find("[data-testid=content]").GetAttribute("style"));
    }

    [Fact(DisplayName = "supports a custom scrollbar renderer that does not forward its ref")]
    [Trait("Requirement", "SCR-EDGE-10")]
    public void Supports_a_custom_scrollbar_renderer_that_does_not_forward_its_ref()
    {
        // The port's analogue of a renderer without a ref: a substitute callback, which leaves the part without an element,
        // still renders the merged attributes; its handle never attaches, so the part is inert.
        IRenderedComponent<ScrollAreaRoot> cut = test.Context.Render<ScrollAreaRoot>(p => p.AddChildContent<ScrollAreaScrollbar>(s => s
            .Add(x => x.KeepMounted, true)
            .Add(x => x.RenderFunction, props => builder =>
            {
                builder.OpenElement(0, "section");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            })));

        IElement scrollbar = cut.Find("section");
        Assert.Equal("vertical", scrollbar.GetAttribute("data-orientation"));
        Assert.Equal("true", scrollbar.GetAttribute("aria-hidden"));
        Assert.Empty(test.AttachesOf("scrollbar"));
    }

    [Fact(DisplayName = "supports a custom content renderer that does not forward its ref")]
    [Trait("Requirement", "SCR-EDGE-10")]
    public void Supports_a_custom_content_renderer_that_does_not_forward_its_ref()
    {
        IRenderedComponent<ScrollAreaRoot> cut = test.Context.Render<ScrollAreaRoot>(p => p.AddChildContent<ScrollAreaViewport>(v => v
            .AddChildContent<ScrollAreaContent>(c => c.Add(x => x.RenderFunction, props => builder =>
            {
                builder.OpenElement(0, "section");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            }))));

        Assert.Equal("presentation", cut.Find("section").GetAttribute("role"));
        Assert.Empty(test.AttachesOf("content"));
    }
}
