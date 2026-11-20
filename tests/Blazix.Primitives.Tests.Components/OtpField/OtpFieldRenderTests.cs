using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

// OTPFieldRoot.test.tsx and OTPFieldInput.test.tsx, the titles of what the parts render (issue 278, M6 track 7): the value
// split across the slots, the hidden input, the slot attributes, the labels and the development warnings. A first render in
// bUnit is the prerendered markup: no module report has arrived, so the slots carry their render-order guesses
// (OTP-EDGE-10).
/// <summary>The rendered DOM of a one-time code field.</summary>
public sealed class OtpFieldRenderTests : IDisposable
{
    private readonly OtpFieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "splits the default value across inputs")]
    [Trait("Requirement", "OTP-STA-06")]
    [Trait("Requirement", "OTP-STA-11")]
    [Trait("Requirement", "OTP-DOM-08")]
    public void Splits_the_default_value_across_inputs()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.DefaultValue, "12a34b56"));

        Assert.Equal(["1", "2", "3", "4", "5", "6"], SlotValues(cut));
    }

    [Fact(DisplayName = "clamps an overlong default value to the rendered slot count")]
    [Trait("Requirement", "OTP-API-03")]
    [Trait("Requirement", "OTP-DOM-14")]
    [Trait("Requirement", "OTP-DOM-22")]
    public void Clamps_an_overlong_default_value_to_the_rendered_slot_count()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.DefaultValue, "12a34b56c7").Add(r => r.Name, "otp"));
        IReadOnlyList<IElement> inputs = Slots(cut);

        Assert.Equal(["1", "2", "3", "4", "5", "6"], SlotValues(cut));
        Assert.Equal("6", inputs[0].GetAttribute("maxlength"));
        Assert.All(inputs.Skip(1), input => Assert.False(input.HasAttribute("maxlength")));
        Assert.Equal("123456", cut.Find("input[name=\"otp\"]").GetAttribute("value"));
    }

    [Fact(DisplayName = "assigns slot indexes from render order when omitted")]
    [Trait("Requirement", "OTP-STR-03")]
    public void Assigns_slot_indexes_from_render_order_when_omitted()
    {
        IRenderedComponent<OtpFieldRoot> cut = Context.Render<OtpFieldRoot>(p => p
            .Add(r => r.Length, 3)
            .Add(r => r.DefaultValue, "123")
            .AddChildContent(OtpFieldTestContext.Slots(3)));

        Assert.Equal(["1", "2", "3"], SlotValues(cut));
    }

    [Fact(DisplayName = "supports grouped layouts without affecting slot counting")]
    [Trait("Requirement", "OTP-STR-01")]
    [Trait("Requirement", "OTP-STR-03")]
    [Trait("Requirement", "OTP-STR-05")]
    [Trait("Requirement", "OTP-ARIA-01")]
    [Trait("Requirement", "OTP-DOM-27")]
    public void Supports_grouped_layouts_without_affecting_slot_counting()
    {
        IRenderedComponent<OtpFieldRoot> cut = Context.Render<OtpFieldRoot>(p => p
            .Add(r => r.Length, 6)
            .Add(r => r.DefaultValue, "123456")
            .AddChildContent(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "data-testid", "first-group");
                builder.AddContent(2, OtpFieldTestContext.Slots(3));
                builder.CloseElement();
                builder.OpenComponent<OtpFieldSeparator>(3);
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(content => content.AddContent(0, "-")));
                builder.CloseComponent();
                builder.OpenElement(5, "div");
                builder.AddAttribute(6, "data-testid", "second-group");
                builder.AddContent(7, OtpFieldTestContext.Slots(3));
                builder.CloseElement();
            }));

        IElement root = cut.Find("[role=\"group\"]");
        Assert.NotNull(root.QuerySelector("[data-testid=\"first-group\"]"));
        Assert.NotNull(root.QuerySelector("[data-testid=\"second-group\"]"));
        IElement separator = cut.Find("[role=\"separator\"]");
        Assert.Equal("-", separator.TextContent);
        Assert.Equal("horizontal", separator.GetAttribute("aria-orientation"));
        Assert.Equal("horizontal", separator.GetAttribute("data-orientation"));
        Assert.Equal(["1", "2", "3", "4", "5", "6"], SlotValues(cut));

        // OTP-STR-01: the hidden input is the root's next sibling, never its descendant.
        Assert.Equal("INPUT", root.NextElementSibling!.TagName);
        Assert.Null(root.QuerySelector("[aria-hidden=\"true\"]"));
    }

    [Fact(DisplayName = "updates the rendered value in controlled mode")]
    [Trait("Requirement", "OTP-API-02")]
    public void Updates_the_rendered_value_in_controlled_mode()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Value, "123456"));
        Assert.Equal("123456", string.Concat(SlotValues(cut)));

        cut.Render(p => p.Add(r => r.Value, "654321"));

        // OTP-DOM-08 (C# → JS): after attach the module writes slot i from the value C# sends.
        Assert.Equal("654321", host.OtpOptions[^1].Value);
    }

    [Fact(DisplayName = "supports alphabetic values when set to `alpha`")]
    [Trait("Requirement", "OTP-API-07")]
    [Trait("Requirement", "OTP-STA-06")]
    public void Supports_alphabetic_values_when_set_to_alpha()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.DefaultValue, "1a2b3Cd4").Add(r => r.ValidationType, OtpFieldValidationType.Alpha));

        Assert.Equal(["a", "b", "C", "d", "", ""], SlotValues(cut));
    }

    [Fact(DisplayName = "applies single-character validation to each visible slot")]
    [Trait("Requirement", "OTP-DOM-16")]
    [Trait("Requirement", "OTP-STA-05")]
    public void Applies_single_character_validation_to_each_visible_slot()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.ValidationType, OtpFieldValidationType.Alphanumeric));

        Assert.Equal("[a-zA-Z0-9]{1}", Slots(cut)[0].GetAttribute("pattern"));
    }

    [Fact(DisplayName = "renders the hidden validation input with an alphanumeric pattern during SSR")]
    [Trait("Requirement", "OTP-DOM-24")]
    [Trait("Requirement", "OTP-EDGE-10")]
    public void Renders_the_hidden_validation_input_with_an_alphanumeric_pattern_during_ssr()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Name, "otp").Add(r => r.Required, true).Add(r => r.ValidationType, OtpFieldValidationType.Alphanumeric));

        Assert.Equal("[a-zA-Z0-9]{6}", cut.Find("input[name=\"otp\"]").GetAttribute("pattern"));
    }

    [Fact(DisplayName = "omits the hidden validation pattern when set to `none`")]
    [Trait("Requirement", "OTP-DOM-24")]
    [Trait("Requirement", "OTP-API-07")]
    public void Omits_the_hidden_validation_pattern_when_set_to_none()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Name, "otp").Add(r => r.ValidationType, OtpFieldValidationType.None));

        Assert.False(cut.Find("input[name=\"otp\"]").HasAttribute("pattern"));
    }

    [Fact(DisplayName = "allows a custom inputMode when validation is set to `none`")]
    [Trait("Requirement", "OTP-API-09")]
    [Trait("Requirement", "OTP-DOM-10")]
    [Trait("Requirement", "OTP-DOM-23")]
    public void Allows_a_custom_input_mode_when_validation_is_set_to_none()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Name, "otp").Add(r => r.ValidationType, OtpFieldValidationType.None).Add(r => r.InputMode, InputMode.Numeric));

        Assert.Equal("numeric", Slots(cut)[0].GetAttribute("inputmode"));
        Assert.Equal("numeric", cut.Find("input[name=\"otp\"]").GetAttribute("inputmode"));
    }

    [Fact(DisplayName = "allows overriding the built-in inputMode when needed")]
    [Trait("Requirement", "OTP-API-09")]
    public void Allows_overriding_the_built_in_input_mode_when_needed()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Name, "otp").Add(r => r.ValidationType, OtpFieldValidationType.Numeric).Add(r => r.InputMode, InputMode.Text));

        Assert.Equal("text", Slots(cut)[0].GetAttribute("inputmode"));
        Assert.Equal("text", cut.Find("input[name=\"otp\"]").GetAttribute("inputmode"));
    }

    [Fact(DisplayName = "port: the default inputMode is derived from the validation type, and absent for `none` (OTP-API-09)")]
    [Trait("Requirement", "OTP-API-09")]
    [Trait("Requirement", "OTP-DOM-10")]
    public void Port_the_default_input_mode_is_derived_from_the_validation_type()
    {
        IRenderedComponent<OtpFieldRoot> numeric = host.RenderSix();
        IRenderedComponent<OtpFieldRoot> alpha = host.RenderSix(p => p.Add(r => r.ValidationType, OtpFieldValidationType.Alpha));
        IRenderedComponent<OtpFieldRoot> none = host.RenderSix(p => p.Add(r => r.ValidationType, OtpFieldValidationType.None));

        Assert.Equal("numeric", Slots(numeric)[0].GetAttribute("inputmode"));
        Assert.Equal("text", Slots(alpha)[0].GetAttribute("inputmode"));
        Assert.False(Slots(none)[0].HasAttribute("inputmode"));
        Assert.False(Slots(none)[0].HasAttribute("pattern"));
    }

    [Fact(DisplayName = "applies the default autocomplete to the first slot only")]
    [Trait("Requirement", "OTP-API-11")]
    [Trait("Requirement", "OTP-DOM-11")]
    public void Applies_the_default_autocomplete_to_the_first_slot_only()
    {
        IReadOnlyList<IElement> inputs = Slots(host.RenderSix());

        Assert.Equal("one-time-code", inputs[0].GetAttribute("autocomplete"));
        Assert.Equal("off", inputs[1].GetAttribute("autocomplete"));
    }

    [Fact(DisplayName = "allows overriding the autocomplete attribute")]
    [Trait("Requirement", "OTP-API-11")]
    [Trait("Requirement", "OTP-DOM-23")]
    public void Allows_overriding_the_autocomplete_attribute()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.AutoComplete, "off").Add(r => r.Name, "otp"));

        Assert.Equal("off", Slots(cut)[0].GetAttribute("autocomplete"));
        Assert.Equal("off", cut.Find("input[name=\"otp\"]").GetAttribute("autocomplete"));
    }

    [Fact(DisplayName = "port: every slot renders autocorrect off, spellcheck false, its enterkeyhint and no name (OTP-DOM-12, OTP-DOM-13, OTP-FRM-02)")]
    [Trait("Requirement", "OTP-DOM-12")]
    [Trait("Requirement", "OTP-DOM-13")]
    [Trait("Requirement", "OTP-FRM-02")]
    public void Port_every_slot_renders_autocorrect_spellcheck_enterkeyhint_and_no_name()
    {
        IReadOnlyList<IElement> inputs = Slots(host.RenderSix(p => p.Add(r => r.Name, "otp")));

        Assert.All(inputs, input =>
        {
            Assert.Equal("off", input.GetAttribute("autocorrect"));
            Assert.Equal("false", input.GetAttribute("spellcheck"));
            Assert.False(input.HasAttribute("name"));
        });
        Assert.Equal(["next", "next", "next", "next", "next", "done"], inputs.Select(input => input.GetAttribute("enterkeyhint")));
    }

    [Fact(DisplayName = "renders password slot inputs when enabled")]
    [Trait("Requirement", "OTP-API-10")]
    [Trait("Requirement", "OTP-DOM-09")]
    public void Renders_password_slot_inputs_when_enabled()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.DefaultValue, "123").Add(r => r.Mask, true));

        Assert.Equal(6, cut.FindAll("input[type=\"password\"]").Count);
    }

    [Fact(DisplayName = "allows overriding the input type on individual slots")]
    [Trait("Requirement", "OTP-DOM-09")]
    [Trait("Requirement", "OTP-API-23")]
    [Trait("Requirement", "OTP-DOM-19")]
    public void Allows_overriding_the_input_type_on_individual_slots()
    {
        IRenderedComponent<OtpFieldRoot> cut = Context.Render<OtpFieldRoot>(p => p
            .Add(r => r.Length, 1)
            .Add(r => r.Mask, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<OtpFieldInput>(0);
                builder.AddAttribute(1, "type", "tel");
                builder.CloseComponent();
            }));

        Assert.Equal("tel", Slots(cut)[0].GetAttribute("type"));
    }

    [Fact(DisplayName = "renders visible inputs with unique IDs")]
    [Trait("Requirement", "OTP-DOM-07")]
    [Trait("Requirement", "OTP-API-18")]
    [Trait("Requirement", "OTP-DOM-01")]
    [Trait("Requirement", "OTP-EDGE-10")]
    public void Renders_visible_inputs_with_unique_ids()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Id, "verification-code").AddUnmatched("data-testid", "root"));

        IElement root = cut.Find("[data-testid=\"root\"]");
        Assert.Equal(
            ["verification-code", "verification-code-2", "verification-code-3", "verification-code-4", "verification-code-5", "verification-code-6"],
            root.QuerySelectorAll("input").Select(input => input.Id));
        Assert.False(root.HasAttribute("id"));
    }

    [Fact(DisplayName = "renders a hidden validation input with the provided length")]
    [Trait("Requirement", "OTP-DOM-24")]
    [Trait("Requirement", "OTP-DOM-25")]
    [Trait("Requirement", "OTP-EDGE-10")]
    public void Renders_a_hidden_validation_input_with_the_provided_length()
    {
        IElement hidden = host.RenderSix(p => p.Add(r => r.Name, "otp").Add(r => r.Required, true)).Find("input[name=\"otp\"]");

        Assert.Equal("6", hidden.GetAttribute("minlength"));
        Assert.Equal("6", hidden.GetAttribute("maxlength"));
        Assert.Equal(@"\d{6}", hidden.GetAttribute("pattern"));
    }

    [Fact(DisplayName = "renders the hidden validation input with the provided name")]
    [Trait("Requirement", "OTP-STR-01")]
    [Trait("Requirement", "OTP-API-14")]
    [Trait("Requirement", "OTP-DOM-20")]
    [Trait("Requirement", "OTP-FRM-02")]
    public void Renders_the_hidden_validation_input_with_the_provided_name()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Name, "otp"));

        IElement hidden = Assert.Single(cut.FindAll("input[name=\"otp\"]"));
        Assert.Equal("text", hidden.GetAttribute("type"));
        Assert.Equal("true", hidden.GetAttribute("aria-hidden"));
        Assert.Equal("-1", hidden.GetAttribute("tabindex"));
        Assert.False(hidden.HasAttribute("id"));
    }

    [Fact(DisplayName = "renders a fallback hidden input id when name is not provided")]
    [Trait("Requirement", "OTP-DOM-21")]
    public void Renders_a_fallback_hidden_input_id_when_name_is_not_provided()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Id, "verification-code"));

        Assert.NotNull(cut.Find("#verification-code-hidden-input"));
    }

    [Fact(DisplayName = "port: the hidden input is absolutely positioned when named and fixed at the origin when nameless (OTP-DOM-26)")]
    [Trait("Requirement", "OTP-DOM-26")]
    public void Port_the_hidden_input_style_follows_its_name()
    {
        string named = host.RenderSix(p => p.Add(r => r.Name, "otp")).Find("input[aria-hidden]").GetAttribute("style")!;
        string nameless = host.RenderSix().Find("input[aria-hidden]").GetAttribute("style")!;

        Assert.Contains("position: absolute", named, StringComparison.Ordinal);
        Assert.Contains("position: fixed", nameless, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: no hidden input renders while the length is not a positive integer (OTP-STR-01, OTP-EDGE-07)")]
    [Trait("Requirement", "OTP-STR-01")]
    [Trait("Requirement", "OTP-EDGE-07")]
    public void Port_no_hidden_input_renders_while_the_length_is_not_a_positive_integer()
    {
        IRenderedComponent<OtpFieldRoot> zero = Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, 0).Add(r => r.DefaultValue, "12").AddChildContent(OtpFieldTestContext.Slots(1)));
        IRenderedComponent<OtpFieldRoot> negative = Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, -1).AddChildContent(OtpFieldTestContext.Slots(1)));

        Assert.Empty(zero.FindAll("input[aria-hidden]"));
        Assert.Empty(negative.FindAll("input[aria-hidden]"));
        Assert.Equal(string.Empty, Slots(zero)[0].GetAttribute("value"));
        Assert.Equal("0", Slots(zero)[0].GetAttribute("maxlength"));
        Assert.Equal("-1", Slots(negative)[0].GetAttribute("maxlength"));
    }

    [Fact(DisplayName = "sets `data-complete` when all slots are filled")]
    [Trait("Requirement", "OTP-DOM-02")]
    [Trait("Requirement", "OTP-STA-09")]
    [Trait("Requirement", "OTP-STA-14")]
    public void Sets_data_complete_when_all_slots_are_filled()
    {
        IElement root = host.RenderSix(p => p.Add(r => r.DefaultValue, "123456").AddUnmatched("data-testid", "root")).Find("[data-testid=\"root\"]");

        Assert.Equal(string.Empty, root.GetAttribute("data-complete"));
        Assert.Equal(string.Empty, root.GetAttribute("data-filled"));
        Assert.False(root.HasAttribute("data-value"));
        Assert.False(root.HasAttribute("data-length"));
    }

    [Fact(DisplayName = "marks each input as complete when all slots are filled")]
    [Trait("Requirement", "OTP-DOM-17")]
    [Trait("Requirement", "OTP-STA-11")]
    public void Marks_each_input_as_complete_when_all_slots_are_filled()
    {
        Assert.All(Slots(host.RenderSix(p => p.Add(r => r.DefaultValue, "123456"))), input => Assert.Equal(string.Empty, input.GetAttribute("data-complete")));
    }

    [Fact(DisplayName = "port: a slot projects data-length and its own data-filled, never data-value or data-index (OTP-DOM-17, OTP-DOM-18)")]
    [Trait("Requirement", "OTP-DOM-17")]
    [Trait("Requirement", "OTP-DOM-18")]
    public void Port_a_slot_projects_data_length_and_its_own_filled()
    {
        IReadOnlyList<IElement> inputs = Slots(host.RenderSix(p => p.Add(r => r.DefaultValue, "12")));

        Assert.All(inputs, input =>
        {
            Assert.Equal("6", input.GetAttribute("data-length"));
            Assert.False(input.HasAttribute("data-value"));
            Assert.False(input.HasAttribute("data-index"));
        });
        Assert.Equal([true, true, false, false, false, false], inputs.Select(input => input.HasAttribute("data-filled")));
    }

    [Fact(DisplayName = "adds disabled and readonly state attributes to each slot")]
    [Trait("Requirement", "OTP-DOM-17")]
    [Trait("Requirement", "OTP-DOM-03")]
    [Trait("Requirement", "OTP-DOM-15")]
    public void Adds_disabled_and_readonly_state_attributes_to_each_slot()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.Add(r => r.Disabled, true));
        Assert.All(Slots(cut), input =>
        {
            Assert.Equal(string.Empty, input.GetAttribute("data-disabled"));
            Assert.True(input.HasAttribute("disabled"));
        });

        cut.Render(p => p.Add(r => r.Disabled, false).Add(r => r.ReadOnly, true));

        Assert.All(Slots(cut), input =>
        {
            Assert.Equal(string.Empty, input.GetAttribute("data-readonly"));
            Assert.True(input.HasAttribute("readonly"));
        });
    }

    [Fact(DisplayName = "port: the states mirror onto the root, every slot and the hidden input (OTP-DOM-03, OTP-DOM-15, OTP-DOM-22, OTP-API-13, OTP-API-17)")]
    [Trait("Requirement", "OTP-DOM-03")]
    [Trait("Requirement", "OTP-DOM-15")]
    [Trait("Requirement", "OTP-DOM-22")]
    [Trait("Requirement", "OTP-API-13")]
    [Trait("Requirement", "OTP-API-17")]
    public void Port_the_states_mirror_onto_the_root_every_slot_and_the_hidden_input()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p
            .Add(r => r.Required, true)
            .Add(r => r.ReadOnly, true)
            .Add(r => r.Disabled, true)
            .Add(r => r.Form, "external")
            .Add(r => r.Name, "otp"));

        IElement root = cut.Find("[role=\"group\"]");
        Assert.Equal(string.Empty, root.GetAttribute("data-required"));
        Assert.Equal(string.Empty, root.GetAttribute("data-readonly"));
        Assert.Equal(string.Empty, root.GetAttribute("data-disabled"));
        Assert.False(root.HasAttribute("form"));
        foreach (IElement input in cut.FindAll("input"))
        {
            Assert.True(input.HasAttribute("required"));
            Assert.True(input.HasAttribute("readonly"));
            Assert.True(input.HasAttribute("disabled"));
            Assert.Equal("external", input.GetAttribute("form"));
        }
    }

    [Fact(DisplayName = "port: the slot in the tab order is the first empty slot, or the last slot when complete (OTP-FOC-01, OTP-FOC-02)")]
    [Trait("Requirement", "OTP-FOC-01")]
    [Trait("Requirement", "OTP-FOC-02")]
    public void Port_the_slot_in_the_tab_order_is_the_first_empty_slot()
    {
        IRenderedComponent<OtpFieldRoot> partial = host.RenderSix(p => p.Add(r => r.DefaultValue, "12"));
        IRenderedComponent<OtpFieldRoot> complete = host.RenderSix(p => p.Add(r => r.DefaultValue, "123456"));

        Assert.Equal(["-1", "-1", "0", "-1", "-1", "-1"], Slots(partial).Select(input => input.GetAttribute("tabindex")));
        Assert.Equal(["-1", "-1", "-1", "-1", "-1", "0"], Slots(complete).Select(input => input.GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "associates Field.Label with the first slot")]
    [Trait("Requirement", "OTP-FRM-11")]
    [Trait("Requirement", "OTP-API-18")]
    public void Associates_field_label_with_the_first_slot()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<FieldLabel>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Verification code")));
            builder.CloseComponent();
            Six(builder, 2);
        }));

        IElement label = cut.Find("label");
        Assert.Equal(Slots(cut)[0].Id, label.GetAttribute("for"));
    }

    [Fact(DisplayName = "applies the Field description to the group")]
    [Trait("Requirement", "OTP-ARIA-03")]
    [Trait("Requirement", "OTP-ARIA-05")]
    [Trait("Requirement", "OTP-FRM-12")]
    public void Applies_the_field_description_to_the_group()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<FieldLabel>(0);
            builder.AddAttribute(1, "data-testid", "label");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Verification code")));
            builder.CloseComponent();
            builder.OpenComponent<FieldDescription>(3);
            builder.AddAttribute(4, "data-testid", "description");
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Enter the code.")));
            builder.CloseComponent();
            Six(builder, 6, ("aria-describedby", "external-description"));
        }));

        IElement label = cut.Find("[data-testid=\"label\"]");
        IElement description = cut.Find("[data-testid=\"description\"]");
        IElement group = cut.Find("[role=\"group\"]");
        Assert.Equal(label.Id, group.GetAttribute("aria-labelledby"));
        Assert.Equal($"external-description {description.Id}", group.GetAttribute("aria-describedby"));

        // OTP-FRM-12, OTP-ARIA-11: the hidden input carries the field's description ids, not the consumer's.
        Assert.Equal(description.Id, cut.Find("input[aria-hidden]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "applies the Field label to every slot")]
    [Trait("Requirement", "OTP-ARIA-06")]
    [Trait("Requirement", "OTP-ARIA-10")]
    [Trait("Requirement", "OTP-FRM-11")]
    public void Applies_the_field_label_to_every_slot()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<FieldLabel>(0);
            builder.AddAttribute(1, "data-testid", "label");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Verification code")));
            builder.CloseComponent();
            builder.OpenComponent<FieldDescription>(3);
            builder.AddAttribute(4, "data-testid", "description");
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Enter the code.")));
            builder.CloseComponent();
            Six(builder, 6);
        }));

        IElement label = cut.Find("[data-testid=\"label\"]");
        string descriptionId = cut.Find("[data-testid=\"description\"]").Id!;
        Assert.All(Slots(cut), input =>
        {
            Assert.Equal(label.Id, input.GetAttribute("aria-labelledby"));
            Assert.NotEqual(descriptionId, input.GetAttribute("aria-describedby"));
        });
    }

    [Fact(DisplayName = "forwards root `aria-describedby` to the group")]
    [Trait("Requirement", "OTP-ARIA-05")]
    public void Forwards_root_aria_describedby_to_the_group()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.AddUnmatched("aria-describedby", "description-id"));

        Assert.Equal("description-id", cut.Find("[role=\"group\"]").GetAttribute("aria-describedby"));
        Assert.All(Slots(cut), input => Assert.False(input.HasAttribute("aria-describedby")));
    }

    [Fact(DisplayName = "forwards root `aria-labelledby` to the group only")]
    [Trait("Requirement", "OTP-ARIA-02")]
    [Trait("Requirement", "OTP-ARIA-06")]
    public void Forwards_root_aria_labelledby_to_the_group_only()
    {
        IRenderedComponent<OtpFieldRoot> cut = host.RenderSix(p => p.AddUnmatched("aria-labelledby", "label-id"));

        Assert.Equal("label-id", cut.Find("[role=\"group\"]").GetAttribute("aria-labelledby"));
        Assert.All(Slots(cut), input => Assert.NotEqual("label-id", input.GetAttribute("aria-labelledby")));
    }

    [Fact(DisplayName = "port: duplicate ids in the described-by composition are emitted once and whitespace yields none (OTP-EDGE-13)")]
    [Trait("Requirement", "OTP-EDGE-13")]
    public void Port_duplicate_described_by_ids_are_emitted_once()
    {
        IRenderedComponent<OtpFieldRoot> duplicated = host.RenderSix(p => p.AddUnmatched("aria-describedby", " a  b a "));
        IRenderedComponent<OtpFieldRoot> blank = host.RenderSix(p => p.AddUnmatched("aria-describedby", "   "));

        Assert.Equal("a b", duplicated.Find("[role=\"group\"]").GetAttribute("aria-describedby"));
        Assert.False(blank.Find("[role=\"group\"]").HasAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "keeps the shared label on the first slot even if an aria-label is provided")]
    [Trait("Requirement", "OTP-API-21")]
    [Trait("Requirement", "OTP-ARIA-07")]
    [Trait("Requirement", "OTP-ARIA-08")]
    public void Keeps_the_shared_label_on_the_first_slot_even_if_an_aria_label_is_provided()
    {
        IRenderedComponent<OtpFieldRoot> cut = Context.Render<OtpFieldRoot>(p => p
            .Add(r => r.Length, 6)
            .Add(r => r.Id, "verification-code")
            .AddChildContent(builder =>
            {
                for (int i = 0; i < 6; i++)
                {
                    builder.OpenComponent<OtpFieldInput>(0);
                    builder.SetKey(i);
                    if (i < 2)
                    {
                        builder.AddAttribute(1, "aria-label", $"Character {i + 1} of 6");
                    }

                    builder.CloseComponent();
                }
            }));

        IReadOnlyList<IElement> inputs = Slots(cut);
        Assert.False(inputs[0].HasAttribute("aria-label"));
        Assert.Equal("Character 2 of 6", inputs[1].GetAttribute("aria-label"));
        Assert.False(inputs[1].HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "port: a slot's own aria-labelledby wins over the inherited label (OTP-API-22)")]
    [Trait("Requirement", "OTP-API-22")]
    [Trait("Requirement", "OTP-ARIA-06")]
    public void Port_a_slots_own_aria_labelledby_wins_over_the_inherited_label()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<FieldLabel>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Verification code")));
            builder.CloseComponent();
            builder.OpenComponent<OtpFieldRoot>(2);
            builder.AddComponentParameter(3, nameof(OtpFieldRoot.Length), 2);
            builder.AddComponentParameter(4, nameof(OtpFieldRoot.ChildContent), (RenderFragment)(slots =>
            {
                slots.OpenComponent<OtpFieldInput>(0);
                slots.AddAttribute(1, "aria-labelledby", "own-label");
                slots.CloseComponent();
                slots.OpenComponent<OtpFieldInput>(2);
                slots.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        IReadOnlyList<IElement> inputs = Slots(cut);
        Assert.Equal("own-label", inputs[0].GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find("label").Id, inputs[1].GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <OTPField.Root>")]
    [Trait("Requirement", "OTP-EDGE-01")]
    [Trait("Requirement", "OTP-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<OtpFieldInput>());

        Assert.Equal("Blazix: `OtpFieldRootContext` is missing. OtpField parts must be placed within `<OtpFieldRoot>`.", error.Message);
    }

    [Fact(DisplayName = "renders one textbox per slot")]
    [Trait("Requirement", "OTP-STR-02")]
    public void Renders_one_textbox_per_slot()
    {
        Assert.Equal(6, Slots(host.RenderSix()).Count);
    }

    [Fact(DisplayName = "warns when length does not match the rendered input count")]
    [Trait("Requirement", "OTP-EDGE-02")]
    [Trait("Requirement", "OTP-STR-04")]
    public void Warns_when_length_does_not_match_the_rendered_input_count()
    {
        Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, 6).AddChildContent(OtpFieldTestContext.Slots(5)));

        string message = Assert.Single(host.RootLogger.Entries).Message;
        Assert.Contains("Blazix: `<OtpFieldRoot>` `Length` must match the number of rendered `<OtpFieldInput />` parts.", message, StringComparison.Ordinal);
        Assert.Contains("Received `Length=6` but rendered 5 inputs.", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "warns with singular input wording when one input is rendered")]
    [Trait("Requirement", "OTP-EDGE-02")]
    public void Warns_with_singular_input_wording_when_one_input_is_rendered()
    {
        Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, 2).AddChildContent(OtpFieldTestContext.Slots(1)));

        Assert.Contains("Received `Length=2` but rendered 1 input.", Assert.Single(host.RootLogger.Entries).Message, StringComparison.Ordinal);
    }

    // OTPFieldRoot.test.tsx's "%p" cases 0 and -1; 3.7, NaN and Infinity cannot be expressed by an int Length (sheet D
    // contradiction 13; otp-field.md, OTP-EDGE-03's dated note).
    [Theory(DisplayName = "warns when length is not a positive integer (%p)")]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Requirement", "OTP-EDGE-03")]
    [Trait("Requirement", "OTP-EDGE-07")]
    [Trait("Requirement", "OTP-API-01")]
    public void Warns_when_length_is_not_a_positive_integer(int length)
    {
        Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, length));

        Assert.Contains($"Blazix: `<OtpFieldRoot>` `Length` must be a positive integer. Received `Length={length}`.", Assert.Single(host.RootLogger.Entries).Message, StringComparison.Ordinal);
    }

    // UTL-EDGE-11/12: Base UI's `warn` writes each text once per page; the port's set is the scoped OtpFieldDevelopmentLog
    // (granted by the orchestrator 2026-11-19), one per circuit or application.
    [Fact(DisplayName = "port: a warning text is written once per session, across two fields (OTP-EDGE-02, OTP-EDGE-03)")]
    [Trait("Requirement", "OTP-EDGE-02")]
    [Trait("Requirement", "OTP-EDGE-03")]
    public void Port_a_warning_text_is_written_once_per_session()
    {
        Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, 0));
        Context.Render<OtpFieldRoot>(p => p.Add(r => r.Length, 0));

        Assert.Single(host.RootLogger.Entries);
    }

    private static IReadOnlyList<IElement> Slots<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("input:not([aria-hidden])");

    private static string[] SlotValues<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => [.. Slots(cut).Select(input => input.GetAttribute("value") ?? string.Empty)];

    private static void Six(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int sequence, (string Name, string Value)? attribute = null)
    {
        builder.OpenComponent<OtpFieldRoot>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(OtpFieldRoot.Length), 6);
        if (attribute is { } pair)
        {
            builder.AddAttribute(sequence + 2, pair.Name, pair.Value);
        }

        builder.AddComponentParameter(sequence + 3, nameof(OtpFieldRoot.ChildContent), OtpFieldTestContext.Slots(6));
        builder.CloseComponent();
    }
}
