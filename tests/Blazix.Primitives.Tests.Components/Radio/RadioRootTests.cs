using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioRoot.test.tsx, the [L2] rows and halves of radio.md (issue 83), and port titles for the `none` rows a part test can
// show. A click reaches C# through the `button` module: on Server as the OnToggle report (ToggleAsync), on WebAssembly as
// the toggle decision (DecideToggleAsync); the browser half of each title is tests/browser/radio.spec.ts. Port titles are
// marked as such.
/// <summary>The radio root: its element, its hidden input, its checked state, its attributes and its twins.</summary>
public sealed class RadioRootTests : IDisposable
{
    private const string RadioSelector = "[role=radio]";

    private static readonly RenderFragment s_nativeButtonElement = builder =>
    {
        builder.OpenElement(0, "button");
        builder.CloseElement();
    };

    private readonly RadioTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets checked and unchecked data attributes")]
    [Trait("Requirement", "RAD-DOM-03")]
    [Trait("Requirement", "RAD-STA-01")]
    [Trait("Requirement", "RAD-API-09")]
    public void Sets_checked_and_unchecked_data_attributes()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "checked")
            .AddChildContent(RadioTestContext.StringRadios("checked", "unchecked")));

        IElement checkedRadio = cut.Find("[data-testid=checked]");
        IElement uncheckedRadio = cut.Find("[data-testid=unchecked]");
        Assert.True(checkedRadio.HasAttribute("data-checked"));
        Assert.False(checkedRadio.HasAttribute("data-unchecked"));
        Assert.True(uncheckedRadio.HasAttribute("data-unchecked"));
        Assert.False(uncheckedRadio.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "does not forward `value` prop")]
    [Trait("Requirement", "RAD-API-01")]
    [Trait("Requirement", "RAD-DOM-14")]
    public void Does_not_forward_value_prop()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .AddChildContent(RadioTestContext.StringRadios("test")));

        IElement root = cut.Find("[data-testid=test]");
        Assert.False(root.HasAttribute("value"));

        // RAD-DOM-14: none of the input's form attributes reach the root.
        foreach (string attribute in new[] { "name", "form", "required", "readonly", "disabled" })
        {
            Assert.False(root.HasAttribute(attribute), attribute);
        }
    }

    // Port, 2026-10-23 (ADR 08 Amendments 2026-10-22): `null` means nothing selected, so a radio whose value is null behaves
    // as Base UI's `undefined` radio: it never selects (RAD-EVT-03) and renders no value (RAD-DOM-11). Base UI's title
    // "allows `null` value" selects it; radio.md's dated deviation note on RAD-STA-01's `null === null` clause.
    [Fact(DisplayName = "port: a radio whose value is null is never selected (allows `null` value)")]
    [Trait("Requirement", "RAD-STA-01")]
    [Trait("Requirement", "RAD-ARIA-02")]
    [Trait("Requirement", "RAD-API-01")]
    [Trait("Requirement", "RAD-EVT-03")]
    [Trait("Requirement", "RAD-DOM-11")]
    public async Task Port_a_radio_whose_value_is_null_is_never_selected()
    {
        List<ValueChangeEventArgs<string?>> calls = [];
        IRenderedComponent<RadioGroup<string?>> cut = Context.Render<RadioGroup<string?>>(p => p
            .Add(x => x.OnValueChange, args => calls.Add(args))
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioRoot<string?>>(0);
                builder.AddComponentParameter(1, nameof(RadioRoot<string?>.Value), (string?)null);
                builder.AddAttribute(2, "data-testid", "radio-null");
                builder.CloseComponent();
                builder.OpenComponent<RadioRoot<string?>>(3);
                builder.AddComponentParameter(4, nameof(RadioRoot<string?>.Value), "a");
                builder.AddAttribute(5, "data-testid", "radio-a");
                builder.CloseComponent();
            }));

        IReadOnlyList<IElement> inputs = RadioTestContext.Inputs(cut);
        Assert.False(inputs[0].HasAttribute("value"));

        // Server: the click is reported, refused and the input restored; WebAssembly: the decision stops it.
        await test.ToggleAsync(cut, 0);
        Assert.Equal("false", cut.Find("[data-testid=radio-null]").GetAttribute("aria-checked"));
        Assert.Equal("""{"checked":false,"checkedRestores":1,"liveRegion":false}""", test.DomUpdates[^1]);
        Assert.Equal("stop", await test.DecideToggleAsync(cut, 0));
        Assert.Empty(calls);

        await test.ToggleAsync(cut, 1);
        Assert.Equal("true", cut.Find("[data-testid=radio-a]").GetAttribute("aria-checked"));
        Assert.Equal("false", cut.Find("[data-testid=radio-null]").GetAttribute("aria-checked"));
        Assert.Equal("a", Assert.Single(calls).Value);
    }

    [Fact(DisplayName = "associates `id` with the native button when `nativeButton=true`")]
    [Trait("Requirement", "RAD-API-07")]
    [Trait("Requirement", "RAD-DOM-01")]
    [Trait("Requirement", "RAD-DOM-10")]
    [Trait("Requirement", "RAD-STR-01")]
    [Trait("Requirement", "RAD-ARIA-05")]
    public async Task Associates_id_with_the_native_button_when_nativeButton_is_true()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "b")
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioRoot<string>>(0);
                builder.AddComponentParameter(1, nameof(RadioRoot<string>.Value), "a");
                builder.AddComponentParameter(2, nameof(RadioRoot<string>.NativeButton), true);
                builder.AddComponentParameter(3, nameof(RadioRoot<string>.Render), s_nativeButtonElement);
                builder.AddAttribute(4, "id", "myRadio");
                builder.AddAttribute(5, "data-testid", "a");
                builder.CloseComponent();
                builder.OpenComponent<RadioRoot<string>>(6);
                builder.AddComponentParameter(7, nameof(RadioRoot<string>.Value), "b");
                builder.AddAttribute(8, "data-testid", "b");
                builder.CloseComponent();
            }));

        IElement radioA = cut.Find("[data-testid=a]");
        Assert.Equal("BUTTON", radioA.TagName);
        Assert.Equal("myRadio", radioA.GetAttribute("id"));
        Assert.Equal("button", radioA.GetAttribute("type"));

        // RAD-STR-01: the input is the root's next sibling, never a descendant, and has no id in native mode.
        IElement? hiddenInput = radioA.NextElementSibling;
        Assert.Equal("INPUT", hiddenInput?.TagName);
        Assert.False(hiddenInput!.HasAttribute("id"));
        Assert.Empty(radioA.QuerySelectorAll("input"));

        // RAD-ARIA-05: a native button looks for no native label (the label's `for` names the button itself); the other,
        // non-native radio does.
        Assert.Equal([false, true], test.FormOptions.Where(o => !o.ReportEvents).Select(o => o.LabelFallback));

        // The label's activation is the browser's (L4); here the click the native button dispatches to the input.
        Assert.Equal("false", radioA.GetAttribute("aria-checked"));
        await test.ToggleAsync(cut, 0);
        Assert.Equal("true", cut.Find("[data-testid=a]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "prefers `aria-label` over an associated label")]
    [Trait("Requirement", "RAD-ARIA-03")]
    [Trait("Requirement", "RAD-EDGE-05")]
    public async Task Prefers_aria_label_over_an_associated_label()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<RadioRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(RadioRoot<string>.Value), "a");
            builder.AddAttribute(2, "aria-label", "Apple");
            builder.CloseComponent();
        }));

        // The hidden input's handle never looks for a label while a non-blank aria-label names the radio.
        Assert.All(test.FormOptions.Where(o => !o.ReportEvents), o => Assert.False(o.LabelFallback));
        await test.ReportLabelAsync(cut, Input(cut), "label-id");

        IElement radio = cut.Find(RadioSelector);
        Assert.False(radio.HasAttribute("aria-labelledby"));
        Assert.Equal("Apple", radio.GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "port: a whitespace aria-label does not name the radio, so the native label still does")]
    [Trait("Requirement", "RAD-EDGE-05")]
    [Trait("Requirement", "RAD-ARIA-04")]
    public async Task Port_a_whitespace_aria_label_does_not_name_the_radio()
    {
        IRenderedComponent<RadioRoot<string>> cut = Context.Render<RadioRoot<string>>(p => p
            .Add(x => x.Value, "a")
            .AddUnmatched("aria-label", "   "));

        Assert.True(test.FormOptions.Last(o => !o.ReportEvents).LabelFallback);
        await test.ReportLabelAsync(cut, Input(cut), "the-label");

        IElement radio = cut.Find(RadioSelector);
        Assert.Equal("the-label", radio.GetAttribute("aria-labelledby"));
        Assert.Equal("   ", radio.GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "uses aria-disabled instead of HTML disabled")]
    [Trait("Requirement", "RAD-ARIA-06")]
    [Trait("Requirement", "RAD-DOM-14")]
    [Trait("Requirement", "RAD-API-02")]
    public void Uses_aria_disabled_instead_of_HTML_disabled()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<RadioRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(RadioRoot<string>.Value), "a");
            builder.AddComponentParameter(2, nameof(RadioRoot<string>.Disabled), true);
            builder.AddAttribute(3, "data-testid", "radio");
            builder.CloseComponent();
        }));

        IElement radio = cut.Find("[data-testid=radio]");
        Assert.False(radio.HasAttribute("disabled"));
        Assert.Equal("true", radio.GetAttribute("aria-disabled"));
        Assert.True(cut.Find("input[type=radio]").HasAttribute("disabled"));
    }

    // RadioRoot.test.tsx "prop: disabled" (L2 half): a disabled radio in a field stays programmatically focusable, and its
    // focus does not publish the field's focused flag; the browser's focus is the L4 half.
    [Fact(DisplayName = "does not acquire the field focused state when focused programmatically")]
    [Trait("Requirement", "RAD-FOC-03")]
    [Trait("Requirement", "RAD-FOC-01")]
    [Trait("Requirement", "RAD-EDGE-03")]
    public async Task Does_not_acquire_the_field_focused_state_when_focused_programmatically()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "field")
            .AddChildContent<RadioRoot<string>>(r => r
                .Add(x => x.Value, "a")
                .Add(x => x.Disabled, true)
                .AddUnmatched("data-testid", "radio")));

        IElement radio = cut.Find("[data-testid=radio]");
        Assert.Equal("-1", radio.GetAttribute("tabindex"));
        await radio.TriggerEventAsync("onfocus", new FocusEventArgs());

        Assert.False(cut.Find("[data-testid=field]").HasAttribute("data-focused"));
    }

    // RadioRoot.test.tsx "prop: readOnly" (L2 half).
    [Fact(DisplayName = "acquires the field focused state on focus")]
    [Trait("Requirement", "RAD-FOC-03")]
    [Trait("Requirement", "RAD-API-03")]
    [Trait("Requirement", "RAD-EDGE-03")]
    [Trait("Requirement", "RAD-EDGE-04")]
    public async Task Acquires_the_field_focused_state_on_focus()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "field")
            .AddChildContent<RadioRoot<string>>(r => r
                .Add(x => x.Value, "a")
                .Add(x => x.ReadOnly, true)
                .AddUnmatched("data-testid", "radio")));

        // RAD-EDGE-04: read-only keeps the radio focusable.
        IElement radio = cut.Find("[data-testid=radio]");
        Assert.Equal("0", radio.GetAttribute("tabindex"));
        await radio.TriggerEventAsync("onfocus", new FocusEventArgs());

        Assert.Equal(string.Empty, cut.Find("[data-testid=field]").GetAttribute("data-focused"));

        // RAD-FOC-03: standalone, the radio's own blur releases the flag (design point R4, pending the user).
        await test.BlurAsync(cut, cut.FindComponent<RadioRoot<string>>().Instance.Element);
        Assert.False(cut.Find("[data-testid=field]").HasAttribute("data-focused"));
        Assert.False(cut.Find("[data-testid=field]").HasAttribute("data-touched"));
    }

    // --- Port titles for the rows without a Base UI test ------------------------------------------------------------

    [Fact(DisplayName = "port: the hidden input is visually hidden by the input style with a name, and by the fixed style without one")]
    [Trait("Requirement", "RAD-DOM-12")]
    [Trait("Requirement", "RAD-DOM-08")]
    public void Port_the_hidden_input_is_visually_hidden()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .AddChildContent(RadioTestContext.StringRadios("a")));

        IElement input = cut.Find("input[type=radio]");
        Assert.Equal("clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0", input.GetAttribute("style"));
        Assert.Equal("true", input.GetAttribute("aria-hidden"));
        Assert.Equal("-1", input.GetAttribute("tabindex"));

        cut.Render(p => p.Add(x => x.Name, "fruit"));

        input = cut.Find("input[type=radio]");
        Assert.Equal("clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute", input.GetAttribute("style"));
        Assert.Equal("fruit", input.GetAttribute("name"));
    }

    [Fact(DisplayName = "port: the hidden input never carries aria-describedby or aria-invalid")]
    [Trait("Requirement", "RAD-DOM-13")]
    public void Port_the_hidden_input_never_carries_aria_describedby_or_aria_invalid()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Invalid, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioGroup<string>>(0);
                builder.AddComponentParameter(1, "ChildContent", RadioTestContext.StringRadios("a"));
                builder.CloseComponent();
                builder.OpenComponent<FieldDescription>(2);
                builder.CloseComponent();
            }));

        IElement radio = cut.Find(RadioSelector);
        Assert.NotNull(radio.GetAttribute("aria-describedby"));
        Assert.Equal("true", radio.GetAttribute("aria-invalid"));

        IElement input = cut.Find("input[type=radio]");
        Assert.False(input.HasAttribute("aria-describedby"));
        Assert.False(input.HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "port: rendering an indicator outside a radio throws a descriptive error")]
    [Trait("Requirement", "RAD-EDGE-01")]
    [Trait("Requirement", "RAD-STR-02")]
    public void Port_rendering_an_indicator_outside_a_radio_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<RadioIndicator>());

        Assert.Equal("Blazix: The `RadioRoot` context is missing. Radio parts must be placed within `<RadioRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: a standalone radio is checked when its value is the empty string, and a click never changes it")]
    [Trait("Requirement", "RAD-STA-01")]
    [Trait("Requirement", "RAD-STA-02")]
    [Trait("Requirement", "RAD-EDGE-03")]
    [Trait("Requirement", "RAD-STR-03")]
    public async Task Port_a_standalone_radio_is_static()
    {
        IRenderedComponent<RadioRoot<string>> empty = Context.Render<RadioRoot<string>>(p => p.Add(x => x.Value, string.Empty));
        IRenderedComponent<RadioRoot<string>> other = Context.Render<RadioRoot<string>>(p => p.Add(x => x.Value, "a"));

        Assert.Equal("true", empty.Find(RadioSelector).GetAttribute("aria-checked"));
        Assert.Equal(string.Empty, empty.Find(RadioSelector).GetAttribute("data-composite-item-active"));
        Assert.Equal("false", other.Find(RadioSelector).GetAttribute("aria-checked"));

        // RAD-STR-03: a plain element with the button writer's tabindex and no composite registration.
        Assert.Equal("0", other.Find(RadioSelector).GetAttribute("tabindex"));
        Assert.Empty(test.CompositeModule.Invocations["attach"]);

        // RAD-STA-02: the request changes nothing, and the input is restored (Server) or the click stopped (WebAssembly).
        await test.ToggleAsync(other, 1);
        Assert.Equal("false", other.Find(RadioSelector).GetAttribute("aria-checked"));
        Assert.Contains("\"checkedRestores\":1", test.DomUpdates[^1], StringComparison.Ordinal);
        Assert.Equal("stop", await test.DecideToggleAsync(other, 1));

        // A click on the checked standalone radio is not a change.
        Assert.Equal("run", await test.DecideToggleAsync(empty, 0));
        Assert.Equal("true", empty.Find(RadioSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "port: a standalone radio's activation only touches its field")]
    [Trait("Requirement", "RAD-EVT-02")]
    [Trait("Requirement", "RAD-STA-02")]
    [Trait("Requirement", "RAD-FRM-05")]
    public async Task Port_a_standalone_radios_activation_only_touches_its_field()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "field")
            .AddChildContent<RadioRoot<string>>(r => r.Add(x => x.Value, "a")));

        Assert.False(cut.Find("[data-testid=field]").HasAttribute("data-touched"));
        await test.ToggleAsync(cut, 0);

        Assert.Equal(string.Empty, cut.Find("[data-testid=field]").GetAttribute("data-touched"));
        Assert.Equal("false", cut.Find(RadioSelector).GetAttribute("aria-checked"));

        // RAD-ARIA-08: a standalone radio has no access to the group's validation.
        Assert.False(cut.Find(RadioSelector).HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "port: a radio whose value is null ignores its input entirely")]
    [Trait("Requirement", "RAD-EVT-03")]
    [Trait("Requirement", "RDG-EVT-07")]
    public async Task Port_a_radio_whose_value_is_null_ignores_its_input()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "field")
            .AddChildContent<RadioRoot<string?>>(r => r.Add(x => x.Value, (string?)null)));

        await test.ToggleAsync(cut, 0);

        // No request, no touched write; the input is re-synchronized.
        Assert.False(cut.Find("[data-testid=field]").HasAttribute("data-touched"));
        Assert.Contains("\"checkedRestores\":1", test.DomUpdates[^1], StringComparison.Ordinal);
    }

    // --- Twins -------------------------------------------------------------------------------------------------------

    [Fact(DisplayName = "port: the root's button behavior is not composite, suppresses Enter and bridges clicks to the hidden input")]
    [Trait("Requirement", "RAD-KEY-02")]
    [Trait("Requirement", "RAD-PTR-01")]
    [Trait("Requirement", "RAD-API-05")]
    [Trait("Requirement", "RAD-KEY-01")]
    public void Port_the_roots_button_behavior_suppresses_Enter_and_bridges_clicks()
    {
        IRenderedComponent<RadioRoot<string>> cut = Context.Render<RadioRoot<string>>(p => p.Add(x => x.Value, "a"));

        Assert.Equal(
            """{"disabled":false,"native":false,"composite":false,"hiddenInput":true,"suppressEnter":true,"reportInteractionType":false,"developmentChecks":true}""",
            Assert.Single(test.ButtonModule.Invocations["attach"]).Arguments[1]);
        Assert.Equal(cut.Instance.Element, Assert.Single(test.ButtonModule.Invocations["attach"]).Arguments[0]);
    }

    [Fact(DisplayName = "port: a read-only radio's button behavior refuses toggles of the hidden input")]
    [Trait("Requirement", "RAD-PTR-07")]
    [Trait("Requirement", "RAD-KEY-06")]
    [Trait("Requirement", "RAD-API-03")]
    public void Port_a_read_only_radios_button_behavior_refuses_toggles()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.ReadOnly, true)
            .AddChildContent(RadioTestContext.StringRadios("a")));

        Assert.Contains("\"readOnly\":true", test.ButtonAttachOptions(), StringComparison.Ordinal);
        Assert.True(cut.Find("input[type=radio]").HasAttribute("readonly"));
        Assert.Equal(string.Empty, cut.Find(RadioSelector).GetAttribute("data-readonly"));

        // RAD-ARIA-09: never aria-readonly or aria-required on the radio.
        Assert.False(cut.Find(RadioSelector).HasAttribute("aria-readonly"));
        Assert.False(cut.Find(RadioSelector).HasAttribute("aria-required"));
    }

    [Fact(DisplayName = "port: the hidden input's checked property is handed to the dom primitive after the first render")]
    [Trait("Requirement", "RAD-DOM-09")]
    [Trait("Requirement", "RAD-ARIA-02")]
    public async Task Port_the_hidden_inputs_checked_property_is_handed_to_the_dom_primitive()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "a")
            .AddChildContent(RadioTestContext.StringRadios("a", "b")));

        IReadOnlyList<IElement> inputs = RadioTestContext.Inputs(cut);
        Assert.True(inputs[0].HasAttribute("checked"));
        Assert.False(inputs[1].HasAttribute("checked"));
        Assert.Equal(
            ["""{"checked":true,"liveRegion":false}""", """{"checked":false,"liveRegion":false}"""],
            test.DomModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!));

        await test.ToggleAsync(cut, 1);

        // The rendered attribute stays frozen; both inputs' handles get their new value.
        inputs = RadioTestContext.Inputs(cut);
        Assert.True(inputs[0].HasAttribute("checked"));
        Assert.False(inputs[1].HasAttribute("checked"));
        Assert.Equal(
            ["""{"checked":false,"liveRegion":false}""", """{"checked":true,"liveRegion":false}"""],
            test.DomUpdates.Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = "port: a click on the selected radio is not a change")]
    [Trait("Requirement", "RDG-EVT-06")]
    [Trait("Requirement", "RAD-PTR-04")]
    public async Task Port_a_click_on_the_selected_radio_is_not_a_change()
    {
        List<string> calls = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "a")
            .Add(x => x.OnValueChange, args => calls.Add(args.Value))
            .AddChildContent(RadioTestContext.StringRadios("a", "b")));
        int domUpdates = test.DomUpdates.Count;

        Assert.Equal("run", await test.DecideToggleAsync(cut, 0));
        await test.ToggleAsync(cut, 0);

        Assert.Empty(calls);
        Assert.Equal(domUpdates, test.DomUpdates.Count);
        Assert.Equal("true", RadioTestContext.Radios(cut)[0].GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "port: inside a group the radio is a composite item whose tabindex the composite writes")]
    [Trait("Requirement", "RAD-STR-03")]
    [Trait("Requirement", "RAD-FOC-01")]
    [Trait("Requirement", "RAD-DOM-02")]
    [Trait("Requirement", "RAD-DOM-16")]
    public void Port_inside_a_group_the_radio_is_a_composite_item()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "b")
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioRoot<string>>(0);
                builder.AddComponentParameter(1, nameof(RadioRoot<string>.Value), "a");
                builder.CloseComponent();
                builder.OpenComponent<RadioRoot<string>>(2);
                builder.AddComponentParameter(3, nameof(RadioRoot<string>.Value), "b");
                builder.CloseComponent();
                builder.OpenComponent<RadioRoot<string>>(4);
                builder.AddComponentParameter(5, nameof(RadioRoot<string>.Value), "c");
                builder.AddAttribute(6, "tabindex", "3");
                builder.CloseComponent();
            }));

        IReadOnlyList<IElement> radios = RadioTestContext.Radios(cut);

        // CMP-STA-15: -1 on every item under the handover (the checked one's 0 comes with the first publication); a consumer
        // tabindex wins at the merge and travels in the registration.
        Assert.Equal(["-1", "-1", "3"], radios.Select(r => r.GetAttribute("tabindex")));
        Assert.Null(radios[0].GetAttribute("data-composite-item-active"));
        Assert.Equal(string.Empty, radios[1].GetAttribute("data-composite-item-active"));

        System.Text.Json.JsonElement items = System.Text.Json.JsonDocument.Parse(test.LastCompositeOptions).RootElement.GetProperty("items");
        Assert.Equal(radios.Select(r => r.Id), items.EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        Assert.Equal("3", items[2].GetProperty("tabIndex").GetString());
    }

    [Fact(DisplayName = "port: a radio's change report reaches the group as one request and one state change")]
    [Trait("Requirement", "RAD-EVT-01")]
    [Trait("Requirement", "RDG-EVT-01")]
    [Trait("Requirement", "RDG-EVT-02")]
    [Trait("Requirement", "RDG-EVT-03")]
    public async Task Port_a_radios_change_report_reaches_the_group_as_one_request()
    {
        List<ValueChangeEventArgs<string>> calls = [];
        List<bool> checkedStates = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.OnValueChange, args => calls.Add(args))
            .AddChildContent<RadioRoot<string>>(r => r
                .Add(x => x.Value, "a")
                .Add(x => x.ClassValue, state =>
                {
                    checkedStates.Add(state.Checked);
                    return null;
                })));
        int renders = checkedStates.Count;

        await test.ToggleAsync(cut, 0, shiftKey: true);

        ValueChangeEventArgs<string> call = Assert.Single(calls);
        Assert.Equal("a", call.Value);
        Assert.Equal(EventReason.None, call.Details.Reason);
        Assert.Equal("click", call.Details.Event.Type);
        Assert.True(call.Details.Event.ShiftKey);
        Assert.Null(call.Details.Trigger);
        Assert.Equal([true], checkedStates.Skip(renders));
    }

    [Fact(DisplayName = "port: a consumer onkeydown runs once per key through one gate, and never while disabled")]
    [Trait("Requirement", "RAD-KEY-06")]
    [Trait("Requirement", "RDG-API-12")]
    [Trait("Requirement", "CMP-EVT-12")]
    public async Task Port_a_consumer_onkeydown_runs_once_per_key_through_one_gate()
    {
        List<string> keys = [];
        bool vetoArrows = false;
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent<RadioRoot<string>>(r => r
            .Add(x => x.Value, "a")
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
            {
                keys.Add(e.Key);
                if (vetoArrows && e.Key.StartsWith("Arrow", StringComparison.Ordinal))
                {
                    e.PreventPartHandler();
                }
            }))));
        IElement radio = cut.Find(RadioSelector);
        string id = radio.Id!;

        // The handled-key report runs it on Server: the composite asks for it while the radio has a handler.
        Assert.Contains("\"reportHandledKeys\":true", test.LastCompositeOptions, StringComparison.Ordinal);

        // WebAssembly: the composite's navigate decision runs it for an arrow; a veto leaves the key to Blazor, which skips it.
        vetoArrows = true;
        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowDown", id));
        await radio.TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });
        Assert.Equal(["ArrowDown"], keys);

        // Space: Blazor's dispatch reaches the button gate, which runs it once.
        await cut.Find(RadioSelector).TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = " ", Code = "Space" });
        Assert.Equal(["ArrowDown", " "], keys);

        // RAD-KEY-06: while disabled neither gate runs it.
        cut.FindComponent<RadioRoot<string>>().Render(r => r.Add(x => x.Disabled, true));
        Assert.Equal("run", await test.DecideNavigateAsync(cut, "ArrowDown", id));
        Assert.Equal(["ArrowDown", " "], keys);
    }

    [Fact(DisplayName = "port: a consumer onfocus that prevents the part handler vetoes the focus-driven selection")]
    [Trait("Requirement", "RAD-FOC-05")]
    public async Task Port_a_consumer_onfocus_that_prevents_the_part_handler_vetoes_the_selection()
    {
        bool veto = true;
        int focuses = 0;
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent<RadioRoot<string>>(r => r
            .Add(x => x.Value, "a")
            .AddUnmatched("onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, e =>
            {
                focuses++;
                if (veto)
                {
                    e.PreventPartHandler();
                }
            }))));
        IElement radio = cut.Find(RadioSelector);

        // On WebAssembly Blazor dispatches focus before the composite's focusin asks the decision.
        await radio.TriggerEventAsync("onfocus", new FocusEventArgs());
        Assert.Equal("stop", await test.DecideSelectAsync(cut, radio.Id!));

        veto = false;
        await cut.Find(RadioSelector).TriggerEventAsync("onfocus", new FocusEventArgs());
        Assert.Equal("run", await test.DecideSelectAsync(cut, radio.Id!));
        Assert.Equal(2, focuses);
    }

    private static ElementReference Input<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        return new ElementReference(cut.Find("input[type=radio]").GetAttribute("blazor:elementReference")!);
    }
}
