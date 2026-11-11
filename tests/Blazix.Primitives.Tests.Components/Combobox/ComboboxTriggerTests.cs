using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.9 at L2 (ComboboxTrigger.test.tsx's titles whose assertions are what C# renders and sends): the button, the ARIA of
// both layouts, the state attributes, the id with the input inside the popup, and the requests the trigger's handles route.
// The press itself, the typeahead and the drag selection are L4 rows (the `interactions`, `composite` and `combobox`
// modules); the cancel-open release is the module's (L3), so its title here is the routing of the report.
/// <summary>Combobox's Trigger at L2.</summary>
public sealed class ComboboxTriggerTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets tabIndex to -1 when not used as the main anchor")]
    [Trait("Requirement", "CMB-DOM-05")]
    public void Sets_tabIndex_to_minus_one_when_not_used_as_the_main_anchor()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), Layout.InputOutside);

        Assert.Equal("-1", Trigger(cut).GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "should render aria-disabled attribute when disabled")]
    [Trait("Requirement", "CMB-API-44")]
    public void Should_render_the_disabled_attribute_when_disabled()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), Layout.NoInput, ("Disabled", true));

        Assert.True(Trigger(cut).HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should inherit disabled state from ComboboxRoot")]
    [Trait("Requirement", "CMB-API-44")]
    public void Should_inherit_disabled_state_from_ComboboxRoot()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Disabled"] = true }, Layout.NoInput);

        Assert.True(Trigger(cut).HasAttribute("disabled"));
        Assert.True(Trigger(cut).HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "should prioritize local disabled over root disabled")]
    [Trait("Requirement", "CMB-API-44")]
    public void Should_prioritize_local_disabled_over_root_disabled()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Disabled"] = false }, Layout.NoInput, ("Disabled", true));

        Assert.True(Trigger(cut).HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: a disabled trigger asks the interactions module for no press")]
    [Trait("Requirement", "CMB-API-44")]
    public void A_disabled_trigger_asks_for_no_press()
    {
        IRenderedComponent<ComboboxHarness> enabled = Render(new() { ["Id"] = "enabled" }, Layout.NoInput);
        Assert.Contains("\"event\":\"mousedown\"", TriggerOptions("enabled"), StringComparison.Ordinal);

        IRenderedComponent<ComboboxHarness> disabled = Render(new() { ["Id"] = "disabled" }, Layout.NoInput, ("Disabled", true));
        Assert.DoesNotContain("\"click\"", TriggerOptions("disabled"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "sets aria-required attribute when required (input inside popup)")]
    [Trait("Requirement", "CMB-ARIA-07")]
    [Trait("Requirement", "CMB-API-28")]
    public void Sets_aria_required_when_required_with_the_input_inside()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Required"] = true }, Layout.NoInput);

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-required"));
    }

    [Fact(DisplayName = "does not set aria-required attribute when the input is outside the popup")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public void Does_not_set_aria_required_with_the_input_outside()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Required"] = true }, Layout.InputOutside);

        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-required"));
        Assert.False(Trigger(cut).HasAttribute("aria-required"));
    }

    [Fact(DisplayName = "sets aria-readonly attribute when readOnly (input inside popup)")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public void Sets_aria_readonly_when_readOnly_with_the_input_inside()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["ReadOnly"] = true }, Layout.NoInput);

        Assert.Equal("combobox", Trigger(cut).GetAttribute("role"));
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "does not set aria-readonly attribute when the input is outside the popup")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public void Does_not_set_aria_readonly_with_the_input_outside()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["ReadOnly"] = true }, Layout.InputOutside);

        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-readonly"));
        Assert.False(Trigger(cut).HasAttribute("role"));
        Assert.False(Trigger(cut).HasAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "sets all aria attributes on the input when closed")]
    [Trait("Requirement", "CMB-ARIA-07")]
    [Trait("Requirement", "CMB-DOM-05")]
    public void Sets_all_aria_attributes_on_the_trigger_when_closed()
    {
        IElement trigger = Trigger(Render(new(), Layout.InputInside));

        Assert.Equal("combobox", trigger.GetAttribute("role"));
        Assert.Equal("0", trigger.GetAttribute("tabindex"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.False(trigger.HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "sets all aria attributes on the trigger when open")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public void Sets_all_aria_attributes_on_the_trigger_when_open()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, Layout.InputInside);
        IElement trigger = Trigger(cut);
        string popupId = cut.Find("[data-testid=popup]").Id!;

        Assert.Equal("0", trigger.GetAttribute("tabindex"));
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.NotEqual(string.Empty, popupId);
        Assert.Equal(popupId, trigger.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "port: with the input outside, an open trigger controls the list")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public void With_the_input_outside_an_open_trigger_controls_the_list()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, Layout.InputOutside);

        Assert.Equal("listbox", Trigger(cut).GetAttribute("aria-haspopup"));
        Assert.Equal(cut.Find("[data-testid=list]").Id, Trigger(cut).GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "sets the id on the trigger when the input is inside the popup")]
    [Trait("Requirement", "CMB-API-45")]
    [Trait("Requirement", "CMB-API-27")]
    public void Sets_the_id_on_the_trigger_when_the_input_is_inside_the_popup()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "fruit" }, Layout.InputInside);

        Assert.Equal("fruit", Trigger(cut).Id);
    }

    [Fact(DisplayName = "applies the data-readonly style hook only when readOnly")]
    [Trait("Requirement", "CMB-DOM-04")]
    public void Applies_the_data_readonly_style_hook_only_when_readOnly()
    {
        Assert.True(Trigger(Render(new() { ["ReadOnly"] = true }, Layout.NoInput)).HasAttribute("data-readonly"));
        Assert.False(Trigger(Render(new(), Layout.NoInput)).HasAttribute("data-readonly"));
    }

    [Fact(DisplayName = "has data-placeholder when no value is selected")]
    [Trait("Requirement", "CMB-STA-07")]
    public void Has_data_placeholder_when_no_value_is_selected()
    {
        Assert.True(Trigger(Render(new(), Layout.NoInput)).HasAttribute("data-placeholder"));
    }

    [Fact(DisplayName = "does not have data-placeholder when value is selected")]
    [Trait("Requirement", "CMB-STA-07")]
    public void Does_not_have_data_placeholder_when_value_is_selected()
    {
        IElement trigger = Trigger(Render(new() { ["DefaultValue"] = "apple" }, Layout.NoInput));

        Assert.Equal("button", trigger.TagName.ToLowerInvariant());
        Assert.False(trigger.HasAttribute("data-placeholder"));
    }

    [Fact(DisplayName = "has data-placeholder when multiple mode has empty array")]
    [Trait("Requirement", "CMB-STA-07")]
    public void Has_data_placeholder_when_multiple_mode_has_empty_array()
    {
        IElement trigger = Trigger(Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)[] }, Layout.NoInput));

        Assert.True(trigger.HasAttribute("data-placeholder"));
    }

    [Fact(DisplayName = "does not have data-placeholder when multiple mode has a default value")]
    [Trait("Requirement", "CMB-STA-07")]
    public void Does_not_have_data_placeholder_when_multiple_mode_has_a_default_value()
    {
        IElement trigger = Trigger(Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple"] }, Layout.NoInput));

        Assert.Equal("button", trigger.TagName.ToLowerInvariant());
        Assert.False(trigger.HasAttribute("data-placeholder"));
    }

    [Fact(DisplayName = "fires with reason trigger-press when Trigger is clicked")]
    [Trait("Requirement", "CMB-PTR-02")]
    [Trait("Requirement", "CMB-EVT-04")]
    public async Task Fires_with_reason_trigger_press_when_Trigger_is_clicked()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "fruit" }, Layout.InputInside);
        InteractionsTwin twin = TriggerTwin("fruit");

        await cut.InvokeAsync(() => twin.OnRequest($$"""{"open":true,"reason":"trigger-press","event":{{ComboboxTestContext.Click}},"triggerId":"fruit"}"""));

        (bool open, EventReason reason) = Assert.Single(cut.Instance.OpenChanges);
        Assert.True(open);
        Assert.Equal(EventReason.TriggerPress, reason);
    }

    [Fact(DisplayName = "port: the module's cancel-open report closes the popup with reason cancel-open")]
    [Trait("Requirement", "CMB-PTR-03")]
    public async Task The_cancel_open_report_closes_the_popup()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, Layout.InputInside);
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.TriggerPart);

        await cut.InvokeAsync(() => twin.OnCancelOpen(ComboboxTestContext.Click));

        (bool open, EventReason reason) = Assert.Single(cut.Instance.OpenChanges);
        Assert.False(open);
        Assert.Equal(EventReason.CancelOpen, reason);
    }

    [Fact(DisplayName = "port: a trigger rendered before an outside input gives up the root's id and the combobox role")]
    [Trait("Requirement", "CMB-API-45")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public void A_trigger_before_an_outside_input_gives_up_the_roots_id()
    {
        RenderFragment extra = builder =>
        {
            builder.OpenComponent<ComboboxTrigger>(0);
            builder.AddComponentParameter(1, "data-testid", "trigger");
            builder.CloseComponent();
            builder.OpenComponent<ComboboxInput>(2);
            builder.AddComponentParameter(3, "data-testid", "input");
            builder.CloseComponent();
        };

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Id"] = "fruit" })
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, extra));

        Assert.Equal("fruit", cut.Find("[data-testid=input]").Id);
        Assert.NotEqual("fruit", Trigger(cut).Id);
        Assert.False(Trigger(cut).HasAttribute("role"));
        Assert.Equal("-1", Trigger(cut).GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: with the input inside the popup, the input is the combobox (ComboboxRoot.test.tsx:711's findByRole)")]
    [Trait("Requirement", "CMB-ARIA-01")]
    [Trait("Requirement", "CMB-ARIA-03")]
    public void With_the_input_inside_the_popup_the_input_is_the_combobox()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, Layout.InputInside);
        IElement input = cut.Find("[data-testid=input]");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal("listbox", input.GetAttribute("aria-haspopup"));
        Assert.Equal(cut.Find("[data-testid=list]").Id, input.GetAttribute("aria-controls"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal("combobox", Trigger(cut).GetAttribute("role"));
    }

    [Fact(DisplayName = "port: the input is the list's reference in both layouts, and the trigger joins no list")]
    [Trait("Requirement", "CMB-ARIA-03")]
    public void The_input_is_the_lists_reference_and_the_trigger_joins_no_list()
    {
        foreach (Layout layout in new[] { Layout.InputInside, Layout.InputOutside })
        {
            using ComboboxTestContext context = new();
            IRenderedComponent<ComboboxHarness> cut = context.Context.Render<ComboboxHarness>(p => p
                .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true })
                .Add(x => x.InputInsidePopup, layout == Layout.InputInside)
                .Add(x => x.Extra, (RenderFragment)(builder =>
                {
                    builder.OpenComponent<ComboboxTrigger>(0);
                    builder.AddComponentParameter(1, "data-testid", "trigger");
                    builder.CloseComponent();
                })));

            // One list handle with the reference role (the input), one floating (the list), none with the trigger role: the
            // trigger attaches no list handle at all.
            List<string> options = context.Composite.Invocations["attach"].Select(attach => attach.Arguments[1] as string ?? string.Empty).ToList();
            Assert.Single(options, json => json.Contains("\"reference\":{}", StringComparison.Ordinal));
            Assert.DoesNotContain(options, json => json.Contains("\"trigger\":{}", StringComparison.Ordinal));
            Assert.Equal(2, options.Count(json => !json.Contains("\"registrationOnly\":true", StringComparison.Ordinal)));
        }
    }

    [Fact(DisplayName = "port: the module's ArrowDown on the trigger opens the popup with list-navigation")]
    [Trait("Requirement", "CMB-KEY-01")]
    public async Task ArrowDown_on_the_trigger_opens_with_list_navigation()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), Layout.InputInside);

        await cut.InvokeAsync(() => test.TwinOf(ComboboxTwin.TriggerPart).OnOpenRequest(ComboboxTestContext.Key("ArrowDown")));

        (bool open, EventReason reason) = Assert.Single(cut.Instance.OpenChanges);
        Assert.True(open);
        Assert.Equal(EventReason.ListNavigation, reason);
    }

    // ComboboxTrigger.tsx:196-199: with the input outside the popup, or none, a trigger press makes the trigger the floating
    // root's DOM reference, so its next press toggles the popup closed and a popup without an input anchors to it.
    [Theory(DisplayName = "port: a trigger press with the input outside the popup makes the trigger the session's active trigger")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "CMB-PTR-02")]
    [Trait("Requirement", "CMB-POS-01")]
    public async Task A_trigger_press_makes_the_trigger_the_active_trigger(bool withInput)
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), withInput ? Layout.InputOutside : Layout.NoInput);
        string id = Trigger(cut).Id!;

        await cut.InvokeAsync(() => Trigger(cut).MouseDownAsync(new MouseEventArgs { Button = 0, Type = "mousedown" }));

        Assert.Contains("\"activeTriggerId\":\"" + id + "\"", LastSessionOptions(), StringComparison.Ordinal);
    }

    // AriaCombobox.tsx:1614-1622: the hidden input hands its focus to the trigger while the input is inside the popup, else to
    // the input, else to the trigger; the `form` module's control role moves it (FFI-FOC-05).
    [Theory(DisplayName = "port: the hidden control hands its focus to the input outside the popup, else to the trigger")]
    [InlineData("outside")]
    [InlineData("inside")]
    [InlineData("none")]
    [Trait("Requirement", "CMB-FOC-05")]
    public void The_hidden_control_hands_its_focus_to_the_input_else_to_the_trigger(string layout)
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), layout switch { "outside" => Layout.InputOutside, "inside" => Layout.InputInside, _ => Layout.NoInput });
        string expected = layout == "outside" ? cut.Find("input[role=combobox]").Id! : Trigger(cut).Id!;

        Assert.Contains("\"focusTarget\":\"" + expected + "\"", LastHiddenControlOptions(), StringComparison.Ordinal);
    }

    // ComboboxTrigger.tsx:107-122 (CMB-KEY-12; option (a), settled by the orchestrator 2026-11-11): the trigger runs the list's
    // typeahead while closed, single, not read-only and not disabled, over the root's labels (`labelsRef`, the derived labels
    // with `items`), and a match selects `valuesRef[index]` with the reason none.
    [Fact(DisplayName = "port: the trigger's typeahead-only list handle matches the derived labels, and a closed match selects the value with reason none")]
    [Trait("Requirement", "CMB-KEY-12")]
    public async Task The_triggers_typeahead_selects_the_matched_value()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)["apple", "banana", "cherry"] }, Layout.InputInside);

        string options = TypeaheadOptions();
        Assert.Contains("\"typeaheadOnly\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"trigger\":{}", options, StringComparison.Ordinal);
        Assert.Contains("\"labels\":[\"apple\",\"banana\",\"cherry\"]", options, StringComparison.Ordinal);

        await cut.InvokeAsync(() => TypeaheadTwin().OnListMatch($$"""{"index":1,"event":{{ComboboxTestContext.Key("b")}}}"""));

        (string? value, EventReason reason) = Assert.Single(cut.Instance.ValueChanges);
        Assert.Equal("banana", value);
        Assert.Equal(EventReason.None, reason);
        Assert.Empty(cut.Instance.OpenChanges);
    }

    [Theory(DisplayName = "port: the trigger's typeahead is off while multiple, read-only or disabled")]
    [InlineData("Multiple")]
    [InlineData("ReadOnly")]
    [InlineData("Disabled")]
    [Trait("Requirement", "CMB-KEY-12")]
    public void The_triggers_typeahead_is_off_while_multiple_read_only_or_disabled(string flag)
    {
        Render(new() { ["Items"] = (IReadOnlyList<string>)["apple", "banana"], [flag] = true }, Layout.InputInside);

        Assert.DoesNotContain("\"typeahead\":", TypeaheadOptions(), StringComparison.Ordinal);
    }

    private static IElement Trigger(IRenderedComponent<ComboboxHarness> cut) => cut.Find("[data-testid=trigger]");

    private string TypeaheadOptions()
    {
        string last = string.Empty;
        foreach (JSRuntimeInvocation attach in test.Composite.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"typeaheadOnly\":true", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.CompositeHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"typeaheadOnly\":true", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        return last;
    }

    private CompositeTwin TypeaheadTwin() =>
        ((DotNetObjectReference<CompositeTwin>)test.Composite.Invocations["attach"].Last(attach => attach.Arguments[1] is string json && json.Contains("\"typeaheadOnly\":true", StringComparison.Ordinal)).Arguments[2]!).Value;

    private string LastHiddenControlOptions()
    {
        string last = string.Empty;
        foreach (JSRuntimeInvocation attach in test.Form.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.FormHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        return last;
    }

    private string LastSessionOptions()
    {
        string last = string.Empty;
        foreach (JSRuntimeInvocation attach in test.Interactions.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"session\":", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.InteractionsHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"session\":", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        return last;
    }

    private string TriggerOptions(string id) =>
        (string)test.Interactions.Invocations["attach"].Last(attach => attach.Arguments[1] is string json && json.Contains("\"triggerId\":\"" + id + "\"", StringComparison.Ordinal)).Arguments[1]!;

    private InteractionsTwin TriggerTwin(string id) =>
        ((DotNetObjectReference<InteractionsTwin>)test.Interactions.Invocations["attach"].Last(attach => attach.Arguments[1] is string json && json.Contains("\"triggerId\":\"" + id + "\"", StringComparison.Ordinal)).Arguments[2]!).Value;

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, Layout layout, params (string Name, object? Value)[] trigger)
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxTrigger>(0);
            builder.AddComponentParameter(1, "data-testid", "trigger");
            foreach ((string name, object? parameter) in trigger)
            {
                builder.AddComponentParameter(2, name, parameter);
            }

            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.WithInput, layout != Layout.NoInput)
            .Add(x => x.InputInsidePopup, layout == Layout.InputInside)
            .Add(x => x.Extra, (RenderFragment)Extra));
    }

    private enum Layout
    {
        NoInput,
        InputOutside,
        InputInside,
    }
}
