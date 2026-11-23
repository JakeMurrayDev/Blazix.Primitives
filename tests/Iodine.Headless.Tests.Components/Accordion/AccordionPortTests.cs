using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The port's own Accordion titles: the rows whose Base UI evidence is <c>Test: none</c> (ACC-STR-07, ACC-API-08/09,
/// ACC-DOM-01/02/03/07/08/10, ACC-EVT-06/08/09, ACC-ANI-05, ACC-EDGE-03/09/13/14), the twin wiring of the panel and the
/// trigger, the reveal through the item's chain (settled by the orchestrator 2026-10-25, M3 plan: open, cancel in the item
/// callback, cancel in <c>OnValueChange</c>, the single-mode close of the other item), design point D2's three ownership titles
/// and design point D4's render counts.
/// </summary>
public sealed class AccordionPortTests : IDisposable
{
    private const string BeforeMatchJson = """
        {"kind":"Event","type":"beforematch","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-5","tagName":"DIV"},"defaultPrevented":false,"timeStamp":12.5}
        """;

    private readonly AccordionTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "port: the root renders data-disabled and data-orientation and no role, aria, dir, data-value or open attribute")]
    [Trait("Requirement", "ACC-DOM-01")]
    [Trait("Requirement", "ACC-DOM-02")]
    [Trait("Requirement", "ACC-DOM-03")]
    [Trait("Requirement", "ACC-API-08")]
    [Trait("Requirement", "ACC-API-09")]
    [Trait("Requirement", "ACC-I18N-01")]
    public void The_root_renders_its_two_data_attributes_and_nothing_else_of_its_own()
    {
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .Add(x => x.DefaultValue, ["a"])
            .Add(x => x.Disabled, true)
            .Add(x => x.LoopFocus, true)
            .AddChildContent<AccordionItem<string>>(item => item.Add(x => x.Value, "a")));
        IElement root = cut.Find("div");

        // bUnit's element-reference capture is not an attribute the part renders.
        Assert.Equal(["data-disabled", "data-orientation"], root.Attributes.Select(attribute => attribute.Name).Where(name => !name.StartsWith("blazor:", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Equal("vertical", root.GetAttribute("data-orientation"));

        IRenderedComponent<AccordionRoot<string>> horizontal = Context.Render<AccordionRoot<string>>(p => p
            .Add(x => x.Orientation, Blazix.Primitives.Orientation.Horizontal)
            .AddChildContent<AccordionItem<string>>(item => item.Add(x => x.Value, "a")));
        Assert.Equal("horizontal", horizontal.Find("div").GetAttribute("data-orientation"));
        Assert.False(horizontal.Find("div").HasAttribute("data-disabled"));
        // ACC-DOM-07: the item reflects the root's orientation.
        Assert.Equal("horizontal", horizontal.Find("div > div").GetAttribute("data-orientation"));
    }

    [Fact(DisplayName = "port: the item parts render data-orientation and data-hidden, and only the panel carries transition attributes")]
    [Trait("Requirement", "ACC-DOM-07")]
    [Trait("Requirement", "ACC-DOM-08")]
    [Trait("Requirement", "ACC-DOM-10")]
    [Trait("Requirement", "ACC-DOM-05")]
    public void The_item_parts_render_orientation_and_hidden_and_no_transition_attributes()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.KeepMounted, true));
        IElement[] parts = [cut.Find("[data-testid=item1]"), cut.Find("h3"), cut.Find("button"), cut.Find("[data-testid=panel1]")];

        Assert.All(parts, part => Assert.Equal("vertical", part.GetAttribute("data-orientation")));
        Assert.All(parts, part => Assert.True(part.HasAttribute("data-hidden")));
        // The index is -1 until the registry reports it (ACC-STA-13; track 1's registration-only list).
        Assert.All(parts, part => Assert.Equal("-1", part.GetAttribute("data-index")));

        cut.Find("button").Click();

        parts = [cut.Find("[data-testid=item1]"), cut.Find("h3"), cut.Find("button"), cut.Find("[data-testid=panel1]")];
        Assert.All(parts, part => Assert.False(part.HasAttribute("data-hidden")));
        Assert.All(parts[..3], part => Assert.False(part.HasAttribute("data-starting-style") || part.HasAttribute("data-ending-style")));
    }

    [Fact(DisplayName = "port: the panel attaches the collapsible module with the accordion variables and the status report")]
    [Trait("Requirement", "ACC-DOM-17")]
    [Trait("Requirement", "ACC-STA-03")]
    [Trait("Requirement", "ACC-ANI-01")]
    public void The_panel_attaches_the_collapsible_module_with_the_accordion_options()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.PanelInlineStyle, "color: red"));
        string panelId = cut.Find("[data-testid=panel1]").Id!;

        Assert.Equal(
            $$"""{"open":true,"mounted":true,"status":"idle","keepMounted":false,"hiddenUntilFound":false,"suppressOpenAnimation":true,"panelId":"{{panelId}}","openSelector":"[data-open]","heightVariable":"--accordion-panel-height","widthVariable":"--accordion-panel-width","reportStatus":true,"style":"color: red"}""",
            Assert.Single(host.AttachOptions(host.CollapsibleModule)));
        Assert.Equal("--accordion-panel-height: auto; --accordion-panel-width: auto; color: red; animation-name: none;", cut.Find("[data-testid=panel1]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the trigger attaches the button module as a non-composite button with its effective disabled state")]
    [Trait("Requirement", "ACC-DOM-12")]
    [Trait("Requirement", "ACC-API-14")]
    public void The_trigger_attaches_the_button_module_as_a_non_composite_button()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.ItemDisabled, true).Add(x => x.TriggerDisabled, false));

        string options = Assert.IsType<string>(Assert.Single(host.AttachOptions(host.ButtonModule)));
        Assert.Contains("\"disabled\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"composite\":false", options, StringComparison.Ordinal);
        // ACC-API-14: the trigger's own Disabled only adds, and never reaches the state.
        IRenderedComponent<AccordionScenarios> own = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.TriggerDisabled, true));
        Assert.True(own.Find("button").HasAttribute("disabled"));
        Assert.False(own.Find("button").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "port: the panel's status report reaches the item's shared state, and the panel's state after an exit is unset")]
    [Trait("Requirement", "ACC-STA-03")]
    [Trait("Requirement", "ACC-STA-09")]
    public void The_panel_status_report_reaches_the_item_shared_state()
    {
        AccordionPanelState<int>? seen = null;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.PanelStyle, state =>
        {
            seen = state;
            return null;
        }));
        cut.Find("button").Click();
        Assert.Equal(TransitionStatus.Starting, seen!.TransitionStatus);
        CollapsibleTwin twin = host.PanelTwins()[0];

        cut.InvokeAsync(() => twin.OnPanelStatusChange("""{"status":"idle"}"""));
        Assert.Equal(TransitionStatus.Idle, seen!.TransitionStatus);

        cut.Find("button").Click();
        cut.InvokeAsync(() => twin.OnPanelStatusChange("""{"status":"ending"}"""));
        Assert.Equal(TransitionStatus.Ending, seen!.TransitionStatus);
        cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Null(cut.FindComponent<AccordionItem<int>>().Instance.Context.Collapsible.State.TransitionStatus);
    }

    [Fact(DisplayName = "port: a reveal opens the item through the root value and closes the other item in single mode")]
    [Trait("Requirement", "ACC-EVT-06")]
    [Trait("Requirement", "ACC-ANI-05")]
    [Trait("Requirement", "ACC-EVT-08")]
    public async Task A_reveal_opens_the_item_through_the_root_value()
    {
        List<ValueChangeEventArgs<bool>> openChanges = [];
        List<ValueChangeEventArgs<IReadOnlyList<int>>> valueChanges = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.HiddenUntilFound, true)
            .Add(x => x.OnOpenChange, args => openChanges.Add(args))
            .Add(x => x.OnValueChange, args => valueChanges.Add(args)));
        CollapsibleTwin second = host.PanelTwins()[1];

        Assert.Equal("run", await cut.InvokeAsync(() => second.Decide(CollapsibleTwin.RevealPoint, BeforeMatchJson)));

        ValueChangeEventArgs<bool> open = Assert.Single(openChanges);
        Assert.True(open.Value);
        Assert.Equal(EventReason.None, open.Details.Reason);
        Assert.Equal("beforematch", open.Details.Event!.Type);
        // ACC-EVT-08: no trigger element; EVD-EVT-04: one details object for both callbacks.
        Assert.Null(open.Details.Trigger);
        ValueChangeEventArgs<IReadOnlyList<int>> value = Assert.Single(valueChanges);
        Assert.Equal([1], value.Value);
        Assert.Same(open.Details, value.Details);
        Assert.True(cut.Find("[data-testid=panel2]").HasAttribute("data-open"));
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-closed"));
    }

    [Theory(DisplayName = "port: a reveal canceled in either callback answers stop and leaves the value")]
    [InlineData("item")]
    [InlineData("root")]
    [Trait("Requirement", "ACC-EVT-06")]
    [Trait("Requirement", "ACC-ANI-05")]
    public async Task A_reveal_canceled_in_either_callback_answers_stop(string where)
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.HiddenUntilFound, true)
            .Add(x => x.OnOpenChange, args =>
            {
                if (where == "item")
                {
                    args.Details.Cancel();
                }
            })
            .Add(x => x.OnValueChange, args =>
            {
                valueChanges++;
                if (where == "root")
                {
                    args.Details.Cancel();
                }
            }));
        CollapsibleTwin first = host.PanelTwins()[0];

        Assert.Equal("stop", await cut.InvokeAsync(() => first.Decide(CollapsibleTwin.RevealPoint, BeforeMatchJson)));
        Assert.False(await cut.InvokeAsync(() => first.OnRevealRequest($$"""{"event":{{BeforeMatchJson}}}""")));

        Assert.Equal(where == "item" ? 0 : 2, valueChanges);
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-closed"));
    }

    // Design point D2 (option (a), settled by the orchestrator 2026-10-26): the item's request re-applies its open state after the chain; the three
    // ownership shapes show that the re-apply changes nothing.
    [Fact(DisplayName = "port: with an uncontrolled root the item is open when its request returns")]
    [Trait("Requirement", "ACC-STA-07")]
    [Trait("Requirement", "ACC-EVT-03")]
    public async Task With_an_uncontrolled_root_the_item_is_open_when_its_request_returns()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem);
        CollapsibleRootContext collapsible = cut.FindComponent<AccordionItem<int>>().Instance.Context.Collapsible;

        bool applied = await cut.InvokeAsync(() => collapsible.RequestOpenAsync(true, new ChangeEventDetails(EventReason.TriggerPress)));

        Assert.True(applied);
        Assert.True(collapsible.State.Open);
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-open"));
    }

    [Fact(DisplayName = "port: with a controlled root that does not pass the value back the item stays closed after its request")]
    [Trait("Requirement", "ACC-STA-07")]
    [Trait("Requirement", "ACC-API-01")]
    public async Task With_a_controlled_root_that_does_not_pass_the_value_back_the_item_stays_closed()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.ControlledValue, [])
            .Add(x => x.HiddenUntilFound, true)
            .Add(x => x.OnValueChange, _ => valueChanges++));
        CollapsibleRootContext collapsible = cut.FindComponent<AccordionItem<int>>().Instance.Context.Collapsible;
        CollapsibleTwin twin = host.PanelTwins()[0];

        bool applied = await cut.InvokeAsync(() => collapsible.RequestOpenAsync(true, new ChangeEventDetails(EventReason.TriggerPress)));
        // The reveal is not canceled, so the decision lets it proceed; the Server answer reads the item still closed.
        string verdict = await cut.InvokeAsync(() => twin.Decide(CollapsibleTwin.RevealPoint, BeforeMatchJson));
        bool openAfterReveal = await cut.InvokeAsync(() => twin.OnRevealRequest($$"""{"event":{{BeforeMatchJson}}}"""));

        Assert.True(applied);
        Assert.Equal("run", verdict);
        Assert.False(openAfterReveal);
        Assert.False(collapsible.State.Open);
        Assert.Equal(3, valueChanges);
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "port: with @bind-Value the item opens once the binding passes the value back")]
    [Trait("Requirement", "ACC-API-01")]
    [Trait("Requirement", "ACC-STA-07")]
    public async Task With_bind_Value_the_item_opens_once_the_binding_passes_the_value_back()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.Bound);
        CollapsibleRootContext collapsible = cut.FindComponent<AccordionItem<int>>().Instance.Context.Collapsible;

        cut.Find("button").Click();

        Assert.Equal([0], cut.Instance.BoundValue);
        Assert.True(collapsible.State.Open);
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-open"));

        bool applied = await cut.InvokeAsync(() => collapsible.RequestOpenAsync(false, new ChangeEventDetails(EventReason.TriggerPress)));
        Assert.True(applied);
        Assert.Empty(cut.Instance.BoundValue);
        Assert.False(collapsible.State.Open);
    }

    [Fact(DisplayName = "port: the latest callbacks are the ones invoked")]
    [Trait("Requirement", "ACC-EVT-09")]
    public void The_latest_callbacks_are_the_ones_invoked()
    {
        List<string> calls = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.OnOpenChange, _ => calls.Add("item 1"))
            .Add(x => x.OnValueChange, _ => calls.Add("root 1")));

        cut.Render(p => p
            .Add(x => x.OnOpenChange, _ => calls.Add("item 2"))
            .Add(x => x.OnValueChange, _ => calls.Add("root 2")));
        cut.Find("button").Click();

        Assert.Equal(["item 2", "root 2"], calls);
    }

    [Fact(DisplayName = "port: a trigger outside an item and a panel without an item throw the collapsible message, a trigger in a collapsible root the item message")]
    [Trait("Requirement", "ACC-EDGE-03")]
    [Trait("Requirement", "ACC-EDGE-01")]
    public void Parts_outside_an_item_throw_by_their_lookup_order()
    {
        const string collapsibleMessage = "Blazix: The `CollapsibleRoot` context is missing. Collapsible parts must be placed within `<CollapsibleRoot>`.";

        Exception trigger = Assert.ThrowsAny<Exception>(() => Context.Render<AccordionRoot<string>>(p => p.AddChildContent<AccordionTrigger<string>>()));
        Exception panel = Assert.ThrowsAny<Exception>(() => Context.Render<AccordionRoot<string>>(p => p.AddChildContent<AccordionPanel<string>>()));
        Exception inCollapsible = Assert.ThrowsAny<Exception>(() => Context.Render<CollapsibleRoot>(p => p.AddChildContent<AccordionTrigger<string>>()));
        Exception panelAlone = Assert.ThrowsAny<Exception>(() => Context.Render<AccordionPanel<string>>());

        Assert.Equal(collapsibleMessage, trigger.GetBaseException().Message);
        Assert.Equal(collapsibleMessage, panel.GetBaseException().Message);
        Assert.Equal("Blazix: The `AccordionItem` context is missing. Accordion parts must be placed within `<AccordionItem>`.", inCollapsible.GetBaseException().Message);
        Assert.Equal("Blazix: The `AccordionRoot` context is missing. Accordion parts must be placed within `<AccordionRoot>`.", panelAlone.GetBaseException().Message);
    }

    [Fact(DisplayName = "port: an item without a value under a TValue other than string, or of another TValue than its root, throws")]
    [Trait("Requirement", "ACC-EDGE-09")]
    [Trait("Requirement", "ACC-API-10")]
    public void An_item_without_a_value_under_another_TValue_or_of_another_TValue_throws()
    {
        Exception missing = Assert.ThrowsAny<Exception>(() => Context.Render<AccordionRoot<int>>(p => p.AddChildContent<AccordionItem<int>>()));
        Exception mismatch = Assert.ThrowsAny<Exception>(() => Context.Render<AccordionRoot<int?>>(p => p.AddChildContent<AccordionItem<int>>(item => item.Add(x => x.Value, 1))));

        Assert.Equal(AccordionValues.MissingValueMessage, missing.GetBaseException().Message);
        Assert.Equal(
            "Blazix: An `AccordionItem` must have the `TValue` of its `AccordionRoot`. Set `TValue` on the part, or give `Value` the root's type, for example `Value=\"(int?)1\"` in an `AccordionRoot` of `int?`.",
            mismatch.GetBaseException().Message);
    }

    [Fact(DisplayName = "port: several triggers in one item share the generated id and all toggle the item")]
    [Trait("Requirement", "ACC-EDGE-13")]
    public void Several_triggers_in_one_item_share_the_generated_id()
    {
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p.AddChildContent<AccordionItem<string>>(item => item
            .AddChildContent<AccordionTrigger<string>>()
            .AddChildContent<AccordionTrigger<string>>()));
        IReadOnlyList<IElement> triggers = cut.FindAll("button");

        Assert.Equal(triggers[0].Id, triggers[1].Id);
        triggers[1].Click();
        Assert.All(cut.FindAll("button"), trigger => Assert.Equal("true", trigger.GetAttribute("aria-expanded")));
        cut.FindAll("button")[0].Click();
        Assert.All(cut.FindAll("button"), trigger => Assert.Equal("false", trigger.GetAttribute("aria-expanded")));
    }

    [Fact(DisplayName = "port: collapsible parts inside an item bind to it, through the item and root callbacks, with collapsible attributes")]
    [Trait("Requirement", "ACC-EDGE-14")]
    public void Collapsible_parts_inside_an_item_bind_to_it()
    {
        List<string> calls = [];
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<IReadOnlyList<string>> args) => calls.Add($"root {string.Join(',', args.Value)}"))
            .AddChildContent<AccordionItem<string>>(item => item
                .Add(x => x.Value, "a")
                .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) => calls.Add($"item {args.Value}"))
                .AddChildContent<CollapsibleTrigger>()
                .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("data-testid", "panel"))));

        cut.Find("button").Click();

        Assert.Equal(["item True", "root a"], calls);
        Assert.True(cut.Find("button").HasAttribute("data-panel-open"));
        IElement panel = cut.Find("[data-testid=panel]");
        Assert.True(panel.HasAttribute("data-open"));
        Assert.Contains("--collapsible-panel-height: auto", panel.GetAttribute("style"), StringComparison.Ordinal);
        Assert.False(panel.HasAttribute("role"));
    }

    // Design point D5 (option (a), settled by the orchestrator 2026-10-26): the opt-in lives on the item's collapsible state,
    // so a CollapsiblePanel inside an item reports its status too, and a CollapsibleTrigger there shows it.
    [Fact(DisplayName = "port: a collapsible panel inside an item reports its status, which a collapsible trigger there shows")]
    [Trait("Requirement", "ACC-EDGE-14")]
    public void A_collapsible_panel_inside_an_item_reports_its_status()
    {
        TransitionStatus? seen = null;
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .AddChildContent<AccordionItem<string>>(item => item
                .Add(x => x.Value, "a")
                .AddChildContent<CollapsibleTrigger>(trigger => trigger.Add(x => x.ClassValue, (Func<CollapsibleTriggerState, string?>)(state =>
                {
                    seen = state.TransitionStatus;
                    return null;
                })))
                .AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true))));
        Assert.Contains("\"reportStatus\":true", host.AttachOptions(host.CollapsibleModule)[0], StringComparison.Ordinal);
        cut.Find("button").Click();
        Assert.Equal(TransitionStatus.Starting, seen);

        cut.InvokeAsync(() => host.PanelTwins()[0].OnPanelStatusChange("""{"status":"idle"}"""));

        Assert.Equal(TransitionStatus.Idle, seen);
    }

    [Fact(DisplayName = "port: a collapsible panel outside an accordion does not report its status")]
    [Trait("Requirement", "ACC-EDGE-14")]
    public void A_collapsible_panel_outside_an_accordion_does_not_report_its_status()
    {
        Context.Render<CollapsibleRoot>(p => p.AddChildContent<CollapsiblePanel>(panel => panel.Add(x => x.KeepMounted, true)));

        // Collapsible's attach JSON is unchanged: the option is omitted.
        string options = Assert.IsType<string>(Assert.Single(host.AttachOptions(host.CollapsibleModule)));
        Assert.DoesNotContain("reportStatus", options, StringComparison.Ordinal);
        Assert.Contains("\"openSelector\":\"[data-open]\"", options, StringComparison.Ordinal);
    }

    // Design point D3 (option (a), settled by the orchestrator 2026-10-26; track 1's design point 2 (a)): an item of the
    // registration-only list is resolved by its element id, so the item renders the consumer's id, else one drawn after Base
    // UI's three ids (fallback value, panel, trigger), which keep their order, so prerender equals the interactive render.
    [Fact(DisplayName = "port: an item renders the consumer's id, else one drawn after its three Base UI ids")]
    [Trait("Requirement", "ACC-STR-02")]
    [Trait("Requirement", "ACC-ARIA-06")]
    public void An_item_renders_the_consumer_id_else_one_drawn_after_its_three_ids()
    {
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .Add(x => x.DefaultValue, ["b"])
            .AddChildContent<AccordionItem<string>>(item => item
                .Add(x => x.Value, "a")
                .AddUnmatched("data-testid", "generated")
                .AddChildContent<AccordionTrigger<string>>()
                .AddChildContent<AccordionPanel<string>>(panel => panel.Add(x => x.KeepMounted, true)))
            .AddChildContent<AccordionItem<string>>(item => item
                .Add(x => x.Value, "b")
                .AddUnmatched("id", "consumer-item")
                .AddChildContent<AccordionTrigger<string>>()));
        IElement generated = cut.Find("[data-testid=generated]");
        int Number(string? id) => int.Parse(id!["base-ui-".Length..], System.Globalization.CultureInfo.InvariantCulture);
        int panel = Number(cut.FindAll("[role=region]")[0].Id);
        int trigger = Number(cut.FindAll("button")[0].Id);

        Assert.Equal(trigger + 1, Number(generated.Id));
        Assert.Equal(panel + 1, trigger);
        Assert.Equal("consumer-item", cut.Find("#consumer-item").Id);
    }

    [Fact(DisplayName = "port: nested accordions are independent")]
    [Trait("Requirement", "ACC-STR-07")]
    public void Nested_accordions_are_independent()
    {
        List<string> outer = [];
        List<string> inner = [];
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .Add(x => x.DefaultValue, ["outer"])
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<IReadOnlyList<string>> args) => outer.Add(string.Join(',', args.Value)))
            .AddChildContent<AccordionItem<string>>(item => item
                .Add(x => x.Value, "outer")
                .AddChildContent<AccordionPanel<string>>(panel => panel.AddChildContent<AccordionRoot<string>>(root => root
                    .Add(x => x.OnValueChange, (ValueChangeEventArgs<IReadOnlyList<string>> args) => inner.Add(string.Join(',', args.Value)))
                    .AddChildContent<AccordionItem<string>>(innerItem => innerItem
                        .Add(x => x.Value, "inner")
                        .AddChildContent<AccordionTrigger<string>>())))));

        cut.Find("button").Click();

        Assert.Equal(["inner"], inner);
        Assert.Empty(outer);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
    }

    // Design point D4 (option (a), settled by the orchestrator 2026-10-26): Base UI renders every part of every item once on a commit, because the
    // value is in every state. Blazor renders more: the root's and each item's cascade re-renders its content in a later queue
    // entry than the parts' own subscription renders, so an item renders twice and its header, trigger and open panel three
    // times. The counts are a budget (ADR 16 rule 7: they only tighten), measured 2026-10-26 at 3 and 20 items.
    [Theory(DisplayName = "port: a committed toggle renders the root once and each item part within its budget")]
    [InlineData(3)]
    [InlineData(20)]
    [Trait("Requirement", "ACC-STA-02")]
    [Trait("Requirement", "ADR16-8")]
    public void A_committed_toggle_renders_each_part_within_its_budget(int items)
    {
        const int ItemBudget = 2;
        const int ItemPartBudget = 3;
        Dictionary<string, int> renders = new(StringComparer.Ordinal);
        IRenderedComponent<AccordionRoot<string>> cut = Context.Render<AccordionRoot<string>>(p => p
            .Add(x => x.ClassValue, (Func<AccordionRootState<string>, string?>)(_ => Count(renders, "root")))
            .Add(x => x.ChildContent, (RenderFragment)(builder => BuildItems(builder, items, renders))));
        Dictionary<string, int> before = new(renders, StringComparer.Ordinal);

        cut.FindAll("button")[0].Click();

        Dictionary<string, int> delta = renders.ToDictionary(pair => pair.Key, pair => pair.Value - before.GetValueOrDefault(pair.Key), StringComparer.Ordinal);
        Assert.Equal(1, delta["root"]);
        for (int i = 0; i < items; i++)
        {
            Assert.Equal(ItemBudget, delta[$"item {i}"]);
            Assert.Equal(ItemPartBudget, delta[$"header {i}"]);
            Assert.Equal(ItemPartBudget, delta[$"trigger {i}"]);
        }

        // Only the opened item's panel exists.
        Assert.Equal(ItemPartBudget, delta["panel 0"]);
        Assert.False(delta.ContainsKey("panel 1"));
    }

    private static string? Count(Dictionary<string, int> renders, string key)
    {
        renders[key] = renders.GetValueOrDefault(key) + 1;
        return null;
    }

    private static void BuildItems(RenderTreeBuilder builder, int count, Dictionary<string, int> renders)
    {
        for (int i = 0; i < count; i++)
        {
            string index = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            builder.OpenComponent<AccordionItem<string>>(0);
            builder.SetKey(index);
            builder.AddComponentParameter(1, nameof(AccordionItem<string>.Value), index);
            builder.AddComponentParameter(2, nameof(AccordionItem<string>.ClassValue), (Func<AccordionItemState<string>, string?>)(_ => Count(renders, $"item {index}")));
            builder.AddComponentParameter(3, nameof(AccordionItem<string>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<AccordionHeader<string>>(0);
                content.AddComponentParameter(1, nameof(AccordionHeader<string>.ClassValue), (Func<AccordionHeaderState<string>, string?>)(_ => Count(renders, $"header {index}")));
                content.AddComponentParameter(2, nameof(AccordionHeader<string>.ChildContent), (RenderFragment)(header =>
                {
                    header.OpenComponent<AccordionTrigger<string>>(0);
                    header.AddComponentParameter(1, nameof(AccordionTrigger<string>.ClassValue), (Func<AccordionTriggerState<string>, string?>)(_ => Count(renders, $"trigger {index}")));
                    header.CloseComponent();
                }));
                content.CloseComponent();
                content.OpenComponent<AccordionPanel<string>>(3);
                content.AddComponentParameter(4, nameof(AccordionPanel<string>.ClassValue), (Func<AccordionPanelState<string>, string?>)(_ => Count(renders, $"panel {index}")));
                content.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
