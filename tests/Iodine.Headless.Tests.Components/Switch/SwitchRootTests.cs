using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SwitchRoot.test.tsx, the rows that render neither Field nor Form (issue 80). A click reaches C# through the `button`
// module: on Server as the OnToggle report (ToggleAsync), on WebAssembly as the toggle decision (DecideToggleAsync); the
// browser half of each title is tests/browser/switch.spec.ts. Port titles are marked as such in a comment.
/// <summary>The switch root: its element, its sibling inputs, its controlled value, its attributes and its twins.</summary>
public sealed class SwitchRootTests : IDisposable
{
    private readonly SwitchTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should update its state if the underlying input is toggled")]
    [Trait("Requirement", "SWT-STR-01")]
    [Trait("Requirement", "SWT-PTR-04")]
    public async Task Should_update_its_state_if_the_underlying_input_is_toggled()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();

        // SWT-STR-01: the root, then the checkbox input, as siblings; the input is never inside the root.
        Assert.Equal(["SPAN", "INPUT"], cut.Nodes.OfType<IElement>().Select(e => e.TagName));
        Assert.Empty(cut.Find("span").QuerySelectorAll("input"));

        await test.ToggleAsync(cut, true);

        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should change its state when clicked")]
    [Trait("Requirement", "SWT-API-02")]
    [Trait("Requirement", "SWT-STA-02")]
    [Trait("Requirement", "SWT-ARIA-02")]
    public async Task Should_change_its_state_when_clicked()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));

        Assert.Equal("run", await test.DecideToggleAsync(cut));

        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Equal("""{"checked":true,"liveRegion":false}""", test.DomOptions[^1]);
    }

    [Fact(DisplayName = "should update its state when changed from outside")]
    [Trait("Requirement", "SWT-API-01")]
    [Trait("Requirement", "SWT-STA-03")]
    public async Task Should_update_its_state_when_changed_from_outside()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.Checked, false));
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));

        cut.Render(p => p.Add(x => x.Checked, true));
        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Equal("""{"checked":true,"liveRegion":false}""", test.DomOptions[^1]);

        // A user toggle alone does not move a controlled switch; the module is told to restore the input.
        await test.ToggleAsync(cut, false);
        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Equal("""{"checked":true,"checkedRestores":1,"liveRegion":false}""", test.DomOptions[^1]);

        cut.Render(p => p.Add(x => x.Checked, false));
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should call onCheckedChange when clicked")]
    [Trait("Requirement", "SWT-API-03")]
    [Trait("Requirement", "SWT-EVT-01")]
    [Trait("Requirement", "SWT-EVT-06")]
    public async Task Should_call_onCheckedChange_when_clicked()
    {
        List<ValueChangeEventArgs<bool>> calls = [];
        List<bool> stateWhenCalled = [];
        IRenderedComponent<SwitchRoot> cut = null!;
        cut = Context.Render<SwitchRoot>(p => p.Add(x => x.OnCheckedChange, args =>
        {
            calls.Add(args);
            stateWhenCalled.Add(cut.Find("[role=switch]").GetAttribute("aria-checked") == "true");
        }));

        await test.ToggleAsync(cut, true, shiftKey: true);

        ValueChangeEventArgs<bool> call = Assert.Single(calls);
        Assert.True(call.Value);
        Assert.Equal([false], stateWhenCalled);
        Assert.Equal(EventReason.None, call.Details.Reason);
        Assert.Equal("click", call.Details.Event.Type);
        Assert.True(call.Details.Event.ShiftKey);
        Assert.Null(call.Details.Trigger);
    }

    [Fact(DisplayName = "port: the change details carry the reason none, whatever the toggle came from")]
    [Trait("Requirement", "SWT-EVT-02")]
    [Trait("Requirement", "SWT-EVT-08")]
    public async Task Port_the_change_details_carry_the_reason_none()
    {
        List<ChangeEventDetails> details = [];
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.OnCheckedChange, args =>
        {
            args.Details.AllowPropagation();
            details.Add(args.Details);
        }));

        await test.ToggleAsync(cut, true);
        await test.DecideToggleAsync(cut);

        Assert.All(details, d => Assert.Equal(EventReason.None, d.Reason));
        Assert.All(details, d => Assert.True(d.IsPropagationAllowed));
        Assert.Equal(2, details.Count);
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "port: a canceled toggle keeps the state and restores the input, decided inside the click on WebAssembly")]
    [Trait("Requirement", "SWT-EVT-04")]
    public async Task Port_a_canceled_toggle_keeps_the_state_and_restores_the_input()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.OnCheckedChange, args => args.Details.Cancel()));

        // WebAssembly: the decision answers stop, so the module reverts the box natively and nothing is restored later.
        Assert.Equal("stop", await test.DecideToggleAsync(cut));
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Equal(["""{"checked":false,"liveRegion":false}"""], test.DomOptions);

        // Server: the report arrives after the browser flipped the box, so the part asks the module to restore it.
        await test.ToggleAsync(cut, true);
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Equal("""{"checked":false,"checkedRestores":1,"liveRegion":false}""", test.DomOptions[^1]);
    }

    [Fact(DisplayName = "port: a controlled switch whose consumer does not pass the value back answers stop on WebAssembly")]
    [Trait("Requirement", "SWT-STA-03")]
    public async Task Port_a_controlled_switch_whose_consumer_does_not_pass_the_value_back_answers_stop()
    {
        List<bool> requested = [];
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.Checked, false)
            .Add(x => x.CheckedChanged, requested.Add));

        Assert.Equal("stop", await test.DecideToggleAsync(cut));

        Assert.Equal([true], requested);
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "port: CheckedChanged runs after OnCheckedChange and not for a canceled toggle")]
    [Trait("Requirement", "SWT-API-03")]
    public async Task Port_CheckedChanged_runs_after_OnCheckedChange_and_not_for_a_canceled_toggle()
    {
        List<string> order = [];
        bool cancel = true;
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.OnCheckedChange, args =>
            {
                order.Add($"change:{args.Value}");
                if (cancel)
                {
                    args.Details.Cancel();
                }
            })
            .Add(x => x.CheckedChanged, value => order.Add($"bind:{value}")));

        await test.ToggleAsync(cut, true);
        cancel = false;
        await test.ToggleAsync(cut, true);

        Assert.Equal(["change:True", "change:True", "bind:True"], order);
    }

    [Fact(DisplayName = "port: ownership is fixed at the first parameter set, and a removed checked falls back to the initial default")]
    [Trait("Requirement", "SWT-STA-01")]
    [Trait("Requirement", "SWT-STA-05")]
    [Trait("Requirement", "SWT-EDGE-02")]
    public async Task Port_ownership_is_fixed_at_the_first_parameter_set()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.Checked, true).Add(x => x.DefaultChecked, false));

        cut.Render(p => p.Add(x => x.DefaultChecked, false));
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));

        // Still controlled: a toggle does not commit.
        await test.ToggleAsync(cut, true);
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.Contains(test.Logger.Entries, e => e.Message.StartsWith(
            "A component is changing the controlled checked state of Switch to be uncontrolled.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: changing defaultChecked after the first render logs and changes nothing")]
    [Trait("Requirement", "SWT-EDGE-03")]
    public void Port_changing_defaultChecked_after_the_first_render_logs_and_changes_nothing()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.DefaultChecked, false));

        cut.Render(p => p.Add(x => x.DefaultChecked, true));

        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        (LogLevel level, string message) = Assert.Single(test.Logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "A component is changing the default checked state of an uncontrolled Switch after being initialized. To suppress this warning opt to use a controlled Switch.",
            message);
    }

    [Fact(DisplayName = "should override the built-in attributes")]
    [Trait("Requirement", "SWT-DOM-14")]
    [Trait("Requirement", "SWT-ARIA-01")]
    [Trait("Requirement", "SWT-API-15")]
    public void Should_override_the_built_in_attributes()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.AddUnmatched("role", "checkbox").AddUnmatched("data-testid", "switch"));

        Assert.Equal("checkbox", cut.Find("[data-testid=switch]").GetAttribute("role"));
    }

    [Fact(DisplayName = "should call onClick when clicked")]
    [Trait("Requirement", "SWT-API-15")]
    [Trait("Requirement", "SWT-PTR-09")]
    public async Task Should_call_onClick_when_clicked()
    {
        int clicks = 0;
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)));

        // Server: Blazor dispatches the root click to the part's handler, which runs the consumer's.
        await cut.Find("[role=switch]").ClickAsync(new MouseEventArgs());
        Assert.Equal(1, clicks);

        // WebAssembly: the activation decision runs it inside the dispatch, and Blazor's dispatch of the same click skips it.
        Assert.Equal("run", await test.DecideRootClickAsync(cut));
        await cut.Find("[role=switch]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal(2, clicks);
    }

    [Fact(DisplayName = "port: a consumer onClick that prevents the part's handler stops the toggle on WebAssembly")]
    [Trait("Requirement", "SWT-PTR-09")]
    public async Task Port_a_consumer_onClick_that_prevents_the_parts_handler_stops_the_toggle()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, e => e.PreventPartHandler())));

        Assert.Equal("stop", await test.DecideRootClickAsync(cut));
    }

    [Fact(DisplayName = "uses aria-disabled instead of HTML disabled")]
    [Trait("Requirement", "SWT-ARIA-05")]
    [Trait("Requirement", "SWT-FOC-02")]
    public void Uses_aria_disabled_instead_of_HTML_disabled()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.Disabled, true));
        IElement root = cut.Find("[role=switch]");

        Assert.False(root.HasAttribute("disabled"));
        Assert.Equal("true", root.GetAttribute("aria-disabled"));
        Assert.Equal("-1", root.GetAttribute("tabindex"));
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should not have the `disabled` attribute when `disabled` is not set")]
    [Trait("Requirement", "SWT-ARIA-05")]
    public void Should_not_have_the_disabled_attribute_when_disabled_is_not_set()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();

        Assert.False(cut.Find("[role=switch]").HasAttribute("disabled"));
        Assert.False(cut.Find("[role=switch]").HasAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "should have the `aria-readonly` attribute")]
    [Trait("Requirement", "SWT-ARIA-03")]
    [Trait("Requirement", "SWT-API-05")]
    public void Should_have_the_aria_readonly_attribute()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.ReadOnly, true));

        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-readonly"));
        Assert.Contains("\"readOnly\":true", test.LastButtonOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should not have the aria attribute when `readOnly` is not set")]
    [Trait("Requirement", "SWT-ARIA-03")]
    public void Should_not_have_the_aria_attribute_when_readOnly_is_not_set()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();

        Assert.False(cut.Find("[role=switch]").HasAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "port: a read-only switch sends readOnly to the module and ignores a toggle that still arrives")]
    [Trait("Requirement", "SWT-API-05")]
    [Trait("Requirement", "SWT-EVT-07")]
    public async Task Port_a_read_only_switch_ignores_a_toggle()
    {
        int calls = 0;
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.ReadOnly, true)
            .Add(x => x.OnCheckedChange, _ => calls++));

        Assert.Equal("stop", await test.DecideToggleAsync(cut));
        await test.ToggleAsync(cut, true);

        Assert.Equal(0, calls);
        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should have the `aria-required` attribute")]
    [Trait("Requirement", "SWT-ARIA-04")]
    [Trait("Requirement", "SWT-API-06")]
    public void Should_have_the_aria_required_attribute()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.Required, true));

        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-required"));
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("required"));
    }

    [Fact(DisplayName = "should not have the aria attribute when `required` is not set")]
    [Trait("Requirement", "SWT-ARIA-04")]
    public void Should_not_have_the_aria_attribute_when_required_is_not_set()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();

        Assert.False(cut.Find("[role=switch]").HasAttribute("aria-required"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("required"));
    }

    [Fact(DisplayName = "should place the style hooks on the root and the thumb")]
    [Trait("Requirement", "SWT-STR-03")]
    [Trait("Requirement", "SWT-STA-08")]
    [Trait("Requirement", "SWT-DOM-02")]
    [Trait("Requirement", "SWT-DOM-03")]
    [Trait("Requirement", "SWT-DOM-06")]
    [Trait("Requirement", "SWT-API-16")]
    public async Task Should_place_the_style_hooks_on_the_root_and_the_thumb()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.DefaultChecked, true)
            .Add(x => x.Disabled, true)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.Required, true)
            .AddChildContent<SwitchThumb>(t => t.AddUnmatched("data-testid", "thumb")));

        IElement root = cut.Find("[role=switch]");
        IElement thumb = cut.Find("[data-testid=thumb]");
        foreach (IElement element in new[] { root, thumb })
        {
            Assert.True(element.HasAttribute("data-checked"));
            Assert.False(element.HasAttribute("data-unchecked"));
            Assert.Equal(string.Empty, element.GetAttribute("data-disabled"));
            Assert.Equal(string.Empty, element.GetAttribute("data-readonly"));
            Assert.Equal(string.Empty, element.GetAttribute("data-required"));
        }

        // SWT-STR-03: the thumb carries no handler, ARIA or id of its own.
        Assert.Equal(
            ["data-checked", "data-disabled", "data-readonly", "data-required", "data-testid"],
            thumb.Attributes.Select(a => a.Name).Where(n => n != "blazor:elementreference").Order(StringComparer.Ordinal));

        cut.Render(p => p.Add(x => x.Disabled, false).Add(x => x.ReadOnly, false));
        await test.ToggleAsync(cut, false);

        root = cut.Find("[role=switch]");
        thumb = cut.Find("[data-testid=thumb]");
        foreach (IElement element in new[] { root, thumb })
        {
            Assert.Equal(string.Empty, element.GetAttribute("data-unchecked"));
            Assert.False(element.HasAttribute("data-checked"));
        }
    }

    [Fact(DisplayName = "should set the name attribute only on the input")]
    [Trait("Requirement", "SWT-API-07")]
    [Trait("Requirement", "SWT-DOM-12")]
    public void Should_set_the_name_attribute_only_on_the_input()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.Name, "switch-name"));

        Assert.Equal("switch-name", cut.Find("input[type=checkbox]").GetAttribute("name"));
        Assert.False(cut.Find("[role=switch]").HasAttribute("name"));
    }

    [Fact(DisplayName = "should not set the value attribute by default")]
    [Trait("Requirement", "SWT-DOM-11")]
    [Trait("Requirement", "SWT-API-08")]
    public void Should_not_set_the_value_attribute_by_default()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();

        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("value"));
    }

    [Fact(DisplayName = "should set the value attribute only on the input")]
    [Trait("Requirement", "SWT-DOM-11")]
    [Trait("Requirement", "SWT-DOM-12")]
    [Trait("Requirement", "SWT-API-08")]
    public void Should_set_the_value_attribute_only_on_the_input()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.Value, "1"));

        Assert.Equal("1", cut.Find("input[type=checkbox]").GetAttribute("value"));
        Assert.False(cut.Find("[role=switch]").HasAttribute("value"));
    }

    [Fact(DisplayName = "port: form is placed on both hidden inputs and never on the root")]
    [Trait("Requirement", "SWT-API-10")]
    public void Port_form_is_placed_on_both_hidden_inputs_and_never_on_the_root()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.Form, "external")
            .Add(x => x.Name, "n")
            .Add(x => x.UncheckedValue, "off"));

        Assert.Equal("external", cut.Find("input[type=checkbox]").GetAttribute("form"));
        Assert.Equal("external", cut.Find("input[type=hidden]").GetAttribute("form"));
        Assert.False(cut.Find("[role=switch]").HasAttribute("form"));
    }

    [Fact(DisplayName = "port: the unchecked-value input sits between the root and the checkbox only while unchecked, named and given a value")]
    [Trait("Requirement", "SWT-DOM-13")]
    [Trait("Requirement", "SWT-API-09")]
    [Trait("Requirement", "SWT-STR-01")]
    public async Task Port_the_unchecked_value_input_sits_between_the_root_and_the_checkbox()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.Name, "n")
            .Add(x => x.UncheckedValue, "off")
            .Add(x => x.Disabled, true)
            .Add(x => x.Form, "f"));

        Assert.Equal(["SPAN", "INPUT", "INPUT"], cut.Nodes.OfType<IElement>().Select(e => e.TagName));
        IElement hidden = cut.Find("input[type=hidden]");
        Assert.Equal(("n", "off", "f", true), (hidden.GetAttribute("name"), hidden.GetAttribute("value"), hidden.GetAttribute("form"), hidden.HasAttribute("disabled")));

        cut.Render(p => p.Add(x => x.Disabled, false));
        await test.ToggleAsync(cut, true);
        Assert.Empty(cut.FindAll("input[type=hidden]"));

        cut.Render(p => p.Add(x => x.Name, null).Add(x => x.DefaultChecked, false));
        await test.ToggleAsync(cut, false);
        Assert.Empty(cut.FindAll("input[type=hidden]"));

        IRenderedComponent<SwitchRoot> noValue = Context.Render<SwitchRoot>(p => p.Add(x => x.Name, "n"));
        Assert.Empty(noValue.FindAll("input[type=hidden]"));
    }

    [Fact(DisplayName = "port: the hidden input is a checkbox hidden from the accessibility tree and the tab order")]
    [Trait("Requirement", "SWT-DOM-07")]
    public void Port_the_hidden_input_is_a_checkbox_hidden_from_the_accessibility_tree_and_the_tab_order()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();
        IElement input = cut.Find("input");

        Assert.Equal(("checkbox", "true", "-1"), (input.GetAttribute("type"), input.GetAttribute("aria-hidden"), input.GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "port: the hidden input is visually hidden, kept in flow beside the switch when named")]
    [Trait("Requirement", "SWT-DOM-09")]
    public void Port_the_hidden_input_is_visually_hidden()
    {
        IRenderedComponent<SwitchRoot> nameless = Context.Render<SwitchRoot>();
        IRenderedComponent<SwitchRoot> named = Context.Render<SwitchRoot>(p => p.Add(x => x.Name, "n"));

        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0",
            nameless.Find("input").GetAttribute("style"));
        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute",
            named.Find("input").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the hidden input renders the initial checked value once and leaves it to the dom module after")]
    [Trait("Requirement", "SWT-DOM-08")]
    [Trait("Requirement", "SWT-EDGE-10")]
    public async Task Port_the_hidden_input_renders_the_initial_checked_value_once()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.Add(x => x.DefaultChecked, true));
        Assert.True(cut.Find("input").HasAttribute("checked"));

        await test.ToggleAsync(cut, false);

        // The attribute C# rendered is frozen; the property follows through the dom module's options.
        Assert.True(cut.Find("input").HasAttribute("checked"));
        Assert.Equal(["""{"checked":true,"liveRegion":false}""", """{"checked":false,"liveRegion":false}"""], test.DomOptions);
    }

    [Fact(DisplayName = "should toggle the switch when a explicitly linked <label> is clicked")]
    [Trait("Requirement", "SWT-API-11")]
    [Trait("Requirement", "SWT-DOM-10")]
    [Trait("Requirement", "SWT-DOM-01")]
    public async Task Should_toggle_the_switch_when_a_explicitly_linked_label_is_clicked()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.AddUnmatched("id", "mySwitch"));

        // The id is the input's (the label targets it); the root keeps a generated id.
        Assert.Equal("mySwitch", cut.Find("input").GetAttribute("id"));
        Assert.StartsWith(PartIdGenerator.Prefix, cut.Find("[role=switch]").GetAttribute("id"), StringComparison.Ordinal);

        await test.ToggleAsync(cut, true);
        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should associate `id` with the native button when `nativeButton=true`")]
    [Trait("Requirement", "SWT-API-11")]
    [Trait("Requirement", "SWT-DOM-01")]
    [Trait("Requirement", "SWT-DOM-10")]
    [Trait("Requirement", "SWT-ARIA-09")]
    public void Should_associate_id_with_the_native_button_when_nativeButton_true()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .AddUnmatched("id", "mySwitch")
            .Add(x => x.NativeButton, true)
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "button");
                builder.CloseElement();
            })));

        IElement root = cut.Find("[role=switch]");
        Assert.Equal("mySwitch", root.GetAttribute("id"));
        Assert.False(cut.Find("input").HasAttribute("id"));
        Assert.False(root.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "port: without an id prop each element gets its own generated id, and the input none in native mode")]
    [Trait("Requirement", "SWT-DOM-01")]
    [Trait("Requirement", "SWT-DOM-10")]
    public void Port_without_an_id_prop_each_element_gets_its_own_generated_id()
    {
        IRenderedComponent<SwitchRoot> nonNative = Context.Render<SwitchRoot>();
        IRenderedComponent<SwitchRoot> native = Context.Render<SwitchRoot>(p => p.Add(x => x.NativeButton, true));

        string rootId = nonNative.Find("[role=switch]").GetAttribute("id")!;
        string inputId = nonNative.Find("input").GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, rootId, StringComparison.Ordinal);
        Assert.StartsWith(PartIdGenerator.Prefix, inputId, StringComparison.Ordinal);
        Assert.NotEqual(rootId, inputId);
        Assert.StartsWith(PartIdGenerator.Prefix, native.Find("[role=switch]").GetAttribute("id"), StringComparison.Ordinal);
        Assert.False(native.Find("input").HasAttribute("id"));
    }

    [Fact(DisplayName = "updates fallback `aria-labelledby` when the hidden input id changes")]
    [Trait("Requirement", "SWT-DOM-18")]
    public void Updates_fallback_aria_labelledby_when_the_hidden_input_id_changes()
    {
        // The label half (the fallback through the form module) is tests/browser/switch.spec.ts.
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p.AddUnmatched("id", "switch-input-a"));
        string rootId = cut.Find("[role=switch]").GetAttribute("id")!;

        cut.Render(p => p.AddUnmatched("id", "switch-input-b"));

        Assert.Equal("switch-input-b", cut.Find("input").GetAttribute("id"));
        Assert.Equal(rootId, cut.Find("[role=switch]").GetAttribute("id"));
    }

    [Fact(DisplayName = "can render a native button")]
    [Trait("Requirement", "SWT-API-13")]
    [Trait("Requirement", "SWT-FOC-01")]
    [Trait("Requirement", "SWT-DOM-16")]
    public void Can_render_a_native_button()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>(p => p
            .Add(x => x.NativeButton, true)
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "button");
                builder.CloseElement();
            })));

        IElement root = cut.Find("[role=switch]");
        Assert.Equal("BUTTON", root.TagName);
        Assert.Equal("button", root.GetAttribute("type"));
        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.Contains("\"native\":true", test.LastButtonOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the non-native root is a focusable span without a type and the input is never a tab stop")]
    [Trait("Requirement", "SWT-FOC-01")]
    [Trait("Requirement", "SWT-DOM-16")]
    [Trait("Requirement", "SWT-API-13")]
    public void Port_the_non_native_root_is_a_focusable_span()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();
        IElement root = cut.Find("[role=switch]");

        Assert.Equal("SPAN", root.TagName);
        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.False(root.HasAttribute("type"));
        Assert.Equal("-1", cut.Find("input").GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: the twins attach to the root and to the hidden input, with the bridge on")]
    [Trait("Requirement", "UBT-EVT-09")]
    [Trait("Requirement", "SWT-DOM-08")]
    public void Port_the_twins_attach_to_the_root_and_to_the_hidden_input()
    {
        IRenderedComponent<SwitchRoot> cut = Context.Render<SwitchRoot>();

        Assert.Equal(
            """{"disabled":false,"native":false,"composite":false,"hiddenInput":true,"reportInteractionType":false,"developmentChecks":true}""",
            Assert.Single(test.ButtonModule.Invocations["attach"]).Arguments[1]);
        Assert.Equal(cut.Instance.Element, Assert.Single(test.ButtonModule.Invocations["attach"]).Arguments[0]);
        Assert.Equal(["""{"checked":false,"liveRegion":false}"""], test.DomOptions);

        cut.Render();
        Assert.Single(test.ButtonModule.Invocations["attach"]);
        Assert.Single(test.DomModule.Invocations["attach"]);
        Assert.Empty(test.DomHandle.Invocations["update"]);
    }

    [Fact(DisplayName = "port: disposing the switch disposes both handles")]
    public async Task Port_disposing_the_switch_disposes_both_handles()
    {
        Context.Render<SwitchRoot>();

        await Context.DisposeComponentsAsync();

        Assert.Single(test.ButtonHandle.Invocations["dispose"]);
        Assert.Single(test.DomHandle.Invocations["dispose"]);
    }
}
