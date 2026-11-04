using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

// The rows of docs/components/navigation-menu.md whose Base UI source has no test (`Test: none`), each given a port title
// where one can fail (the review of PR 168, fix 7); the rows' dated notes name these titles.
/// <summary>Port titles for the NavigationMenu requirements no Base UI test covers.</summary>
public sealed class NavigationMenuPortRowsTests : IDisposable
{
    private const string PositioningPath = "./_content/Blazix.Primitives/js/positioning.js";
    private const string CompositePath = "./_content/Blazix.Primitives/js/composite.js";

    private readonly NavigationMenuTestContext test = new();
    private readonly CapturingLogger<NavigationMenuRoot<string>> rootLogger = new();

    public NavigationMenuPortRowsTests()
    {
        test.Context.Services.AddSingleton<ILogger<NavigationMenuRoot<string>>>(rootLogger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the Popup's generated id has the base-ui- prefix, and only the active Trigger's aria-controls names it")]
    [Trait("Requirement", "NAV-API-17")]
    [Trait("Requirement", "NAV-ARIA-02")]
    public async Task The_popups_id_is_named_only_by_the_active_triggers_aria_controls()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true).Add(x => x.NestedDefaultValue, "nested-1"));
        Assert.False(Element(cut, "trigger-item-1").HasAttribute("aria-controls"));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        string popupId = Element(cut, "popup").Id!;
        Assert.StartsWith("base-ui-", popupId, StringComparison.Ordinal);
        Assert.Equal(popupId, Element(cut, "trigger-item-1").GetAttribute("aria-controls"));
        Assert.False(Element(cut, "trigger-item-2").HasAttribute("aria-controls"));

        // The inline nested Root registers no Popup, so its active Trigger names none.
        Assert.Equal("true", Element(cut, "nested-trigger-1").GetAttribute("aria-expanded"));
        Assert.False(Element(cut, "nested-trigger-1").HasAttribute("aria-controls"));
    }

    // ADR 08's generated ids (PartIdGenerator): the port's Viewport id carries the `base-ui-` prefix, which Base UI's
    // useId leaves off; the owner span is what reads it.
    [Fact(DisplayName = "port: the Viewport's generated id has the base-ui- prefix, and the active Trigger's owner span names it in aria-owns")]
    [Trait("Requirement", "NAV-API-18")]
    [Trait("Requirement", "NAV-ARIA-06")]
    public async Task The_viewports_id_is_named_by_the_owner_span()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        await test.PressAsync(cut, "trigger-item-1", open: true);

        string viewportId = Element(cut, "viewport").Id!;
        Assert.StartsWith("base-ui-", viewportId, StringComparison.Ordinal);
        IElement owner = Assert.Single(cut.FindAll("[aria-owns]"));
        Assert.Equal(viewportId, owner.GetAttribute("aria-owns"));
        Assert.Same(Element(cut, "trigger-item-1").ParentElement, owner.ParentElement);
    }

    [Fact(DisplayName = "port: a Trigger renders aria-disabled true while disabled and false while enabled, and never the disabled attribute")]
    [Trait("Requirement", "NAV-ARIA-03")]
    public void A_trigger_renders_aria_disabled_and_never_disabled()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.DisabledValue, "item-1"));

        Assert.Equal("true", Element(cut, "trigger-item-1").GetAttribute("aria-disabled"));
        Assert.Equal("false", Element(cut, "trigger-item-2").GetAttribute("aria-disabled"));
        Assert.False(Element(cut, "trigger-item-1").HasAttribute("disabled"));
        Assert.False(Element(cut, "trigger-item-2").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: the Icon, the Arrow and the active Trigger's guards are aria-hidden")]
    [Trait("Requirement", "NAV-ARIA-05")]
    public async Task The_icon_the_arrow_and_the_guards_are_aria_hidden()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.WithArrow, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.Equal("true", Element(cut, "icon-item-1").GetAttribute("aria-hidden"));
        Assert.Equal("true", Element(cut, "arrow").GetAttribute("aria-hidden"));
        IReadOnlyList<IElement> guards = cut.FindAll("[data-base-ui-focus-guard]");
        Assert.NotEmpty(guards);
        Assert.All(guards, guard => Assert.Equal("true", guard.GetAttribute("aria-hidden")));
    }

    [Fact(DisplayName = "port: the Positioner and the Backdrop are role presentation, and the Popup and the Content have no role")]
    [Trait("Requirement", "NAV-ARIA-08")]
    public async Task The_positioner_and_the_backdrop_are_presentation()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.WithBackdrop, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.Equal("presentation", Element(cut, "positioner").GetAttribute("role"));
        Assert.Equal("presentation", Element(cut, "backdrop").GetAttribute("role"));
        Assert.Equal("NAV", Element(cut, "popup").TagName);
        Assert.False(Element(cut, "popup").HasAttribute("role"));
        Assert.False(Element(cut, "content-item-1").HasAttribute("role"));
    }

    [Fact(DisplayName = "port: no part renders aria-haspopup, aria-labelledby or aria-describedby")]
    [Trait("Requirement", "NAV-ARIA-09")]
    public async Task No_part_renders_haspopup_labelledby_or_describedby()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.WithBackdrop, true).Add(x => x.WithArrow, true).Add(x => x.Nested, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.Empty(cut.FindAll("[aria-haspopup], [aria-labelledby], [aria-describedby]"));
    }

    [Fact(DisplayName = "port: the Root is a nav with data-open while open, and a nested Root is a div with data-nested")]
    [Trait("Requirement", "NAV-DOM-01")]
    public async Task The_root_maps_open_and_nested_by_the_default_rule()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true).Add(x => x.NestedDefaultValue, "nested-1"));
        IElement root = Element(cut, "root");
        Assert.Equal("NAV", root.TagName);
        Assert.False(root.HasAttribute("data-open"));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.True(Element(cut, "root").HasAttribute("data-open"));
        Assert.False(Element(cut, "root").HasAttribute("data-nested"));
        IElement nested = Element(cut, "nested-root");
        Assert.Equal("DIV", nested.TagName);
        Assert.True(nested.HasAttribute("data-nested"));
        Assert.True(nested.HasAttribute("data-open"));
    }

    [Fact(DisplayName = "port: a kept Backdrop is hidden while the popup is not mounted, and always has user-select none")]
    [Trait("Requirement", "NAV-DOM-24")]
    public async Task A_kept_backdrop_is_hidden_while_unmounted()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.WithBackdrop, true).Add(x => x.KeepMountedPortal, true));
        IElement closed = Element(cut, "backdrop");
        Assert.True(closed.HasAttribute("hidden"));
        Assert.Contains("user-select: none", closed.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("-webkit-user-select: none", closed.GetAttribute("style"), StringComparison.Ordinal);

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement open = Element(cut, "backdrop");
        Assert.False(open.HasAttribute("hidden"));
        Assert.Contains("user-select: none", open.GetAttribute("style"), StringComparison.Ordinal);
    }

    // The style after the handover is the positioning module's: the L4 title "port: the Positioner has pointer-events none
    // while the menu is not open" follows it.
    [Fact(DisplayName = "port: a kept Positioner is hidden while the popup is not mounted, and its first style has pointer-events none while closed")]
    [Trait("Requirement", "NAV-DOM-26")]
    public async Task A_kept_positioner_is_hidden_while_unmounted()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.KeepMountedPortal, true));
        IElement closed = Element(cut, "positioner");
        Assert.True(closed.HasAttribute("hidden"));
        Assert.Contains("pointer-events: none", closed.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);

        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.False(Element(cut, "positioner").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "port: an inline Viewport renders inert while viewportInert holds, and a Viewport in a Positioner never does")]
    [Trait("Requirement", "NAV-DOM-35")]
    [Trait("Requirement", "NAV-STA-09")]
    public async Task An_inline_viewport_renders_inert_while_viewport_inert_holds()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true).Add(x => x.NestedDefaultValue, "nested-1"));
        await test.PressAsync(cut, "trigger-item-1", open: true);
        NavigationMenuViewport[] viewports = cut.FindComponents<NavigationMenuViewport>().Select(viewport => viewport.Instance).ToArray();
        NavigationMenuViewport inline = viewports.Single(viewport => (string?)viewport.AdditionalAttributes?["data-testid"] == "nested-viewport");
        NavigationMenuViewport positioned = viewports.Single(viewport => (string?)viewport.AdditionalAttributes?["data-testid"] == "viewport");

        await cut.InvokeAsync(() => inline.ModuleTwin.OnViewportInertChange("{\"inert\":true}"));
        await cut.InvokeAsync(() => positioned.ModuleTwin.OnViewportInertChange("{\"inert\":true}"));

        Assert.True(Element(cut, "nested-viewport").HasAttribute("inert"));
        Assert.False(Element(cut, "viewport").HasAttribute("inert"));

        await cut.InvokeAsync(() => inline.ModuleTwin.OnViewportInertChange("{\"inert\":false}"));

        Assert.False(Element(cut, "nested-viewport").HasAttribute("inert"));
    }

    [Fact(DisplayName = "port: the List and the Content leave Home and End to the browser (no composite group enables them)")]
    [Trait("Requirement", "NAV-KEY-07")]
    public async Task The_list_and_the_content_leave_home_and_end_alone()
    {
        BunitJSModuleInterop composite = test.Context.JSInterop.SetupModule(CompositePath);
        composite.SetupModule("attach", _ => true);
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);

        string[] roots = composite.Invocations["attach"]
            .Select(attach => attach.Arguments[1] as string)
            .OfType<string>()
            .Where(json => json.Contains("\"enableHomeAndEndKeys\":", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(roots);
        Assert.All(roots, json => Assert.Contains("\"enableHomeAndEndKeys\":false", json, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the Positioner always asks for the adaptive origin, and a top-level Root never falls back to the perpendicular axis")]
    [Trait("Requirement", "NAV-POS-04")]
    [Trait("Requirement", "NAV-POS-05")]
    public async Task The_positioner_asks_for_the_adaptive_origin_and_the_top_level_fallback()
    {
        BunitJSModuleInterop positioning = test.Context.JSInterop.SetupModule(PositioningPath);
        positioning.SetupModule("attach", _ => true);
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);

        string options = positioning.Invocations["attach"]
            .Select(attach => attach.Arguments[1] as string)
            .OfType<string>()
            .Last();
        Assert.Contains("\"adaptiveOrigin\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"fallbackAxisSide\":\"none\"", options, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: Items with identical values are all active while that value is open")]
    [Trait("Requirement", "NAV-EDGE-07")]
    public async Task Items_with_identical_values_are_all_active()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Values, new[] { "item-1", "item-1" }));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IReadOnlyList<IElement> triggers = cut.FindAll("[data-testid=\"trigger-item-1\"]");
        Assert.Equal(2, triggers.Count);
        Assert.All(triggers, trigger => Assert.Equal("true", trigger.GetAttribute("aria-expanded")));
    }

    [Fact(DisplayName = "port: a value that stops being supplied logs the controlled-state error once, with the name NavigationMenu")]
    [Trait("Requirement", "NAV-EDGE-09")]
    public void A_value_that_stops_being_supplied_logs_the_controlled_state_error_once()
    {
        RenderFragment content = static builder => builder.AddContent(0, "menu");
        IRenderedComponent<NavigationMenuRoot<string>> cut = test.Context.Render<NavigationMenuRoot<string>>(p => p
            .Add(x => x.Value, "item-1")
            .Add(x => x.ChildContent, content));

        cut.Render(p => p.Add(x => x.ChildContent, content));
        cut.Render(p => p.Add(x => x.ChildContent, content));

        (LogLevel _, string message) = Assert.Single(rootLogger.Entries);
        Assert.StartsWith("A component is changing the controlled value state of NavigationMenu to be uncontrolled.\n", message, StringComparison.Ordinal);
    }

    private static IElement Element(IRenderedComponent<NavigationMenuHarness> cut, string testId) => cut.Find($"[data-testid=\"{testId}\"]");
}
