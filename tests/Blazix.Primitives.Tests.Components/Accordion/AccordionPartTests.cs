using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>AccordionItem.test.tsx</c>, <c>AccordionHeader.test.tsx</c>, <c>AccordionTrigger.test.tsx</c>
/// and <c>AccordionPanel.test.tsx</c> (ACC §13), titles verbatim. The context errors read the port's words (ACC-EDGE-01/02,
/// the PRG-EDGE-01 precedent); <c>toBeVisible()</c> reads as "rendered and not <c>hidden</c>"; the SSR title runs on the first
/// render (ADR 06 Amendments 2026-10-18 rule 3); the warnings run through the development warnings switch.
/// </summary>
public sealed class AccordionPartTests : IDisposable
{
    private static readonly RenderFragment s_span = builder =>
    {
        builder.OpenElement(0, "span");
        builder.CloseElement();
    };

    private readonly AccordionTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "throws when rendered outside an Accordion.Root")]
    [Trait("Requirement", "ACC-EDGE-01")]
    [Trait("Requirement", "ACC-STR-02")]
    public void Throws_when_rendered_outside_an_Accordion_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<AccordionItem<string>>());

        Assert.Equal("Blazix: The `AccordionRoot` context is missing. Accordion parts must be placed within `<AccordionRoot>`.", error.Message);
    }

    [Fact(DisplayName = "does not report hidden=true after the item has started opening")]
    [Trait("Requirement", "ACC-STA-10")]
    [Trait("Requirement", "ACC-STA-02")]
    public void Does_not_report_hidden_true_after_the_item_has_started_opening()
    {
        List<AccordionItemState<string>> states = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoGeneratedItems, p => p.Add(
            x => x.ItemRender,
            (RenderFragment<RenderProps<AccordionItemState<string>>>)(props => builder =>
            {
                states.Add(props.State);
                builder.OpenElement(0, "div");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.AddContent(2, props.ChildContent);
                builder.CloseElement();
            })));
        Assert.Contains(states, state => !state.Open && state.Hidden);

        cut.FindAll("button")[0].Click();

        Assert.Contains(states, state => state.Open);
        Assert.DoesNotContain(states, state => state.Open && state.Hidden);
    }

    [Fact(DisplayName = "throws when rendered outside an Accordion.Item")]
    [Trait("Requirement", "ACC-EDGE-02")]
    public void Throws_when_rendered_outside_an_Accordion_Item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<AccordionHeader<string>>());

        Assert.Equal("Blazix: The `AccordionItem` context is missing. Accordion parts must be placed within `<AccordionItem>`.", error.Message);
    }

    [Fact(DisplayName = "renders the disabled attribute when disabled")]
    [Trait("Requirement", "ACC-DOM-12")]
    public void Renders_the_disabled_attribute_when_disabled()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.ItemDisabled, true));
        IElement trigger = cut.Find("button");

        Assert.True(trigger.HasAttribute("disabled"));
        Assert.False(trigger.HasAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "removes a disabled non-native trigger from the tab order")]
    [Trait("Requirement", "ACC-DOM-12")]
    [Trait("Requirement", "ACC-STR-06")]
    public void Removes_a_disabled_non_native_trigger_from_the_tab_order()
    {
        // An item without a panel (ACC-STR-06); the Tab clause runs at L4.
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.ItemDisabled, true)
            .Add(x => x.PanelMounted, false)
            .Add(x => x.NativeButton, false)
            .Add(x => x.TriggerRender, s_span));
        IElement trigger = cut.Find("[role=button]");

        Assert.Equal("true", trigger.GetAttribute("aria-disabled"));
        Assert.Equal("-1", trigger.GetAttribute("tabindex"));
        Assert.False(trigger.HasAttribute("disabled"));
    }

    [Fact(DisplayName = "keeps a non-native trigger tabbable")]
    [Trait("Requirement", "ACC-DOM-12")]
    public void Keeps_a_non_native_trigger_tabbable()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.TriggerRender, s_span));

        Assert.Equal("0", cut.Find("[role=button]").GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "does not render the root value as data-value")]
    [Trait("Requirement", "ACC-DOM-09")]
    public void Does_not_render_the_root_value_as_data_value()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoStringItems, p => p
            .Add(x => x.ControlledStrings, ["one", "two"])
            .Add(x => x.Multiple, true));

        // Both items are open, so their state carries the value, and no part renders it.
        Assert.All(cut.FindAll("button"), trigger => Assert.True(trigger.HasAttribute("data-panel-open")));
        Assert.Empty(cut.FindAll("[data-value]"));
    }

    // ACC-STA-13, ACC-DOM-05: the index is the item's position in the order the registration-only list reports (track 1's
    // E1, ADR 04 Amendments 2026-10-26), -1 until the first report; the report is driven here as the module sends it.
    [Fact(DisplayName = "renders data-index on every trigger including the first")]
    [Trait("Requirement", "ACC-DOM-05")]
    [Trait("Requirement", "ACC-STA-13")]
    public void Renders_data_index_on_every_trigger_including_the_first()
    {
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .AddChildContent<AccordionItem<string>>(item => item.AddChildContent<AccordionHeader<string>>(header => header.AddChildContent<AccordionTrigger<string>>(trigger => trigger.AddChildContent("first"))))
            .AddChildContent<AccordionItem<string>>(item => item.AddChildContent<AccordionHeader<string>>(header => header.AddChildContent<AccordionTrigger<string>>(trigger => trigger.AddChildContent("second")))));
        Assert.All(cut.FindAll("button"), trigger => Assert.Equal("-1", trigger.GetAttribute("data-index")));
        Assert.Contains("\"registrationOnly\":true", host.AttachOptions(host.CompositeModule)[0], StringComparison.Ordinal);
        string[] ids = [.. cut.FindAll("div > div").Select(item => item.Id!)];

        cut.InvokeAsync(() => host.ListTwin().OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}"]}"""));

        IReadOnlyList<IElement> triggers = cut.FindAll("button");
        Assert.Equal("0", triggers[0].GetAttribute("data-index"));
        Assert.Equal("1", triggers[1].GetAttribute("data-index"));
        Assert.Equal("0", cut.FindAll("h3")[0].GetAttribute("data-index"));
        Assert.Equal("1", cut.Find($"#{ids[1]}").GetAttribute("data-index"));
    }

    [Fact(DisplayName = "port: a reordered report moves the indexes, and an item outside a root's list keeps -1")]
    [Trait("Requirement", "ACC-STA-13")]
    [Trait("Requirement", "ACC-STR-07")]
    public void A_reordered_report_moves_the_indexes()
    {
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .AddChildContent<AccordionItem<string>>(item => item.AddUnmatched("id", "a").AddChildContent<AccordionTrigger<string>>())
            .AddChildContent<AccordionItem<string>>(item => item.AddUnmatched("id", "b").AddChildContent<AccordionTrigger<string>>()));

        cut.InvokeAsync(() => host.ListTwin().OnItemsChange("""{"ids":["b","a"]}"""));
        Assert.Equal("1", cut.Find("#a").GetAttribute("data-index"));
        Assert.Equal("0", cut.Find("#b").GetAttribute("data-index"));

        cut.InvokeAsync(() => host.ListTwin().OnItemsChange("""{"ids":["a"]}"""));
        Assert.Equal("0", cut.Find("#a").GetAttribute("data-index"));
        Assert.Equal("-1", cut.Find("#b").GetAttribute("data-index"));
    }

    [Fact(DisplayName = "keeps data-panel-open instead of data-open")]
    [Trait("Requirement", "ACC-DOM-11")]
    public void Keeps_data_panel_open_instead_of_data_open()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoStringItems, p => p.Add(x => x.ControlledStrings, ["one"]));
        IElement trigger = cut.FindAll("button")[0];

        Assert.True(trigger.HasAttribute("data-panel-open"));
        Assert.False(trigger.HasAttribute("data-open"));
        Assert.False(trigger.HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "warns when a panel enables hiddenUntilFound and disables keepMounted")]
    [Trait("Requirement", "ACC-EDGE-05")]
    [Trait("Requirement", "ACC-DOM-15")]
    public void Warns_when_a_panel_enables_hiddenUntilFound_and_disables_keepMounted()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.PanelHiddenUntilFound, true)
            .Add(x => x.PanelKeepMounted, false));
        cut.Render();

        (LogLevel level, string message) = Assert.Single(host.PanelLogger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "Blazix: The `KeepMounted` parameter set to `false` on an `AccordionPanel` is ignored when `HiddenUntilFound` is enabled on the panel or root, since the panel must remain mounted while closed.",
            message);
        // The closed panel stays in the document, hidden; the module writes the `until-found` literal (L4).
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "port: a root keepMounted={false} alone does not log the panel warning")]
    [Trait("Requirement", "ACC-EDGE-05")]
    public void A_root_keepMounted_false_alone_does_not_log_the_panel_warning()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.KeepMounted, false)
            .Add(x => x.PanelHiddenUntilFound, true));
        cut.Render();

        Assert.Empty(host.PanelLogger.Entries);
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "suppresses the initial keyframe animation from inline styles when rendered open")]
    [Trait("Requirement", "ACC-ANI-03")]
    public void Suppresses_the_initial_keyframe_animation_from_inline_styles_when_rendered_open()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.PanelInlineStyle, "animation-duration: 100ms; animation-name: panel-slide-down; animation-timing-function: linear"));

        IReadOnlyDictionary<string, string> style = Declarations(cut.Find("[data-testid=panel1]").GetAttribute("style"));
        Assert.Equal("none", style["animation-name"]);
        Assert.Equal("100ms", style["animation-duration"]);
        Assert.Equal("auto", style["--accordion-panel-height"]);
    }

    [Fact(DisplayName = "passes root keepMounted to closed panels")]
    [Trait("Requirement", "ACC-API-07")]
    [Trait("Requirement", "ACC-API-16")]
    [Trait("Requirement", "ACC-DOM-15")]
    public void Passes_root_keepMounted_to_closed_panels()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.KeepMounted, true));

        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "passes root hiddenUntilFound to closed panels and allows panel overrides")]
    [Trait("Requirement", "ACC-API-06")]
    [Trait("Requirement", "ACC-API-17")]
    [Trait("Requirement", "ACC-DOM-15")]
    [Trait("Requirement", "ACC-DOM-16")]
    public void Passes_root_hiddenUntilFound_to_closed_panels_and_allows_panel_overrides()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.HiddenUntilFound, true)
            .Add(x => x.KeepMounted, true)
            .Add(x => x.SecondPanelHiddenUntilFound, false)
            .Add(x => x.SecondPanelKeepMounted, false));

        // The until-found literal is the module's (L4); C# renders the boolean and the persisted starting style.
        IElement first = cut.Find("[data-testid=panel1]");
        Assert.True(first.HasAttribute("hidden"));
        Assert.True(first.HasAttribute("data-starting-style"));
        Assert.Contains("\"hiddenUntilFound\":true", host.AttachOptions(host.CollapsibleModule)[0], StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=panel2]"));
    }

    private static Dictionary<string, string> Declarations(string? style)
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
