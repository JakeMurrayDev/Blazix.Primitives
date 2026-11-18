using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderRoot.test.tsx, the [L2] rows (docs/components/slider.md): what C# renders, and the C# half of every request the
// `slider` module reports (ADR 06 §1: the module's reports are driven by hand). The keys, the pointer arithmetic and the
// positions are the module's, at L3 and L4.
/// <summary>The slider root: its element, its value and its requests.</summary>
public sealed class SliderRootTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Slider.Root>")]
    [Trait("Requirement", "SLD-STR-01")]
    [Trait("Requirement", "SLD-EDGE-01")]
    public void Throws_a_descriptive_error_when_rendered_outside_Slider_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<SliderControl>());

        Assert.Equal("Blazix: `SliderRootContext` is missing. Slider parts must be placed within `<SliderRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: rejects a value of each triple")]
    [Trait("Requirement", "SLD-API-02")]
    public void Port_rejects_a_value_of_each_triple()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Context.Render<SliderRoot>(p => p.Add(x => x.Value, 10).Add(x => x.Values, new double[] { 10, 20 })));

        Assert.StartsWith("Blazix: `SliderRoot` received `Value` and `Values`.", error.Message, StringComparison.Ordinal);
        Assert.EndsWith("`OnValuesChange` for a list.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "sets the data-orientation attribute")]
    [Trait("Requirement", "SLD-DOM-01")]
    [Trait("Requirement", "SLD-DOM-02")]
    [Trait("Requirement", "SLD-ARIA-10")]
    public void Sets_the_data_orientation_attribute()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.Orientation, Primitives.Orientation.Vertical).AddChildContent(Anatomy()));

        IElement root = cut.Find("[role=group]");
        Assert.Equal("vertical", root.GetAttribute("data-orientation"));
        foreach (string part in new[] { "value", "control", "track", "indicator", "thumb" })
        {
            Assert.Equal("vertical", cut.Find($"[data-testid={part}]").GetAttribute("data-orientation"));
        }

        Assert.Null(root.GetAttribute("tabindex"));
        Assert.False(string.IsNullOrEmpty(root.Id));
    }

    [Fact(DisplayName = "should render data-disabled on all subcomponents")]
    [Trait("Requirement", "SLD-DOM-02")]
    [Trait("Requirement", "SLD-API-05")]
    [Trait("Requirement", "SLD-STA-05")]
    public void Should_render_data_disabled_on_all_subcomponents()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Fragments(Part<SliderLabel>(l => l.AddUnmatched("data-testid", "label")), Anatomy())));

        Assert.True(cut.Find("[role=group]").HasAttribute("data-disabled"));
        foreach (string part in new[] { "label", "value", "control", "track", "indicator", "thumb" })
        {
            Assert.True(cut.Find($"[data-testid={part}]").HasAttribute("data-disabled"), part);
        }

        Assert.True(Inputs(cut)[0].HasAttribute("disabled"));
        Assert.Null(Inputs(cut)[0].GetAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "has the correct aria attributes")]
    [Trait("Requirement", "SLD-ARIA-01")]
    [Trait("Requirement", "SLD-ARIA-02")]
    [Trait("Requirement", "SLD-ARIA-03")]
    [Trait("Requirement", "SLD-ARIA-06")]
    [Trait("Requirement", "SLD-ARIA-07")]
    [Trait("Requirement", "SLD-API-14")]
    [Trait("Requirement", "SLD-DOM-14")]
    public void Has_the_correct_aria_attributes()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 30)
            .AddUnmatched("aria-labelledby", "a-label")
            .AddChildContent(Anatomy()));

        IElement root = cut.Find("[role=group]");
        IElement input = Inputs(cut)[0];
        Assert.Equal("a-label", root.GetAttribute("aria-labelledby"));
        Assert.Equal("INPUT", input.TagName);
        Assert.Null(input.GetAttribute("role"));
        Assert.Null(input.GetAttribute("aria-hidden"));
        Assert.Equal("range", input.GetAttribute("type"));
        Assert.Equal("30", input.GetAttribute("aria-valuenow"));
        Assert.Equal("horizontal", input.GetAttribute("aria-orientation"));
        Assert.Equal("1", input.GetAttribute("step"));
        Assert.Equal("a-label", input.GetAttribute("aria-labelledby"));
        Assert.Null(cut.Find("[data-testid=thumb]").GetAttribute("role"));
    }

    [Fact(DisplayName = "sets the max attribute on the input")]
    [Trait("Requirement", "SLD-API-06")]
    [Trait("Requirement", "SLD-DOM-14")]
    public void Sets_the_max_attribute_on_the_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 150).Add(x => x.Step, 100).Add(x => x.Max, 750).AddChildContent(Anatomy()));

        Assert.Equal("750", Inputs(cut)[0].GetAttribute("max"));
    }

    [Fact(DisplayName = "sets the min attribute on the input")]
    [Trait("Requirement", "SLD-API-06")]
    [Trait("Requirement", "SLD-DOM-14")]
    public void Sets_the_min_attribute_on_the_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 150).Add(x => x.Step, 100).Add(x => x.Min, 150).AddChildContent(Anatomy()));

        Assert.Equal("150", Inputs(cut)[0].GetAttribute("min"));
    }

    [Fact(DisplayName = "sets the `aria-orientation` attribute")]
    [Trait("Requirement", "SLD-ARIA-03")]
    [Trait("Requirement", "SLD-API-08")]
    public void Sets_the_aria_orientation_attribute()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.Orientation, Primitives.Orientation.Vertical).AddChildContent(Anatomy()));

        Assert.Equal("vertical", Inputs(cut)[0].GetAttribute("aria-orientation"));
    }

    // SLD-I18N-03, SLD-EDGE-16: `writing-mode` only, never `appearance`.
    [Fact(DisplayName = "port: does not set the orientation via appearance for WebKit browsers")]
    [Trait("Requirement", "SLD-EDGE-16")]
    [Trait("Requirement", "SLD-I18N-03")]
    public void Port_does_not_set_the_orientation_via_appearance()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.Orientation, Primitives.Orientation.Vertical).AddChildContent(Anatomy()));

        string style = Inputs(cut)[0].GetAttribute("style")!;
        Assert.Contains("writing-mode: vertical-lr", style, StringComparison.Ordinal);
        Assert.DoesNotContain("appearance", style, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should set default aria-valuetext on range slider thumbs")]
    [Trait("Requirement", "SLD-ARIA-04")]
    public void Should_set_default_aria_valuetext_on_range_slider_thumbs()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 44, 50 })
            .Add(x => x.Culture, EnUs)
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("44 start range", Inputs(cut)[0].GetAttribute("aria-valuetext"));
        Assert.Equal("50 end range", Inputs(cut)[1].GetAttribute("aria-valuetext"));
    }

    // SLD-API-09, SLD-I18N-02; ADR 08 Amendments 2026-11-18 (decision 10): Base UI's USD_NUMBER_FORMAT becomes `C2` under en-US.
    [Fact(DisplayName = "port: formats the value")]
    [Trait("Requirement", "SLD-API-09")]
    [Trait("Requirement", "SLD-ARIA-04")]
    [Trait("Requirement", "SLD-DOM-18")]
    public void Port_formats_the_value()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.Format, "C2")
            .Add(x => x.Culture, EnUs)
            .AddChildContent(Anatomy()));

        Assert.Equal("$50.00", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("$50.00", Inputs(cut)[0].GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "port: formats range values")]
    [Trait("Requirement", "SLD-API-09")]
    [Trait("Requirement", "SLD-DOM-18")]
    public void Port_formats_range_values()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 1234.5, 5678.9 })
            .Add(x => x.Max, 10000)
            .Add(x => x.Culture, EnUs)
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("1,234.5 – 5,678.9", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("1,234.5 start range", Inputs(cut)[0].GetAttribute("aria-valuetext"));
    }

    // SLD-I18N-02: Base UI's `de-DE` with two fraction digits becomes `N2` under de-DE.
    [Fact(DisplayName = "port: sets the locale when formatting a single value")]
    [Trait("Requirement", "SLD-I18N-02")]
    public void Port_sets_the_locale_when_formatting_a_single_value()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 70.51)
            .Add(x => x.Format, "N2")
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("de-DE"))
            .AddChildContent(Anatomy()));

        Assert.Equal("70,51", cut.Find("[data-testid=value]").TextContent);
    }

    // SLD-API-09; ADR 08 Amendments 2026-11-18: no Format is grouping and at most three fraction digits, and no aria-valuetext.
    [Fact(DisplayName = "port: formats with grouping and three fraction digits without a format")]
    [Trait("Requirement", "SLD-API-09")]
    [Trait("Requirement", "SLD-ARIA-04")]
    public void Port_formats_with_grouping_and_three_fraction_digits_without_a_format()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 1234.56789)
            .Add(x => x.Max, 10000)
            .Add(x => x.Culture, EnUs)
            .AddChildContent(Anatomy()));

        Assert.Equal("1,234.568", cut.Find("[data-testid=value]").TextContent);
        Assert.False(Inputs(cut)[0].HasAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "recomputes the thumb aria text when the format option changes")]
    [Trait("Requirement", "SLD-EDGE-17")]
    [Trait("Requirement", "SLD-ARIA-04")]
    public void Recomputes_the_thumb_aria_text_when_the_format_option_changes()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 50).Add(x => x.Format, "N0").Add(x => x.Culture, EnUs).AddChildContent(Anatomy()));
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuetext"));

        cut.Render(p => p.Add(x => x.DefaultValue, 50).Add(x => x.Format, "P0").Add(x => x.Culture, EnUs).AddChildContent(Anatomy()));

        Assert.Equal("5,000%", Inputs(cut)[0].GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "should not break when initial value is out of range")]
    [Trait("Requirement", "SLD-EDGE-04")]
    [Trait("Requirement", "SLD-STA-03")]
    public void Should_not_break_when_initial_value_is_out_of_range()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.Value, 150).AddChildContent(Anatomy()));

        Assert.Equal("100", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("100", Inputs(cut)[0].GetAttribute("value"));
    }

    [Fact(DisplayName = "clamps range values that fall outside the min and max bounds")]
    [Trait("Requirement", "SLD-EDGE-04")]
    [Trait("Requirement", "SLD-STA-03")]
    public void Clamps_range_values_that_fall_outside_the_min_and_max_bounds()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.Values, new double[] { 120, -20 })
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("0", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("100", Inputs(cut)[1].GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "should call handlers")]
    [Trait("Requirement", "SLD-EVT-01")]
    [Trait("Requirement", "SLD-EVT-05")]
    [Trait("Requirement", "SLD-EVT-11")]
    [Trait("Requirement", "SLD-API-03")]
    [Trait("Requirement", "SLD-API-04")]
    public async Task Should_call_handlers()
    {
        List<ValueChangeEventArgs<double>> changes = [];
        List<ValueCommittedEventArgs<double>> commits = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 10)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> e) => changes.Add(e))
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double> e) => commits.Add(e))
            .AddChildContent(Anatomy()));

        await test.RequestAsync(cut, "keyboard", 0, [11]);

        ValueChangeEventArgs<double> change = Assert.Single(changes);
        Assert.Equal(11, change.Value);
        Assert.Equal(EventReason.Keyboard, change.Details.Reason);
        Assert.Equal(0, Assert.IsType<SliderChangeEventDetails>(change.Details).ActiveThumbIndex);
        Assert.Equal("keydown", change.Details.Event.Type);
        ValueCommittedEventArgs<double> commit = Assert.Single(commits);
        Assert.Equal(11, commit.Value);
        Assert.Equal(EventReason.Keyboard, commit.Details.Reason);
        Assert.Equal("11", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("11", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "provides the change reason for input events")]
    [Trait("Requirement", "SLD-EVT-02")]
    [Trait("Requirement", "SLD-EVT-11")]
    public async Task Provides_the_change_reason_for_input_events()
    {
        List<ValueChangeEventArgs<IReadOnlyList<double>>> changes = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 20, 50 })
            .Add(x => x.OnValuesChange, (ValueChangeEventArgs<IReadOnlyList<double>> e) => changes.Add(e))
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        await test.RequestAsync(cut, "input-change", 1, [20, 60]);

        ValueChangeEventArgs<IReadOnlyList<double>> change = Assert.Single(changes);
        Assert.Equal([20, 60], change.Value);
        Assert.Equal(EventReason.InputChange, change.Details.Reason);
        Assert.Equal(1, ((SliderChangeEventDetails)change.Details).ActiveThumbIndex);
    }

    [Fact(DisplayName = "should update aria-valuenow")]
    [Trait("Requirement", "SLD-ARIA-02")]
    [Trait("Requirement", "SLD-DOM-16")]
    [Trait("Requirement", "SLD-STA-10")]
    [Trait("Requirement", "SLD-API-01")]
    public async Task Should_update_aria_valuenow()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy()));
        Assert.Equal("0", Inputs(cut)[0].GetAttribute("aria-valuenow"));

        await test.RequestAsync(cut, "input-change", 0, [51]);

        Assert.Equal("51", Inputs(cut)[0].GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "does not commit a canceled change")]
    [Trait("Requirement", "SLD-EVT-03")]
    [Trait("Requirement", "SLD-FRM-16")]
    public async Task Does_not_commit_a_canceled_change()
    {
        int commits = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> e) => e.Details.Cancel())
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double> _) => commits++)
            .AddChildContent(Anatomy()));

        int revision = await test.RequestAsync(cut, "keyboard", 0, [51]);

        Assert.Equal(0, commits);
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        JsonElement options = test.LastOptions;
        Assert.True(options.GetProperty("rollback").GetBoolean());
        Assert.Equal(revision, options.GetProperty("revision").GetInt32());
        Assert.Equal(50, options.GetProperty("values")[0].GetDouble());
    }

    // ADR 04 Amendments 2026-11-18 (the revision rule): a controlled value the consumer does not change rolls the module back.
    [Fact(DisplayName = "port: a controlled value the consumer keeps rolls the module back")]
    [Trait("Requirement", "SLD-STA-11")]
    [Trait("Requirement", "SLD-EVT-03")]
    public async Task Port_a_controlled_value_the_consumer_keeps_rolls_the_module_back()
    {
        int changes = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.Value, 50)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++)
            .AddChildContent(Anatomy()));

        int revision = await test.RequestAsync(cut, "keyboard", 0, [51]);

        Assert.Equal(1, changes);
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.True(test.LastOptions.GetProperty("rollback").GetBoolean());
        Assert.Equal(revision, test.LastOptions.GetProperty("revision").GetInt32());
    }

    [Fact(DisplayName = "port: an applied request is answered with its revision and no rollback")]
    [Trait("Requirement", "SLD-STA-10")]
    public async Task Port_an_applied_request_is_answered_with_its_revision_and_no_rollback()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 50).AddChildContent(Anatomy()));

        int revision = await test.RequestAsync(cut, "keyboard", 0, [51]);

        JsonElement options = test.LastOptions;
        Assert.False(options.GetProperty("rollback").GetBoolean());
        Assert.Equal(revision, options.GetProperty("revision").GetInt32());
        Assert.Equal(51, options.GetProperty("values")[0].GetDouble());
    }

    [Fact(DisplayName = "positions the thumb when the controlled value changes externally")]
    [Trait("Requirement", "SLD-STA-11")]
    [Trait("Requirement", "SLD-EVT-07")]
    public void Positions_the_thumb_when_the_controlled_value_changes_externally()
    {
        int changes = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.Value, 20)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++)
            .AddChildContent(Anatomy()));

        cut.Render(p => p.Add(x => x.Value, 60).Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++).AddChildContent(Anatomy()));

        Assert.Equal("60", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal(60, test.LastOptions.GetProperty("values")[0].GetDouble());
        Assert.Equal(0, changes);
    }

    [Fact(DisplayName = "is called even if the range did not change")]
    [Trait("Requirement", "SLD-EVT-10")]
    [Trait("Requirement", "SLD-STA-09")]
    public async Task Is_called_even_if_the_range_did_not_change()
    {
        double[] consumer = [50, 20];
        List<IReadOnlyList<double>> changes = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.Values, consumer)
            .Add(x => x.OnValuesChange, (ValueChangeEventArgs<IReadOnlyList<double>> e) => changes.Add(e.Value))
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));
        Assert.True(test.LastOptions.GetProperty("rawDiffers").GetBoolean());

        await test.RequestAsync(cut, "input-change", 0, [20, 50]);

        IReadOnlyList<double> change = Assert.Single(changes);
        Assert.Equal([20, 50], change);
        Assert.NotSame(consumer, change);
    }

    [Fact(DisplayName = "should fire only when the value changes")]
    [Trait("Requirement", "SLD-STA-09")]
    [Trait("Requirement", "SLD-EVT-06")]
    public async Task Should_fire_only_when_the_value_changes()
    {
        int changes = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 30)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++)
            .AddChildContent(Anatomy()));

        await test.RequestAsync(cut, "input-change", 0, [30]);

        Assert.Equal(0, changes);
    }

    [Fact(DisplayName = "onValueCommitted is called with the same value as the latest onValueChange when pointerUp occurs at a different location than onValueChange")]
    [Trait("Requirement", "SLD-EVT-05")]
    [Trait("Requirement", "SLD-EVT-12")]
    [Trait("Requirement", "SLD-STA-23")]
    public async Task OnValueCommitted_is_called_with_the_same_value_as_the_latest_onValueChange()
    {
        List<string> commits = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double> e) => commits.Add($"{e.Value}:{e.Details.Reason.ToLiteral()}"))
            .AddChildContent(Anatomy()));

        await test.GestureAsync(cut, active: true, dragging: true);
        await test.RequestAsync(cut, "track-press", 0, [10]);
        await test.RequestAsync(cut, "drag", 0, [20]);
        Assert.Empty(commits);
        await test.GestureAsync(cut, active: false, dragging: false, commit: true);

        Assert.Equal(["20:drag"], commits);
    }

    [Fact(DisplayName = "commits the last applied value when a later move is canceled")]
    [Trait("Requirement", "SLD-EVT-12")]
    [Trait("Requirement", "SLD-EVT-03")]
    public async Task Commits_the_last_applied_value_when_a_later_move_is_canceled()
    {
        List<double> commits = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> e) =>
            {
                if (e.Value > 30)
                {
                    e.Details.Cancel();
                }
            })
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double> e) => commits.Add(e.Value))
            .AddChildContent(Anatomy()));

        await test.GestureAsync(cut, active: true, dragging: true);
        await test.RequestAsync(cut, "drag", 0, [20]);
        await test.RequestAsync(cut, "drag", 0, [40]);
        await test.GestureAsync(cut, active: false, dragging: false, commit: true);

        Assert.Equal([20], commits);
        Assert.Equal("20", Inputs(cut)[0].GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "does not commit when thumb press leaves the value unchanged")]
    [Trait("Requirement", "SLD-EVT-05")]
    [Trait("Requirement", "SLD-PTR-05")]
    public async Task Does_not_commit_when_thumb_press_leaves_the_value_unchanged()
    {
        int commits = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 30)
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double> _) => commits++)
            .AddChildContent(Anatomy()));

        await test.GestureAsync(cut, active: true, dragging: true);
        await test.GestureAsync(cut, active: false, dragging: false, commit: true);

        Assert.Equal(0, commits);
    }

    // SLD-STA-08: data-dragging is C#'s, rendered from the gesture report.
    [Fact(DisplayName = "port: renders data-dragging from the gesture report")]
    [Trait("Requirement", "SLD-STA-08")]
    [Trait("Requirement", "SLD-DOM-02")]
    public async Task Port_renders_data_dragging_from_the_gesture_report()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy()));
        Assert.False(cut.Find("[role=group]").HasAttribute("data-dragging"));

        await test.GestureAsync(cut, active: true, dragging: true);

        Assert.True(cut.Find("[role=group]").HasAttribute("data-dragging"));
        Assert.True(cut.Find("[data-testid=thumb]").HasAttribute("data-dragging"));

        await test.GestureAsync(cut, active: false, dragging: false, commit: true);

        Assert.False(cut.Find("[role=group]").HasAttribute("data-dragging"));
    }

    [Fact(DisplayName = "preserves single-element array values on track press")]
    [Trait("Requirement", "SLD-STA-04")]
    [Trait("Requirement", "SLD-API-02")]
    public async Task Preserves_single_element_array_values_on_track_press()
    {
        List<IReadOnlyList<double>> changes = [];
        List<IReadOnlyList<double>> commits = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 20 })
            .Add(x => x.OnValuesChange, (ValueChangeEventArgs<IReadOnlyList<double>> e) => changes.Add(e.Value))
            .Add(x => x.OnValuesCommitted, (ValueCommittedEventArgs<IReadOnlyList<double>> e) => commits.Add(e.Value))
            .AddChildContent(Anatomy(Thumb(0))));
        Assert.True(test.LastOptions.GetProperty("range").GetBoolean());

        await test.GestureAsync(cut, active: true, dragging: true);
        await test.RequestAsync(cut, "track-press", 0, [60]);
        await test.GestureAsync(cut, active: false, dragging: false, commit: true);

        Assert.Equal([60], Assert.Single(changes));
        Assert.Equal([60], Assert.Single(commits));
    }

    [Fact(DisplayName = "port: the attach carries the values, the bounds, the thumbs and the flags")]
    [Trait("Requirement", "SLD-API-06")]
    [Trait("Requirement", "SLD-API-07")]
    [Trait("Requirement", "SLD-API-12")]
    public void Port_the_attach_carries_the_values_the_bounds_the_thumbs_and_the_flags()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 30)
            .Add(x => x.Min, 10)
            .Add(x => x.Max, 90)
            .Add(x => x.Step, 5)
            .Add(x => x.LargeStep, 20)
            .Add(x => x.ThumbCollisionBehavior, SliderThumbCollisionBehavior.Swap)
            .AddChildContent(Anatomy()));

        JsonElement options = test.LastOptions;
        Assert.Equal(30, options.GetProperty("values")[0].GetDouble());
        Assert.Equal(10, options.GetProperty("min").GetDouble());
        Assert.Equal(90, options.GetProperty("max").GetDouble());
        Assert.Equal(5, options.GetProperty("step").GetDouble());
        Assert.Equal(20, options.GetProperty("largeStep").GetDouble());
        Assert.Equal("swap", options.GetProperty("collision").GetString());
        Assert.False(options.GetProperty("range").GetBoolean());
        Assert.Equal(Inputs(cut)[0].Id, options.GetProperty("thumbs")[0].GetProperty("inputId").GetString());
        Assert.Equal(0, options.GetProperty("controlRef").GetInt32());
    }
}
