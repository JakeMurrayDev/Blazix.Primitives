using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>AccordionRoot.test.tsx</c>'s <c>[L2]</c> titles (ACC §13), titles verbatim; where Base UI repeats a title under several
/// <c>describe</c> blocks, the block is named in parentheses. bUnit has no layout, so <c>toBeVisible()</c> reads as "rendered
/// and not <c>hidden</c>" (settled by the orchestrator 2026-10-25), and a closing panel leaves the document when the
/// <c>collapsible</c> module reports the end of its exit, as Base UI's <c>BASE_UI_ANIMATIONS_DISABLED</c> ends it at once. The
/// keyboard titles run at L4; here their activation is the click the <c>button</c> module dispatches.
/// </summary>
public sealed class AccordionRootTests : IDisposable
{
    private const string Panel1 = AccordionScenarios.PanelContent1;
    private const string Panel2 = AccordionScenarios.PanelContent2;

    private readonly AccordionTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "warns when hiddenUntilFound overrides keepMounted={false}")]
    [Trait("Requirement", "ACC-EDGE-04")]
    [Trait("Requirement", "ACC-API-06")]
    [Trait("Requirement", "ACC-DOM-15")]
    public void Warns_when_hiddenUntilFound_overrides_keepMounted_false()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.HiddenUntilFound, true)
            .Add(x => x.KeepMounted, false));
        cut.Render();

        (LogLevel level, string message) = Assert.Single(host.RootLogger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "Blazix: The `KeepMounted` parameter set to `false` on `AccordionRoot` is ignored when `HiddenUntilFound` is enabled, since panels must remain mounted while closed.",
            message);
        // The closed panel stays in the document, hidden; the module writes the `until-found` literal (L4).
        Assert.True(Panel(cut).HasAttribute("hidden"));
    }

    [Fact(DisplayName = "renders correct ARIA attributes")]
    [Trait("Requirement", "ACC-ARIA-02")]
    [Trait("Requirement", "ACC-ARIA-03")]
    [Trait("Requirement", "ACC-ARIA-04")]
    [Trait("Requirement", "ACC-API-15")]
    [Trait("Requirement", "ACC-DOM-19")]
    public void Renders_correct_ARIA_attributes()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.DefaultValue, [0]));
        IElement trigger = cut.Find("button");
        IElement panel = Panel(cut);

        Assert.True(trigger.HasAttribute("aria-controls"));
        Assert.Equal(trigger.GetAttribute("aria-controls"), panel.GetAttribute("id"));
        Assert.Equal("region", panel.GetAttribute("role"));
        Assert.Equal(panel.GetAttribute("aria-labelledby"), trigger.GetAttribute("id"));
    }

    [Fact(DisplayName = "references manual panel id in trigger aria-controls")]
    [Trait("Requirement", "ACC-API-18")]
    [Trait("Requirement", "ACC-ARIA-02")]
    [Trait("Requirement", "ACC-STA-12")]
    public void References_manual_panel_id_in_trigger_aria_controls()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.PanelId, "custom-panel-id"));

        Assert.Equal("custom-panel-id", cut.Find("button").GetAttribute("aria-controls"));
        Assert.Equal("custom-panel-id", Panel(cut).GetAttribute("id"));
    }

    [Fact(DisplayName = "port: an empty panel id renders as written while aria-controls names the generated panel id")]
    [Trait("Requirement", "ACC-API-18")]
    [Trait("Requirement", "ACC-DOM-19")]
    public void An_empty_panel_id_renders_as_written()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.PanelId, string.Empty));

        Assert.Equal(string.Empty, Panel(cut).GetAttribute("id"));
        Assert.StartsWith("base-ui-", cut.Find("button").GetAttribute("aria-controls"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "references manual trigger id in panel aria-labelledby")]
    [Trait("Requirement", "ACC-API-15")]
    [Trait("Requirement", "ACC-ARIA-04")]
    public void References_manual_trigger_id_in_panel_aria_labelledby()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.TriggerId, "custom-trigger-id"));

        Assert.Equal("custom-trigger-id", Panel(cut).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "updates panel labeling when a manual trigger id is added or changed")]
    [Trait("Requirement", "ACC-STA-11")]
    [Trait("Requirement", "ACC-ARIA-04")]
    [Trait("Requirement", "ACC-DOM-13")]
    public void Updates_panel_labeling_when_a_manual_trigger_id_is_added_or_changed()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.DefaultValue, [0]));
        IElement trigger = cut.Find("button");
        Assert.True(trigger.HasAttribute("id"));
        Assert.Equal(trigger.Id, Panel(cut).GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.TriggerId, "custom-trigger-id-1"));
        Assert.Equal("custom-trigger-id-1", Panel(cut).GetAttribute("aria-labelledby"));
        Assert.Equal("custom-trigger-id-1", cut.Find("button").Id);

        cut.Render(p => p.Add(x => x.TriggerId, "custom-trigger-id-2"));
        Assert.Equal("custom-trigger-id-2", Panel(cut).GetAttribute("aria-labelledby"));
        Assert.Equal("custom-trigger-id-2", cut.Find("button").Id);
    }

    [Fact(DisplayName = "restores panel labeling when a manual trigger id is removed")]
    [Trait("Requirement", "ACC-STA-11")]
    [Trait("Requirement", "ACC-ARIA-04")]
    public void Restores_panel_labeling_when_a_manual_trigger_id_is_removed()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.TriggerId, "custom-trigger-id")
            .Add(x => x.TriggerIdSupplied, true));
        Assert.Equal("custom-trigger-id", Panel(cut).GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.TriggerId, (string?)null).Add(x => x.TriggerIdSupplied, true));

        string? labelledBy = Panel(cut).GetAttribute("aria-labelledby");
        Assert.StartsWith("base-ui-", labelledBy, StringComparison.Ordinal);
        Assert.Equal(labelledBy, cut.Find("button").Id);
    }

    // ADR 06 Amendments 2026-10-18 rule 2: StrictMode's repeated mount and teardown is restated as registration idempotency,
    // here a repeated unmount and remount of each part (COL-EDGE-09's precedent).
    [Fact(DisplayName = "unregisters generated part ids when the trigger or panel unmounts")]
    [Trait("Requirement", "ACC-STA-11")]
    [Trait("Requirement", "ACC-STA-12")]
    [Trait("Requirement", "ACC-ARIA-05")]
    public void Unregisters_generated_part_ids_when_the_trigger_or_panel_unmounts()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.DefaultValue, [0]));

        for (int round = 0; round < 2; round++)
        {
            cut.Render(p => p.Add(x => x.TriggerMounted, false));
            Assert.False(Panel(cut).HasAttribute("aria-labelledby"));

            cut.Render(p => p.Add(x => x.TriggerMounted, true));
            Assert.Equal(cut.Find("button").Id, Panel(cut).GetAttribute("aria-labelledby"));

            cut.Render(p => p.Add(x => x.PanelMounted, false));
            Assert.False(cut.Find("button").HasAttribute("aria-controls"));

            cut.Render(p => p.Add(x => x.PanelMounted, true));
            Assert.Equal(Panel(cut).Id, cut.Find("button").GetAttribute("aria-controls"));
        }
    }

    [Fact(DisplayName = "open state (uncontrolled)")]
    [Trait("Requirement", "ACC-API-03")]
    [Trait("Requirement", "ACC-API-10")]
    [Trait("Requirement", "ACC-PTR-01")]
    [Trait("Requirement", "ACC-ARIA-01")]
    [Trait("Requirement", "ACC-DOM-04")]
    [Trait("Requirement", "ACC-DOM-11")]
    [Trait("Requirement", "ACC-DOM-16")]
    public void Open_state_uncontrolled()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoGeneratedItems);
        IElement trigger = cut.FindAll("button")[0];

        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.DoesNotContain(Panel1, cut.Markup, StringComparison.Ordinal);

        trigger.Click();

        trigger = cut.FindAll("button")[0];
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.True(trigger.HasAttribute("data-panel-open"));
        IElement panel = cut.Find("[data-testid=panel1]");
        Assert.False(panel.HasAttribute("hidden"));
        Assert.True(panel.HasAttribute("data-open"));

        trigger.Click();
        host.CompletePanelExits(cut);

        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
    }

    [Fact(DisplayName = "custom item value (uncontrolled)")]
    [Trait("Requirement", "ACC-API-02")]
    [Trait("Requirement", "ACC-STA-04")]
    public void Custom_item_value_uncontrolled()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoStringItems, p => p
            .Add(x => x.DefaultStrings, ["first"])
            .Add(x => x.FirstValue, "first")
            .Add(x => x.SecondValue, "second"));

        IElement panel = cut.Find("[data-testid=panel1]");
        Assert.False(panel.HasAttribute("hidden"));
        Assert.True(panel.HasAttribute("data-open"));
        Assert.Empty(cut.FindAll("[data-testid=panel2]"));
    }

    [Fact(DisplayName = "open state (controlled)")]
    [Trait("Requirement", "ACC-API-01")]
    [Trait("Requirement", "ACC-STA-07")]
    [Trait("Requirement", "ACC-EVT-07")]
    [Trait("Requirement", "ACC-ARIA-01")]
    public void Open_state_controlled()
    {
        List<ValueChangeEventArgs<bool>> openChanges = [];
        List<ValueChangeEventArgs<IReadOnlyList<int>>> valueChanges = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.ControlledValue, [])
            .Add(x => x.OnOpenChange, args => openChanges.Add(args))
            .Add(x => x.OnValueChange, args => valueChanges.Add(args)));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));

        cut.Render(p => p.Add(x => x.ControlledValue, [0]));

        IElement trigger = cut.Find("button");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.True(trigger.HasAttribute("data-panel-open"));
        Assert.False(Panel(cut).HasAttribute("hidden"));
        Assert.True(Panel(cut).HasAttribute("data-open"));

        cut.Render(p => p.Add(x => x.ControlledValue, []));
        host.CompletePanelExits(cut);

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        // ACC-EVT-07: a consumer value change calls neither callback.
        Assert.Empty(openChanges);
        Assert.Empty(valueChanges);
    }

    [Fact(DisplayName = "custom item value (controlled)")]
    [Trait("Requirement", "ACC-API-01")]
    [Trait("Requirement", "ACC-STA-04")]
    public void Custom_item_value_controlled()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoStringItems, p => p
            .Add(x => x.ControlledStrings, ["one"])
            .Add(x => x.SecondValue, "second"));

        IElement panel = cut.Find("[data-testid=panel1]");
        Assert.False(panel.HasAttribute("hidden"));
        Assert.True(panel.HasAttribute("data-open"));
        Assert.Empty(cut.FindAll("[data-testid=panel2]"));
    }

    [Fact(DisplayName = "can disable the whole accordion")]
    [Trait("Requirement", "ACC-API-05")]
    [Trait("Requirement", "ACC-DOM-06")]
    [Trait("Requirement", "ACC-ARIA-07")]
    [Trait("Requirement", "ACC-EDGE-08")]
    public void Can_disable_the_whole_accordion()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.RootDisabled, true));
        IReadOnlyList<IElement> headers = cut.FindAll("h3");
        IReadOnlyList<IElement> triggers = cut.FindAll("button");

        // ACC-EDGE-08: the item open in the default value stays open while disabled.
        IElement[] elements = [cut.Find("[data-testid=item1]"), headers[0], triggers[0], cut.Find("[data-testid=panel1]"), cut.Find("[data-testid=item2]"), headers[1], triggers[1]];
        Assert.All(elements, element => Assert.True(element.HasAttribute("data-disabled")));
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-open"));
        // ACC-ARIA-07: the headers are headings by their element, with no role of their own.
        Assert.All(headers, header => Assert.False(header.HasAttribute("role")));
    }

    [Fact(DisplayName = "can disable one accordion item")]
    [Trait("Requirement", "ACC-API-11")]
    [Trait("Requirement", "ACC-DOM-06")]
    public void Can_disable_one_accordion_item()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.ItemDisabled, true));
        IReadOnlyList<IElement> headers = cut.FindAll("h3");
        IReadOnlyList<IElement> triggers = cut.FindAll("button");

        Assert.All([cut.Find("[data-testid=item1]"), headers[0], triggers[0], cut.Find("[data-testid=panel1]")], element => Assert.True(element.HasAttribute("data-disabled")));
        Assert.All([cut.Find("[data-testid=item2]"), headers[1], triggers[1]], element => Assert.False(element.HasAttribute("data-disabled")));
    }

    [Theory(DisplayName = "does not toggle or fire callbacks when the %s is disabled")]
    [InlineData("root")]
    [InlineData("item")]
    [Trait("Requirement", "ACC-API-05")]
    [Trait("Requirement", "ACC-API-11")]
    [Trait("Requirement", "ACC-API-14")]
    [Trait("Requirement", "ACC-PTR-02")]
    public void Does_not_toggle_or_fire_callbacks_when_the_part_is_disabled(string disabledPart)
    {
        int valueChanges = 0;
        int openChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.RootDisabled, disabledPart == "root")
            .Add(x => x.ItemDisabled, disabledPart == "item")
            .Add(x => x.TriggerDisabled, false)
            .Add(x => x.OnValueChange, _ => valueChanges++)
            .Add(x => x.OnOpenChange, _ => openChanges++));
        IElement trigger = cut.FindAll("button")[0];

        trigger.Click();

        Assert.True(trigger.HasAttribute("disabled"));
        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(0, valueChanges);
        Assert.Equal(0, openChanges);
    }

    // RND-API-06: the port's veto is PreventPartHandler() on a C# handler; a handler for an event the part does not handle
    // receives an event on which it can be called.
    [Fact(DisplayName = "allows onMouseUp to call preventBaseUIHandler on the trigger")]
    [Trait("Requirement", "ACC-PTR-03")]
    public void Allows_onMouseUp_to_call_preventBaseUIHandler_on_the_trigger()
    {
        int mouseUps = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.TriggerMouseUp, e =>
        {
            mouseUps++;
            e.PreventPartHandler();
        }));

        Exception? thrown = Record.Exception(() => cut.Find("button").MouseUp());

        Assert.Null(thrown);
        Assert.Equal(1, mouseUps);
    }

    [Theory(DisplayName = "opens and closes on Space keyup when rendering $triggerKind triggers")]
    [InlineData("interactive")]
    [InlineData("non-interactive")]
    [Trait("Requirement", "ACC-EVT-01")]
    public void Opens_and_closes_when_rendering_trigger_kinds(string triggerKind)
    {
        List<bool> openChanges = [];
        bool native = triggerKind == "interactive";
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p =>
        {
            p.Add(x => x.NativeButton, native);
            p.Add(x => x.OnOpenChange, args => openChanges.Add(args.Value));
            if (!native)
            {
                p.Add(x => x.TriggerRender, (Microsoft.AspNetCore.Components.RenderFragment)(b =>
                {
                    b.OpenElement(0, "span");
                    b.CloseElement();
                }));
            }
        });

        // The `button` module turns the Space keyup into this click; the keyup timing runs at L4.
        cut.Find("[aria-expanded]").Click();
        Assert.Equal("true", cut.Find("[aria-expanded]").GetAttribute("aria-expanded"));
        Assert.Equal([true], openChanges);

        cut.Find("[aria-expanded]").Click();
        Assert.Equal("false", cut.Find("[aria-expanded]").GetAttribute("aria-expanded"));
        Assert.Equal([true, false], openChanges);
    }

    [Fact(DisplayName = "onOpenChange cancel() prevents opening while uncontrolled")]
    [Trait("Requirement", "ACC-EVT-02")]
    [Trait("Requirement", "ACC-API-12")]
    public void OnOpenChange_cancel_prevents_opening_while_uncontrolled()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.OnValueChange, _ => valueChanges++)
            .Add(x => x.OnOpenChange, args =>
            {
                if (args.Value)
                {
                    args.Details.Cancel();
                }
            }));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(0, valueChanges);
    }

    [Fact(DisplayName = "onValueChange cancel() prevents opening while uncontrolled")]
    [Trait("Requirement", "ACC-EVT-04")]
    public void OnValueChange_cancel_prevents_opening_while_uncontrolled()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p.Add(x => x.OnValueChange, args =>
        {
            valueChanges++;
            args.Details.Cancel();
        }));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(1, valueChanges);
    }

    [Fact(DisplayName = "onValueChange cancel() prevents closing while uncontrolled")]
    [Trait("Requirement", "ACC-EVT-04")]
    public void OnValueChange_cancel_prevents_closing_while_uncontrolled()
    {
        List<IReadOnlyList<int>> values = [];
        bool canceledInside = false;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.OnValueChange, args =>
            {
                values.Add(args.Value);
                args.Details.Cancel();
                canceledInside = args.Details.IsCanceled;
            }));

        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.True(Panel(cut).HasAttribute("data-open"));
        Assert.Empty(Assert.Single(values));
        // ACC-EVT-04: reading isCanceled after cancel() inside the handler returns true.
        Assert.True(canceledInside);
    }

    [Fact(DisplayName = "onOpenChange cancel() prevents onValueChange while controlled")]
    [Trait("Requirement", "ACC-EVT-02")]
    public void OnOpenChange_cancel_prevents_onValueChange_while_controlled()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.ControlledValue, [])
            .Add(x => x.OnValueChange, _ => valueChanges++)
            .Add(x => x.OnOpenChange, args =>
            {
                if (args.Value)
                {
                    args.Details.Cancel();
                }
            }));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(0, valueChanges);
    }

    [Fact(DisplayName = "onValueChange cancel() prevents opening while controlled")]
    [Trait("Requirement", "ACC-EVT-04")]
    [Trait("Requirement", "ACC-STA-07")]
    public void OnValueChange_cancel_prevents_opening_while_controlled()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.ApplyingControlled, p => p.Add(x => x.OnValueChange, args =>
        {
            valueChanges++;
            args.Details.Cancel();
        }));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(1, valueChanges);
    }

    [Fact(DisplayName = "onValueChange cancel() prevents opening while multiple")]
    [Trait("Requirement", "ACC-EVT-04")]
    public void OnValueChange_cancel_prevents_opening_while_multiple()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.Multiple, true)
            .Add(x => x.OnValueChange, args =>
            {
                valueChanges++;
                args.Details.Cancel();
            }));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(1, valueChanges);
    }

    [Fact(DisplayName = "onValueChange cancel() prevents closing while multiple")]
    [Trait("Requirement", "ACC-EVT-04")]
    public void OnValueChange_cancel_prevents_closing_while_multiple()
    {
        int valueChanges = 0;
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.OneItem, p => p
            .Add(x => x.DefaultValue, [0])
            .Add(x => x.Multiple, true)
            .Add(x => x.OnValueChange, args =>
            {
                valueChanges++;
                args.Details.Cancel();
            }));

        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("[data-testid=panel1]"));
        Assert.Equal(1, valueChanges);
    }

    [Fact(DisplayName = "keeps an array item value intact while multiple")]
    [Trait("Requirement", "ACC-STA-06")]
    public void Keeps_an_array_item_value_intact_while_multiple()
    {
        string[] itemValue = ["section", "details"];
        List<IReadOnlyList<string[]>> values = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.ArrayItem, p => p
            .Add(x => x.ArrayItemValue, itemValue)
            .Add(x => x.OnArrayValueChange, args => values.Add(args.Value)));

        cut.Find("button").Click();

        Assert.Same(itemValue, Assert.Single(Assert.Single(values)));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("[data-testid=panel1]"));
    }

    [Fact(DisplayName = "multiple items can be open when `multiple = true`")]
    [Trait("Requirement", "ACC-API-04")]
    [Trait("Requirement", "ACC-STA-06")]
    [Trait("Requirement", "ACC-DOM-11")]
    public void Multiple_items_can_be_open_when_multiple_is_true()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoGeneratedItems, p => p.Add(x => x.Multiple, true));
        Assert.All(cut.FindAll("button"), trigger => Assert.False(trigger.HasAttribute("data-panel-open")));
        Assert.Empty(cut.FindAll("[data-testid^=panel]"));

        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();

        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-open"));
        Assert.True(cut.Find("[data-testid=panel2]").HasAttribute("data-open"));
        Assert.All(cut.FindAll("button"), trigger => Assert.True(trigger.HasAttribute("data-panel-open")));

        cut.FindAll("button")[0].Click();
        host.CompletePanelExits(cut);

        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.True(cut.Find("[data-testid=panel2]").HasAttribute("data-open"));
        Assert.False(cut.FindAll("button")[0].HasAttribute("data-panel-open"));
        Assert.True(cut.FindAll("button")[1].HasAttribute("data-panel-open"));
    }

    [Fact(DisplayName = "when false only one item can be open")]
    [Trait("Requirement", "ACC-API-04")]
    [Trait("Requirement", "ACC-STA-05")]
    [Trait("Requirement", "ACC-DOM-04")]
    public void When_false_only_one_item_can_be_open()
    {
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoGeneratedItems, p => p.Add(x => x.Multiple, false));
        Assert.Empty(cut.FindAll("[data-testid^=panel]"));

        cut.FindAll("button")[0].Click();
        Assert.True(cut.Find("[data-testid=panel1]").HasAttribute("data-open"));
        Assert.True(cut.FindAll("button")[0].HasAttribute("data-panel-open"));

        cut.FindAll("button")[1].Click();
        host.CompletePanelExits(cut);

        Assert.True(cut.Find("[data-testid=panel2]").HasAttribute("data-open"));
        Assert.True(cut.FindAll("button")[1].HasAttribute("data-panel-open"));
        Assert.Empty(cut.FindAll("[data-testid=panel1]"));
        Assert.False(cut.FindAll("button")[0].HasAttribute("data-panel-open"));
    }

    [Fact(DisplayName = "default item value")]
    [Trait("Requirement", "ACC-STA-06")]
    [Trait("Requirement", "ACC-EVT-03")]
    [Trait("Requirement", "ACC-EVT-05")]
    public void Default_item_value()
    {
        List<ValueChangeEventArgs<IReadOnlyList<int>>> calls = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoItems, p => p
            .Add(x => x.Multiple, true)
            .Add(x => x.OnValueChange, args => calls.Add(args)));
        Assert.Empty(calls);

        cut.FindAll("button")[0].Click(new MouseEventArgs { Type = "click", Detail = 1 });
        Assert.Single(calls);
        Assert.Equal([0], calls[^1].Value);
        Assert.Equal(EventReason.TriggerPress, calls[^1].Details.Reason);
        // ACC-EVT-05: the event is the click's own snapshot (a pointer click, then a keyboard one), never a placeholder.
        Assert.Equal("click", calls[^1].Details.Event!.Type);

        cut.FindAll("button")[1].Click(new MouseEventArgs { Type = "click", Detail = 0 });
        Assert.Equal(2, calls.Count);
        Assert.Equal([0, 1], calls[^1].Value);
        Assert.Equal(EventReason.TriggerPress, calls[^1].Details.Reason);
        Assert.Equal("click", calls[^1].Details.Event!.Type);
    }

    [Fact(DisplayName = "custom item value (onValueChange)")]
    [Trait("Requirement", "ACC-STA-06")]
    [Trait("Requirement", "ACC-EVT-03")]
    public void Custom_item_value_on_value_change()
    {
        List<IReadOnlyList<string>> values = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoStringItems, p => p
            .Add(x => x.Multiple, true)
            .Add(x => x.OnStringValueChange, args => values.Add(args.Value)));

        cut.FindAll("button")[1].Click();
        Assert.Equal(["two"], Assert.Single(values));

        cut.FindAll("button")[0].Click();
        Assert.Equal(2, values.Count);
        Assert.Equal(["two", "one"], values[1]);
    }

    [Fact(DisplayName = "`multiple` is false")]
    [Trait("Requirement", "ACC-STA-05")]
    [Trait("Requirement", "ACC-EVT-03")]
    [Trait("Requirement", "ACC-EVT-07")]
    public void Multiple_is_false()
    {
        List<IReadOnlyList<string>> values = [];
        IRenderedComponent<AccordionScenarios> cut = host.Render(AccordionScenario.TwoStringItems, p => p
            .Add(x => x.Multiple, false)
            .Add(x => x.OnStringValueChange, args => values.Add(args.Value)));

        cut.FindAll("button")[0].Click();
        Assert.Equal(["one"], Assert.Single(values));

        cut.FindAll("button")[1].Click();
        Assert.Equal(2, values.Count);
        Assert.Equal(["two"], values[1]);
    }

    private static IElement Panel(IRenderedComponent<AccordionScenarios> cut) => cut.Find("[data-testid=panel1]");
}
