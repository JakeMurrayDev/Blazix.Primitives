using AngleSharp.Dom;
using Microsoft.Extensions.DependencyInjection;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FieldScenarios.FieldScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> rows of docs/components/field.md and the field infrastructure that <c>FieldRoot.test.tsx</c>'s titles
/// prove with Field Root, Label, Control and Description only (PR 1a; the titles that render Field.Error, Field.Validity,
/// Form or another control land with those parts). Titles are Base UI's. The browser's native state is the form module's
/// report, driven by <see cref="FieldTestContext.Module"/>; the browser itself is L4.
/// </summary>
public sealed class FieldRootTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "updates label association when replacing one control with another")]
    [Trait("Requirement", "FLD-STR-03")]
    [Trait("Requirement", "FLD-DOM-06")]
    [Trait("Requirement", "FFI-ARIA-04")]
    public void Updates_label_association_when_replacing_one_control_with_another()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "control-a").Add(x => x.SecondMounted, false));
        IElement label = cut.Find("[data-testid=label]");
        Assert.Equal("control-a", label.GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstMounted, false).Add(x => x.SecondMounted, true).Add(x => x.SecondId, "control-b"));

        Assert.Equal("control-b", cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "drops a stale explicit id when an id-less control replaces the control that owned it")]
    [Trait("Requirement", "FLD-API-20")]
    [Trait("Requirement", "FLD-DOM-06")]
    [Trait("Requirement", "FFI-ALG-07")]
    public void Drops_a_stale_explicit_id_when_an_id_less_control_replaces_the_control_that_owned_it()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "control").Add(x => x.SecondMounted, false));
        Assert.Equal("control", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstMounted, false).Add(x => x.SecondMounted, true));

        string? id = cut.Find("[data-testid=second]").GetAttribute("id");
        Assert.NotEqual("control", id);
        Assert.Equal(id, cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "updates label associations when the control id changes")]
    [Trait("Requirement", "FLD-API-20")]
    [Trait("Requirement", "FLD-DOM-06")]
    public void Updates_label_associations_when_the_control_id_changes()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "control-a").Add(x => x.SecondMounted, false));
        Assert.Equal("control-a", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "control-b").Add(x => x.SecondMounted, false));

        Assert.Equal("control-b", cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "falls back to a generated id when the control id is removed")]
    [Trait("Requirement", "FLD-API-20")]
    [Trait("Requirement", "FLD-DOM-06")]
    [Trait("Requirement", "FLD-DOM-08")]
    [Trait("Requirement", "FFI-FRM-36")]
    public void Falls_back_to_a_generated_id_when_the_control_id_is_removed()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "control-a").Add(x => x.SecondMounted, false));
        Assert.Equal("control-a", cut.Find("[data-testid=label]").GetAttribute("for"));
        Assert.Equal("control-a", cut.Find("[data-testid=first]").GetAttribute("id"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, (string?)null));

        string? updated = cut.Find("[data-testid=first]").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(updated));
        Assert.NotEqual("control-a", updated);
        Assert.Equal(updated, cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    // React.Activity destroys the control's effects but keeps its element; Blazor keeps no element of a removed part, so
    // the port takes the control away (its Enabled parameter, as the Collapsible re-attach fixture does) and asserts the
    // rule the title guards: the scope keeps its selection when no claim remains (FFI-FRM-35).
    [Fact(DisplayName = "keeps an explicit control id while the subtree is hidden")]
    [Trait("Requirement", "FLD-FRM-16")]
    [Trait("Requirement", "FFI-FRM-35")]
    public void Keeps_an_explicit_control_id_while_the_subtree_is_hidden()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldLabel>(l => l.AddUnmatched("data-testid", "label"))
            .AddChildContent<FieldControl>(c => c.AddUnmatched("id", "email").AddUnmatched("data-testid", "control")));
        Assert.Equal("email", cut.Find("[data-testid=control]").GetAttribute("id"));
        Assert.Equal("email", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.FindComponent<FieldControl>().Render(p => p.Add(x => x.Enabled, false));

        Assert.Equal("email", cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "should add data-disabled style hook to all components")]
    [Trait("Requirement", "FLD-API-02")]
    [Trait("Requirement", "FLD-API-10")]
    [Trait("Requirement", "FLD-STA-02")]
    [Trait("Requirement", "FLD-STA-03")]
    [Trait("Requirement", "FLD-STA-04")]
    [Trait("Requirement", "FLD-DOM-02")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FFI-STA-11")]
    public void Should_add_data_disabled_style_hook_to_all_components()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "field").Add(x => x.Disabled, true)
            .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control"))
            .AddChildContent<FieldLabel>(l => l.AddUnmatched("data-testid", "label"))
            .AddChildContent<FieldDescription>(d => d.AddUnmatched("data-testid", "message")));

        foreach (string part in new[] { "field", "control", "label", "message" })
        {
            Assert.Equal(string.Empty, cut.Find($"[data-testid={part}]").GetAttribute("data-disabled"));
        }
    }

    [Fact(DisplayName = "keeps an explicitly invalid field marked invalid while disabled")]
    [Trait("Requirement", "FLD-API-03")]
    [Trait("Requirement", "FLD-STA-09")]
    [Trait("Requirement", "FLD-ARIA-03")]
    [Trait("Requirement", "FLD-FRM-11")]
    [Trait("Requirement", "FFI-STA-02")]
    public void Keeps_an_explicitly_invalid_field_marked_invalid_while_disabled()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "field").Add(x => x.Disabled, true).Add(x => x.Invalid, true)
            .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control"))
            .AddChildContent<FieldLabel>(l => l.AddUnmatched("data-testid", "label"))
            .AddChildContent<FieldDescription>(d => d.AddUnmatched("data-testid", "description")));

        foreach (string part in new[] { "field", "control", "label", "description" })
        {
            Assert.Equal(string.Empty, cut.Find($"[data-testid={part}]").GetAttribute("data-invalid"));
        }

        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "accepts synchronous and async validators with no return value")]
    [Trait("Requirement", "FLD-API-04")]
    [Trait("Requirement", "FFI-API-01")]
    public void Accepts_synchronous_and_async_validators_with_no_return_value()
    {
        IRenderedComponent<FieldRoot> sync = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "sync").Add(x => x.Validate, _ => ValueTask.FromResult<FieldValidationResult?>(null)));
        IRenderedComponent<FieldRoot> async = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "async").Add(x => x.Validate, async _ => { await Task.Yield(); return null; }));

        Assert.NotNull(sync.Find("[data-testid=sync]"));
        Assert.NotNull(async.Find("[data-testid=async]"));
    }

    [Fact(DisplayName = "should not mark invalid if `valueMissing` is the only error and not yet dirtied")]
    [Trait("Requirement", "FLD-FRM-05")]
    [Trait("Requirement", "FFI-FRM-06")]
    public void Should_not_mark_invalid_if_valueMissing_is_the_only_error_and_not_yet_dirtied()
    {
        IRenderedComponent<FieldRoot> cut = RenderRequired();

        host.Module.Control.Blur(cut, Native.ValueMissing);

        IElement control = cut.Find("[data-testid=control]");
        Assert.False(control.HasAttribute("data-invalid"));
        Assert.False(control.HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "should mark invalid if `valueMissing` is the only error and dirtied")]
    [Trait("Requirement", "FLD-FRM-05")]
    [Trait("Requirement", "FFI-FRM-06")]
    public void Should_mark_invalid_if_valueMissing_is_the_only_error_and_dirtied()
    {
        IRenderedComponent<FieldRoot> cut = RenderRequired();

        host.Module.Control.Input(cut, "a", Native.Valid);
        host.Module.Control.Input(cut, string.Empty, Native.ValueMissing);
        host.Module.Control.Blur(cut);

        IElement control = cut.Find("[data-testid=control]");
        Assert.Equal(string.Empty, control.GetAttribute("data-invalid"));
        Assert.Equal("true", control.GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "should not mark field as invalid for valueMissing if not dirty")]
    [Trait("Requirement", "FLD-FRM-05")]
    public void Should_not_mark_field_as_invalid_for_valueMissing_if_not_dirty()
    {
        IRenderedComponent<FieldRoot> cut = RenderRequired();

        host.Module.Control.Blur(cut, Native.ValueMissing);

        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "should mark field as invalid for valueMissing if dirty")]
    [Trait("Requirement", "FLD-FRM-05")]
    public void Should_mark_field_as_invalid_for_valueMissing_if_dirty()
    {
        IRenderedComponent<FieldRoot> cut = RenderRequired();

        host.Module.Control.Input(cut, "a", Native.Valid);
        host.Module.Control.Input(cut, string.Empty, Native.ValueMissing);
        host.Module.Control.Blur(cut);

        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty")]
    [Trait("Requirement", "FLD-API-23")]
    [Trait("Requirement", "FLD-FRM-05")]
    public void Should_mark_field_as_invalid_for_other_errors_even_if_not_dirty()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control").AddUnmatched("type", "email").Add(x => x.DefaultValue, "not_an_email@")));
        Assert.Equal("email", cut.Find("[data-testid=control]").GetAttribute("type"));

        host.Module.Control.Blur(cut, Native.TypeMismatch);

        IElement control = cut.Find("[data-testid=control]");
        Assert.Equal(string.Empty, control.GetAttribute("data-invalid"));
        Assert.Equal("true", control.GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "clears its own message on an input that became disabled")]
    [Trait("Requirement", "FLD-FRM-06")]
    [Trait("Requirement", "FLD-FRM-11")]
    [Trait("Requirement", "FLD-DOM-10")]
    public async Task Clears_its_own_message_on_an_input_that_became_disabled()
    {
        bool failing = true;
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Validate, _ => ValueTask.FromResult<FieldValidationResult?>(failing ? "custom error" : null))
            .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control")));
        FieldRoot root = cut.Instance;

        Task validation = cut.InvokeAsync(root.ValidateAsync);
        host.Module.Settle(cut);
        await validation;
        Assert.Equal("custom error", LastControlOptions()!.CustomValidity);

        cut.FindComponent<FieldControl>().Render(p => p.Add(x => x.Disabled, true));
        failing = false;
        validation = cut.InvokeAsync(root.ValidateAsync);
        host.Module.Settle(cut);
        await validation;
        cut.FindComponent<FieldControl>().Render(p => p.Add(x => x.Disabled, false));

        Assert.Null(LastControlOptions()!.CustomValidity);
    }

    [Fact(DisplayName = "keeps the original baseline when a controlled control remounts")]
    [Trait("Requirement", "FLD-STA-14")]
    [Trait("Requirement", "FLD-FRM-01")]
    [Trait("Requirement", "FFI-STA-04")]
    public void Keeps_the_original_baseline_when_a_controlled_control_remounts()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.Controlled).Add(x => x.InitialValue, "a"));
        IElement root = cut.Find("[data-testid=root]");

        host.Module.Control.Input(cut, "b");
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-dirty"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Controlled).Add(x => x.InitialValue, "a").Add(x => x.FirstMounted, false));
        cut.Render(p => p.Add(x => x.Scenario, Scenario.Controlled).Add(x => x.InitialValue, "a").Add(x => x.FirstMounted, true));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
        Assert.NotNull(root);

        host.Module.Control.Input(cut, "a");
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "keeps the field baseline when the control is swapped")]
    [Trait("Requirement", "FLD-STA-14")]
    [Trait("Requirement", "FLD-FRM-14")]
    [Trait("Requirement", "FFI-STA-04")]
    public void Keeps_the_field_baseline_when_the_control_is_swapped()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.DefaultValue, "a").Add(x => x.SecondKey, "a"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.DefaultValue, "x").Add(x => x.SecondKey, "b"));

        host.Module.Control.Input(cut, "y");
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));

        host.Module.Control.Input(cut, "a");
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "changes [data-filled] when the value is changed externally")]
    [Trait("Requirement", "FLD-API-15")]
    [Trait("Requirement", "FLD-STA-13")]
    [Trait("Requirement", "FLD-EVT-09")]
    public void Changes_data_filled_when_the_value_is_changed_externally()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.ConsumerInput).Add(x => x.InitialValue, string.Empty));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("data-filled"));

        cut.Find("[data-testid=change]").Click();
        host.Module.Settle(cut);
        Assert.Equal(string.Empty, cut.Find("[data-testid=control]").GetAttribute("data-filled"));

        cut.Find("[data-testid=reset]").Click();
        host.Module.Settle(cut);
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "does not update controlled dirty state from user input")]
    [Trait("Requirement", "FLD-API-07")]
    [Trait("Requirement", "FLD-STA-10")]
    [Trait("Requirement", "FFI-API-05")]
    public void Does_not_update_controlled_dirty_state_from_user_input()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.Dirty, false));

        host.Module.Control.Input(cut, "changed");

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "does not update controlled touched state on blur")]
    [Trait("Requirement", "FLD-API-07")]
    [Trait("Requirement", "FLD-STA-10")]
    public void Does_not_update_controlled_touched_state_on_blur()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.Touched, false));

        cut.Find("[data-testid=control]").Focus();
        host.Module.Control.Blur(cut);

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-touched"));
    }

    [Fact(DisplayName = "should apply [data-touched] and [data-focused] style hooks from the control's focus and blur")]
    [Trait("Requirement", "FLD-FOC-01")]
    [Trait("Requirement", "FLD-FOC-02")]
    [Trait("Requirement", "FFI-STA-07")]
    [Trait("Requirement", "FFI-STA-09")]
    public void Should_apply_touched_and_focused_style_hooks_from_focus_and_blur()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p.Add(x => x.Scenario, Scenario.Uncontrolled));

        cut.Find("[data-testid=control]").Focus();
        foreach (string part in new[] { "root", "control", "label", "description" })
        {
            Assert.True(cut.Find($"[data-testid={part}]").HasAttribute("data-focused"));
        }

        host.Module.Control.Blur(cut);
        foreach (string part in new[] { "root", "control", "label", "description" })
        {
            Assert.False(cut.Find($"[data-testid={part}]").HasAttribute("data-focused"));
            Assert.True(cut.Find($"[data-testid={part}]").HasAttribute("data-touched"));
        }
    }

    private IRenderedComponent<FieldRoot> RenderRequired() => Context.Render<FieldRoot>(p => p
        .Add(x => x.ValidationMode, ValidationMode.OnBlur)
        .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control").AddUnmatched("required", true)));

    private FormControlOptions? LastControlOptions() => host.UpdateOptions().Select(FieldTestContext.Parse).LastOrDefault(o => o.Control is not null)?.Control;

    // FieldRoot.test.tsx "should debounce validation for radio groups" (M2 track 8; FLD-API-06, FLD-FRM-04): the group's
    // change commit waits for the debounce, on the field's time provider.
    [Fact(DisplayName = "should debounce validation for radio groups")]
    [Trait("Requirement", "FLD-API-06")]
    [Trait("Requirement", "FLD-FRM-04")]
    public async Task Should_debounce_validation_for_radio_groups()
    {
        using RadioTestContext radio = new();
        Microsoft.Extensions.Time.Testing.FakeTimeProvider clock = new();
        radio.Context.Services.AddSingleton<TimeProvider>(clock);
        List<object?> validated = [];
        IRenderedComponent<FieldRoot> cut = radio.Context.Render<FieldRoot>(p => p
            .Add(x => x.ValidationDebounceTime, 100)
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                validated.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>((string?)args.Value == "item-b" ? "error" : null);
            })
            .AddChildContent<RadioGroup<string>>(g => g.AddChildContent(RadioTestContext.StringRadios("item-a", "item-b"))));
        IElement Control() => cut.Find("[role=radiogroup]");

        await radio.ToggleAsync(cut, 1);
        await radio.SettleAllFormsAsync(cut);

        Assert.Empty(validated);
        Assert.False(Control().HasAttribute("aria-invalid"));

        await cut.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(99)));
        Assert.Empty(validated);
        Assert.False(Control().HasAttribute("aria-invalid"));

        await cut.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(1)));
        await radio.SettleAllFormsAsync(cut);

        Assert.Equal(["item-b"], validated.Select(v => (string?)v));
        Assert.Equal("true", Control().GetAttribute("aria-invalid"));
    }

    // FieldRoot.test.tsx "keeps [data-dirty] on a RadioGroup when returning to the first picked value" (M2 track 8;
    // FLD-DOM-02, FFI-STA-04/05): the baseline is the value at the group's first registration, here null.
    [Fact(DisplayName = "keeps [data-dirty] on a RadioGroup when returning to the first picked value")]
    [Trait("Requirement", "FLD-DOM-02")]
    [Trait("Requirement", "FFI-STA-04")]
    [Trait("Requirement", "FFI-STA-05")]
    public async Task Keeps_data_dirty_on_a_RadioGroup_when_returning_to_the_first_picked_value()
    {
        using RadioTestContext radio = new();
        IRenderedComponent<RadioGroupFieldHost> cut = radio.Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, null)
            .Add(x => x.Radios, [new HostRadio("a"), new HostRadio("b")]));
        IElement Root() => cut.Find("[data-testid=field]");
        Assert.False(Root().HasAttribute("data-dirty"));

        cut.Render(p => p.Add(x => x.Value, "a"));
        await radio.SettleAllFormsAsync(cut);
        Assert.Equal(string.Empty, Root().GetAttribute("data-dirty"));

        cut.Render(p => p.Add(x => x.Value, "b"));
        cut.Render(p => p.Add(x => x.Value, "a"));
        await radio.SettleAllFormsAsync(cut);
        Assert.Equal(string.Empty, Root().GetAttribute("data-dirty"));
    }
}
