using System.Globalization;
using AngleSharp.Dom;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// useNumberFieldStepperButton.test.tsx's describe.each over NumberField.Increment and NumberField.Decrement, the rows that
// need no typed text: one derived class per case. A press reaches C# through the `button` module's hold reports
// (NumberFieldTestContext): `fireEvent.click` is the single-action click report, `fireEvent.pointerDown` a press's start and
// first tick. The browser half of the press-and-hold titles is tests/browser/number-field*.spec.ts (NUM-PTR-01..06, [L4]).
/// <summary>A stepper button of a number field, run once as the increment button and once as the decrement button.</summary>
public abstract class NumberFieldStepperTests : IDisposable
{
    private readonly NumberFieldTestContext test = new();

    private protected NumberFieldStepperTests(bool increment, string label, string step1, double fromFractional, double boundaryDefault, double? boundaryMin, double? boundaryMax, double boundaryValue)
    {
        Increment = increment;
        Label = label;
        Step1 = step1;
        FromFractional = fromFractional;
        BoundaryDefault = boundaryDefault;
        BoundaryMin = boundaryMin;
        BoundaryMax = boundaryMax;
        BoundaryValue = boundaryValue;
    }

    private bool Increment { get; }

    private string Label { get; }

    private string Step1 { get; }

    private double FromFractional { get; }

    private double BoundaryDefault { get; }

    private double? BoundaryMin { get; }

    private double? BoundaryMax { get; }

    private double BoundaryValue { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        test.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "has the ${label} label")]
    [Trait("Requirement", "NUM-ARIA-05")]
    public void Has_the_label()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.ShowInput, false));

        Assert.Equal(Label, Button(cut).GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "exposes aria-controls on the stepper")]
    [Trait("Requirement", "NUM-ARIA-06")]
    [Trait("Requirement", "NUM-API-18")]
    public void Exposes_aria_controls_on_the_stepper()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.ReadOnly, true));

        string inputId = Input(cut).GetAttribute("id")!;
        Assert.StartsWith("base-ui-", inputId, StringComparison.Ordinal);
        Assert.Equal(inputId, Button(cut).GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "seeds an empty field with 0")]
    [Trait("Requirement", "NUM-STA-17")]
    public async Task Seeds_an_empty_field_with_0()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render();

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "steps once from defaultValue=0")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task Steps_once_from_defaultValue_0()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.DefaultValue, 0));

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal(Step1, test.Text);
    }

    [Fact(DisplayName = "seeds an empty fully-negative range in range on the first step")]
    [Trait("Requirement", "NUM-STA-17")]
    public async Task Seeds_an_empty_fully_negative_range_in_range_on_the_first_step()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.Min, -10).Add(x => x.Max, -5));

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal("-5", test.Text);
    }

    [Fact(DisplayName = "seeds an empty field in range without directional snapping")]
    [Trait("Requirement", "NUM-STA-17")]
    public async Task Seeds_an_empty_field_in_range_without_directional_snapping()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p
            .Add(x => x.Min, -10)
            .Add(x => x.Max, -5)
            .Add(x => x.Step, (StepValue)2)
            .Add(x => x.SnapOnStep, true));

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal("-5", test.Text);
    }

    [Fact(DisplayName = "only calls onValueChange once per step")]
    [Trait("Requirement", "NUM-EVT-01")]
    public async Task Only_calls_onValueChange_once_per_step()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render();

        await test.ClickAsync(cut, Stepper(cut));
        Assert.Single(cut.Instance.Changes);

        await test.ClickAsync(cut, Stepper(cut));
        Assert.Equal(2, cut.Instance.Changes.Count);
    }

    [Fact(DisplayName = "steps from the value set by an external controlled update")]
    [Trait("Requirement", "NUM-STA-04")]
    public async Task Steps_from_the_value_set_by_an_external_controlled_update()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.Controlled, true));

        await cut.InvokeAsync(() => cut.Instance.ExternalValue(1.23456));
        Assert.Equal(1.23456.ToString("#,##0.###", CultureInfo.GetCultureInfo("en-US")), test.Text);

        await test.ClickAsync(cut, Stepper(cut));
        Assert.Equal(FromFractional.ToString("#,##0.###", CultureInfo.GetCultureInfo("en-US")), test.Text);
    }

    [Fact(DisplayName = "steps uncontrolled defaultValue from numeric state, not rounded display text")]
    [Trait("Requirement", "NUM-KEY-12")]
    [Trait("Requirement", "NUM-PTR-05")]
    public async Task Steps_uncontrolled_defaultValue_from_numeric_state_not_rounded_display_text()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.DefaultValue, 1.23456));
        Assert.Equal("1.235", test.Text);

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal([FromFractional], cut.Instance.Changes);
        Assert.Equal(FromFractional.ToString("#,##0.###", CultureInfo.GetCultureInfo("en-US")), test.Text);
    }

    // NUM-PTR-03 at L2: the hold's tick that applied nothing at the boundary stops the repeat through the hold stop.
    [Fact(DisplayName = "stops calling onValueChange once the boundary is reached")]
    [Trait("Requirement", "NUM-STA-20")]
    [Trait("Requirement", "NUM-EVT-13")]
    public async Task Stops_calling_onValueChange_once_the_boundary_is_reached()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p
            .Add(x => x.DefaultValue, BoundaryDefault)
            .Add(x => x.Min, BoundaryMin)
            .Add(x => x.Max, BoundaryMax));

        await test.PressAsync(cut, Stepper(cut));

        Assert.Equal(BoundaryValue.ToString(CultureInfo.InvariantCulture), test.Text);
        Assert.Single(cut.Instance.Changes);

        await test.TickAsync(cut, Stepper(cut));
        await test.TickAsync(cut, Stepper(cut));

        Assert.Equal(BoundaryValue.ToString(CultureInfo.InvariantCulture), test.Text);
        Assert.Single(cut.Instance.Changes);
        Assert.Contains(test.ButtonUpdates, update => update.Contains("\"stop\":", StringComparison.Ordinal));
        Assert.True(Button(cut).HasAttribute("disabled"));
    }

    // Base UI's onClick returns while read-only; the module drops the press, and a report that still arrives (Server, a
    // stale handle) changes nothing.
    [Fact(DisplayName = "does not step when readOnly")]
    [Trait("Requirement", "NUM-API-14")]
    [Trait("Requirement", "NUM-EDGE-11")]
    public async Task Does_not_step_when_readOnly()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.ReadOnly, true));
        Assert.Equal("true", Button(cut).GetAttribute("aria-disabled"));

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal(string.Empty, test.Text);
        Assert.Empty(cut.Instance.Changes);
    }

    [Fact(DisplayName = "does not step when root is disabled")]
    [Trait("Requirement", "NUM-EDGE-12")]
    public async Task Does_not_step_when_root_is_disabled()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.Disabled, true));
        Assert.True(Button(cut).HasAttribute("disabled"));

        await test.ClickAsync(cut, Stepper(cut));

        Assert.Equal(string.Empty, test.Text);
        Assert.Empty(cut.Instance.Changes);
    }

    [Fact(DisplayName = "does not step when the button is disabled")]
    [Trait("Requirement", "NUM-API-23")]
    [Trait("Requirement", "NUM-DOM-10")]
    public async Task Does_not_step_when_the_button_is_disabled()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.StepperDisabled, true));
        Assert.True(Button(cut).HasAttribute("disabled"));
        Assert.Equal("0", test.Text);

        await test.PressAsync(cut, Stepper(cut));

        Assert.Empty(cut.Instance.Changes);
        Assert.Equal("0", test.Text);
    }

    [Theory(DisplayName = "passes disabled state to className when $scenario")]
    [Trait("Requirement", "NUM-DOM-03")]
    [InlineData("root is disabled", true, false)]
    [InlineData("button is disabled", false, true)]
    [Trait("Requirement", "NUM-STA-01")]
    [Trait("Requirement", "NUM-STA-02")]
    [Trait("Requirement", "NUM-DOM-17")]
    public void Passes_disabled_state_to_className(string scenario, bool rootDisabled, bool buttonDisabled)
    {
        _ = scenario;
        bool? seen = null;
        IRenderedComponent<NumberFieldStepperHost> cut = Render(p =>
        {
            p.Add(x => x.Disabled, rootDisabled).Add(x => x.StepperDisabled, buttonDisabled);
            if (Increment)
            {
                p.Add(x => x.IncrementClass, s => { seen = s.Disabled; return null; });
            }
            else
            {
                p.Add(x => x.DecrementClass, s => { seen = s.Disabled; return null; });
            }
        });

        Assert.True(seen);
        Assert.Equal(rootDisabled, Input(cut).HasAttribute("data-disabled"));
        Assert.True(Button(cut).HasAttribute("data-disabled"));
    }

    private IRenderedComponent<NumberFieldStepperHost> Render(Action<ComponentParameterCollectionBuilder<NumberFieldStepperHost>>? parameters = null)
    {
        return test.Context.Render<NumberFieldStepperHost>(p =>
        {
            p.Add(x => x.Increment, Increment);
            parameters?.Invoke(p);
        });
    }

    private Microsoft.AspNetCore.Components.IComponent Stepper(IRenderedComponent<NumberFieldStepperHost> cut) => Increment
        ? cut.FindComponent<NumberFieldIncrement>().Instance
        : cut.FindComponent<NumberFieldDecrement>().Instance;

    private static IElement Button(IRenderedComponent<NumberFieldStepperHost> cut) => cut.Find("button");

    private static IElement Input(IRenderedComponent<NumberFieldStepperHost> cut) => cut.Find("input[type=text]");
}

/// <summary>The increment button's case of the stepper tests.</summary>
public sealed class NumberFieldIncrementStepperTests : NumberFieldStepperTests
{
    public NumberFieldIncrementStepperTests()
        : base(increment: true, label: "Increase", step1: "1", fromFractional: 2.23456, boundaryDefault: 9, boundaryMin: null, boundaryMax: 10, boundaryValue: 10)
    {
    }
}

/// <summary>The decrement button's case of the stepper tests.</summary>
public sealed class NumberFieldDecrementStepperTests : NumberFieldStepperTests
{
    public NumberFieldDecrementStepperTests()
        : base(increment: false, label: "Decrease", step1: "-1", fromFractional: 0.23456, boundaryDefault: -9, boundaryMin: -10, boundaryMax: null, boundaryValue: -10)
    {
    }
}
