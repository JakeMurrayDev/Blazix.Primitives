using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// 6b's input role at L2 (issue 277), batch B1: the stepper titles that type before they step, from
// useNumberFieldStepperButton.test.tsx's describe.each over NumberField.Increment and NumberField.Decrement (one derived class
// per case) and from NumberFieldIncrement.test.tsx's and NumberFieldDecrement.test.tsx's `prop: snapOnStep` blocks. Base UI's
// fireEvent.change and blur are the `number-field` module's reports, fireEvent.click the `button` module's single-action
// click report and fireEvent.pointerDown / pointerUp its press and release reports (NumberFieldTestContext); user.click on a
// stepper is a mouse press and release, whose click the module skips. The browser half (a real typed text, then a real press)
// is tests/browser/number-field.input-b1.spec.ts.
/// <summary>A stepper button that steps from typed text, run once as the increment button and once as the decrement button.</summary>
public abstract class NumberFieldStepperTypingTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();

    private protected NumberFieldStepperTypingTests(bool increment, string step1, string fromDirty100, double[] fromFractional, double snappedDirtyStep)
    {
        Increment = increment;
        Step1 = step1;
        FromDirty100 = fromDirty100;
        FromFractional = fromFractional;
        SnappedDirtyStep = snappedDirtyStep;
    }

    private bool Increment { get; }

    private string Step1 { get; }

    private string FromDirty100 { get; }

    private double[] FromFractional { get; }

    private double SnappedDirtyStep { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        test.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "steps from numeric state after typed precision is formatted on blur")]
    [Trait("Requirement", "NUM-PTR-05")]
    [Trait("Requirement", "NUM-STA-04")]
    public async Task Steps_from_numeric_state_after_typed_precision_is_formatted_on_blur()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(defaultValue: null);

        // user.click(input), then user.keyboard('1.23456'): one change per typed character.
        await test.FocusAsync(cut);
        const string typed = "1.23456";
        for (int length = 1; length <= typed.Length; length++)
        {
            await test.ChangeAsync(cut, typed[..length]);
        }

        await test.BlurAsync(cut, typed, revision: typed.Length);
        Assert.Equal(Format(1.23456), test.Text);

        await ClickWithMouseAsync(cut);
        Assert.Equal(Format(FromFractional[0]), test.Text);

        await ClickWithMouseAsync(cut);
        Assert.Equal(FromFractional[1], cut.Instance.Changes[^1]);
        Assert.Equal(Format(FromFractional[1]), test.Text);
    }

    [Fact(DisplayName = "steps when input is dirty but not blurred (click)")]
    [Trait("Requirement", "NUM-PTR-05")]
    [Trait("Requirement", "NUM-PTR-02")]
    public async Task Steps_when_input_is_dirty_but_not_blurred_click()
    {
        IRenderedComponent<StepperTypingHost> cut = Render();
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "100");
        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal(FromDirty100, test.Text);
    }

    [Fact(DisplayName = "steps when input is dirty but not blurred (pointerdown)")]
    [Trait("Requirement", "NUM-PTR-05")]
    [Trait("Requirement", "NUM-PTR-02")]
    public async Task Steps_when_input_is_dirty_but_not_blurred_pointerdown()
    {
        IRenderedComponent<StepperTypingHost> cut = Render();
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "100");
        await test.PressAsync(cut, Stepper(cut));

        Assert.Equal(FromDirty100, test.Text);
    }

    [Fact(DisplayName = "does not emit a snapped intermediate when committing dirty text before a step")]
    [Trait("Requirement", "NUM-PTR-05")]
    [Trait("Requirement", "NUM-EVT-03")]
    public async Task Does_not_emit_a_snapped_intermediate_when_committing_dirty_text_before_a_step()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(p => p.Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, true));
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "7");
        cut.Instance.Changes.Clear();
        await test.ClickAsync(cut, Stepper(cut));

        // Typing already reported 7, so the press reports only the step; a directional snap of the dirty "7" before
        // stepping would report an extra intermediate value (6).
        Assert.Equal([SnappedDirtyStep], cut.Instance.Changes.Select(v => v!.Value));
        Assert.Equal(SnappedDirtyStep.ToString(CultureInfo.InvariantCulture), test.Text);
    }

    [Fact(DisplayName = "does not commit a stale value when the first hold tick is canceled after dirty input")]
    [Trait("Requirement", "NUM-EVT-10")]
    [Trait("Requirement", "NUM-PTR-02")]
    public async Task Does_not_commit_a_stale_value_when_the_first_hold_tick_is_canceled_after_dirty_input()
    {
        bool cancelNextChange = false;
        IRenderedComponent<StepperTypingHost> cut = Render(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Cancel, () =>
            {
                if (!cancelNextChange)
                {
                    return false;
                }

                cancelNextChange = false;
                return true;
            }));

        await test.ClickAsync(cut, Stepper(cut));
        Assert.Equal(double.Parse(Step1, CultureInfo.InvariantCulture), cut.Instance.Commits[^1]);

        await cut.InvokeAsync(() => cut.Instance.ExternalValue(10));
        Assert.Equal("10", test.Text);

        await test.FocusAsync(cut);
        await test.ChangeAsync(cut, "-");
        Assert.Equal("-", test.Text);

        cancelNextChange = true;
        await test.PressAsync(cut, Stepper(cut));
        await test.ReleaseAsync(cut, Stepper(cut));

        Assert.Equal(2, cut.Instance.Commits.Count);
        Assert.Equal(10, cut.Instance.Commits[^1]);
    }

    private static string Format(double value) => value.ToString("#,##0.###", s_enUs);

    private async Task ClickWithMouseAsync(IRenderedComponent<StepperTypingHost> cut)
    {
        await test.PressAsync(cut, Stepper(cut));
        await test.ReleaseAsync(cut, Stepper(cut));
    }

    private IRenderedComponent<StepperTypingHost> Render(Action<ComponentParameterCollectionBuilder<StepperTypingHost>>? parameters = null, double? defaultValue = 0)
    {
        return test.Context.Render<StepperTypingHost>(p =>
        {
            p.Add(x => x.Increment, Increment).Add(x => x.DefaultValue, defaultValue);
            parameters?.Invoke(p);
        });
    }

    private IComponent Stepper(IRenderedComponent<StepperTypingHost> cut) => Increment
        ? cut.FindComponent<NumberFieldIncrement>().Instance
        : cut.FindComponent<NumberFieldDecrement>().Instance;
}

/// <summary>The increment button's case of the stepper typing tests.</summary>
public sealed class NumberFieldIncrementStepperTypingTests : NumberFieldStepperTypingTests
{
    public NumberFieldIncrementStepperTypingTests()
        : base(increment: true, step1: "1", fromDirty100: "101", fromFractional: [2.23456, 3.23456], snappedDirtyStep: 8)
    {
    }
}

/// <summary>The decrement button's case of the stepper typing tests.</summary>
public sealed class NumberFieldDecrementStepperTypingTests : NumberFieldStepperTypingTests
{
    public NumberFieldDecrementStepperTypingTests()
        : base(increment: false, step1: "-1", fromDirty100: "99", fromFractional: [0.23456, -0.76544], snappedDirtyStep: 6)
    {
    }
}

// NumberFieldIncrement.test.tsx's and NumberFieldDecrement.test.tsx's `prop: snapOnStep` blocks: a click steps, a change
// types, a click steps again from the typed text.
/// <summary>The increment and decrement buttons' snapping from typed text.</summary>
public sealed class NumberFieldStepperSnapTypingTests : IDisposable
{
    private readonly NumberFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should increment by exact step without rounding when snapOnStep is false")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_increment_by_exact_step_without_rounding_when_snapOnStep_is_false()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(increment: true, 2.7, p => p.Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, false));

        await Click(cut);

        Assert.Equal(4.7.ToString(CultureInfo.GetCultureInfo("en-US")), test.Text);
    }

    [Fact(DisplayName = "should snap on increment when snapOnStep is true")]
    [Trait("Requirement", "NUM-API-08")]
    [Trait("Requirement", "NUM-STA-08")]
    [Trait("Requirement", "NUM-PTR-05")]
    public async Task Should_snap_on_increment_when_snapOnStep_is_true()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(increment: true, 1.3, p => p.Add(x => x.SnapOnStep, true));

        await Click(cut);
        Assert.Equal("2", test.Text);

        await test.ChangeAsync(cut, "1.9");
        await Click(cut);
        Assert.Equal("2", test.Text);

        await test.ChangeAsync(cut, "-0.2");
        await Click(cut);
        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "should increment with respect to the min value")]
    [Trait("Requirement", "NUM-STA-08")]
    [Trait("Requirement", "NUM-PTR-05")]
    public async Task Should_increment_with_respect_to_the_min_value()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(increment: true, 1, p => p.Add(x => x.Min, 1).Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, true));

        await Click(cut);
        Assert.Equal("3", test.Text);

        await Click(cut);
        Assert.Equal("5", test.Text);

        await test.ChangeAsync(cut, "1.112");
        await Click(cut);
        Assert.Equal("3", test.Text);

        await test.ChangeAsync(cut, "0.999");
        await Click(cut);
        Assert.Equal("1", test.Text);
    }

    [Fact(DisplayName = "should decrement by exact step without rounding when snapOnStep is false")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_decrement_by_exact_step_without_rounding_when_snapOnStep_is_false()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(increment: false, 2.7, p => p.Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, false));

        await Click(cut);

        Assert.Equal(0.7.ToString(CultureInfo.GetCultureInfo("en-US")), test.Text);
    }

    [Fact(DisplayName = "should snap on decrement when snapOnStep is true")]
    [Trait("Requirement", "NUM-API-08")]
    [Trait("Requirement", "NUM-STA-08")]
    [Trait("Requirement", "NUM-PTR-05")]
    public async Task Should_snap_on_decrement_when_snapOnStep_is_true()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(increment: false, 1.3, p => p.Add(x => x.SnapOnStep, true));

        await Click(cut);
        Assert.Equal("1", test.Text);

        await test.ChangeAsync(cut, "1.9");
        await Click(cut);
        Assert.Equal("1", test.Text);

        await test.ChangeAsync(cut, "-0.2");
        await Click(cut);
        Assert.Equal("-1", test.Text);
    }

    [Fact(DisplayName = "should decrement with respect to the min value")]
    [Trait("Requirement", "NUM-STA-08")]
    [Trait("Requirement", "NUM-PTR-05")]
    public async Task Should_decrement_with_respect_to_the_min_value()
    {
        IRenderedComponent<StepperTypingHost> cut = Render(increment: false, 8, p => p.Add(x => x.Min, 1).Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, true));

        await Click(cut);
        Assert.Equal("7", test.Text);

        await Click(cut);
        Assert.Equal("5", test.Text);

        await test.ChangeAsync(cut, "9.112");
        await Click(cut);
        Assert.Equal("9", test.Text);

        await test.ChangeAsync(cut, "1.112");
        await Click(cut);
        Assert.Equal("1", test.Text);
    }

    private Task Click(IRenderedComponent<StepperTypingHost> cut)
    {
        IComponent stepper = cut.Instance.Increment
            ? cut.FindComponent<NumberFieldIncrement>().Instance
            : cut.FindComponent<NumberFieldDecrement>().Instance;
        return test.ClickAsync(cut, stepper);
    }

    private IRenderedComponent<StepperTypingHost> Render(bool increment, double defaultValue, Action<ComponentParameterCollectionBuilder<StepperTypingHost>> parameters)
    {
        return test.Context.Render<StepperTypingHost>(p =>
        {
            p.Add(x => x.Increment, increment).Add(x => x.DefaultValue, defaultValue);
            parameters(p);
        });
    }
}

// The test consumer of the stepper typing titles: Base UI's TestNumberField (a root with one stepper and the input, under
// en-US), and its `Controlled` component, whose useState value the host keeps and whose "external" button is ExternalValue.
/// <summary>A number field with one stepper and the input, owned by the root or by this host.</summary>
public sealed class StepperTypingHost : ComponentBase
{
    private double? hostValue;
    private bool initialized;

    [Parameter] public bool Increment { get; set; } = true;

    [Parameter] public bool Controlled { get; set; }

    [Parameter] public double? DefaultValue { get; set; }

    [Parameter] public double? Min { get; set; }

    [Parameter] public StepValue? Step { get; set; }

    [Parameter] public bool SnapOnStep { get; set; }

    /// <summary>Gets or sets the host's onValueChange veto: a change it answers true for is canceled.</summary>
    [Parameter] public Func<bool>? Cancel { get; set; }

    /// <summary>Gets the values every OnValueChange received, in order.</summary>
    public List<double?> Changes { get; } = [];

    /// <summary>Gets the values every OnValueCommitted received, in order.</summary>
    public List<double?> Commits { get; } = [];

    /// <summary>Sets the controlled value from outside, as the test's "external" button.</summary>
    public void ExternalValue(double? value)
    {
        hostValue = value;
        StateHasChanged();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!initialized)
        {
            initialized = true;
            hostValue = DefaultValue;
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<NumberFieldRoot>(0);
        if (Controlled)
        {
            builder.AddComponentParameter(1, nameof(NumberFieldRoot.Value), hostValue);
        }
        else
        {
            builder.AddComponentParameter(2, nameof(NumberFieldRoot.DefaultValue), DefaultValue);
        }

        builder.AddComponentParameter(3, nameof(NumberFieldRoot.Culture), CultureInfo.GetCultureInfo("en-US"));
        builder.AddComponentParameter(4, nameof(NumberFieldRoot.Min), Min);
        builder.AddComponentParameter(5, nameof(NumberFieldRoot.Step), Step);
        builder.AddComponentParameter(6, nameof(NumberFieldRoot.SnapOnStep), SnapOnStep);
        builder.AddComponentParameter(7, nameof(NumberFieldRoot.OnValueChange), EventCallback.Factory.Create<ValueChangeEventArgs<double?>>(this, OnChange));
        builder.AddComponentParameter(8, nameof(NumberFieldRoot.OnValueCommitted), EventCallback.Factory.Create<ValueCommittedEventArgs<double?>>(this, e => Commits.Add(e.Value)));
        builder.AddComponentParameter(9, nameof(NumberFieldRoot.ChildContent), (RenderFragment)(parts =>
        {
            if (Increment)
            {
                parts.OpenComponent<NumberFieldIncrement>(10);
                parts.CloseComponent();
            }
            else
            {
                parts.OpenComponent<NumberFieldDecrement>(11);
                parts.CloseComponent();
            }

            parts.OpenComponent<NumberFieldInput>(12);
            parts.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private void OnChange(ValueChangeEventArgs<double?> args)
    {
        Changes.Add(args.Value);
        if (Cancel?.Invoke() == true)
        {
            args.Details.Cancel();
            return;
        }

        if (Controlled)
        {
            hostValue = args.Value;
        }
    }
}
