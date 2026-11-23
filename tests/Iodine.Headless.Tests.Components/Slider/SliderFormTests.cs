using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderRoot.test.tsx's Form and Field › prop: validate titles at [L2] (SLD §8): the module's value requests and the
// `form` module's submit request are driven by hand (SliderTestContext); a field error's exit ends with the
// `transitions` module's report. Base UI's keys are the requests the module sends for them (`keyboard`), its
// `fireEvent.change` the `input-change` request.
/// <summary>The slider inside a form and a field: external errors, submitted values and validation timing.</summary>
public sealed class SliderFormTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    private static RenderFragment Slider(Action<FragmentParameters<SliderRoot>> root, params RenderFragment[] thumbs) =>
        Part<SliderRoot>(r =>
        {
            r.AddUnmatched("data-testid", "root");
            root(r);
            r.AddChildContent(Anatomy(thumbs));
        });

    private static RenderFragment Field(Action<FragmentParameters<FieldRoot>> field, RenderFragment slider) =>
        Part<FieldRoot>(f =>
        {
            field(f);
            f.AddChildContent(Fragments(slider, Part<FieldError>(e => e.AddUnmatched("data-testid", "error"))));
        });

    private IRenderedComponent<SliderHost> Host(RenderFragment content) => Context.Render<SliderHost>(p => p.AddChildContent(content));

    private static Func<FieldValidationArguments, ValueTask<FieldValidationResult?>> Validator(Func<object?, string?> validate) =>
        args => ValueTask.FromResult<FieldValidationResult?>(validate(args.Value));

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "SLD-FRM-14")]
    [Trait("Requirement", "SLD-ARIA-08")]
    [Trait("Requirement", "SLD-KEY-03")]
    public async Task Clears_external_errors_on_change()
    {
        IRenderedComponent<SliderHost> cut = Host(Part<Form>(f => f
            .Add(x => x.Errors, new FormErrors { ["test"] = "test" })
            .AddChildContent(Field(x => x.Add(y => y.Name, "test"), Slider(r => r.Add(x => x.DefaultValue, 50))))));
        Assert.Equal("true", Inputs(cut)[0].GetAttribute("aria-invalid"));
        Assert.Equal("test", cut.Find("[data-testid=error]").TextContent);

        // Shift+ArrowRight: the large step (10) from 50.
        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "keyboard", 0, [60], KeyJson("ArrowRight", Inputs(cut)[0].Id, shiftKey: true));

        Assert.False(Inputs(cut)[0].HasAttribute("aria-invalid"));
        await test.CompleteExitAsync(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "submits clamped range slider values to onFormSubmit")]
    [Trait("Requirement", "SLD-FRM-03")]
    [Trait("Requirement", "SLD-FRM-02")]
    [Trait("Requirement", "SLD-STA-03")]
    public async Task Submits_clamped_range_slider_values_to_onFormSubmit()
    {
        List<FormSubmitEventArgs> submits = [];
        IRenderedComponent<SliderHost> cut = Host(Part<Form>(f => f
            .Add(x => x.OnFormSubmit, EventCallback.Factory.Create<FormSubmitEventArgs>(this, submits.Add))
            .AddChildContent(Field(x => x.Add(y => y.Name, "slider"), Slider(r => r.Add(x => x.DefaultValues, new double[] { 19, 41 }).Add(x => x.Min, 20).Add(x => x.Max, 40), Thumb(0), Thumb(1))))));

        await test.SubmitAsync(cut);

        FormSubmitEventArgs submit = Assert.Single(submits);
        Assert.Equal([20, 40], Assert.IsAssignableFrom<IReadOnlyList<double>>(submit.Values["slider"]));
        Assert.Equal(EventReason.None, submit.Details.Reason);
    }

    [Fact(DisplayName = "submits a clamped single-thumb slider value to onFormSubmit")]
    [Trait("Requirement", "SLD-FRM-03")]
    [Trait("Requirement", "SLD-FRM-02")]
    [Trait("Requirement", "SLD-STA-03")]
    public async Task Submits_a_clamped_single_thumb_slider_value_to_onFormSubmit()
    {
        List<FormSubmitEventArgs> submits = [];
        IRenderedComponent<SliderHost> cut = Host(Part<Form>(f => f
            .Add(x => x.OnFormSubmit, EventCallback.Factory.Create<FormSubmitEventArgs>(this, submits.Add))
            .AddChildContent(Field(x => x.Add(y => y.Name, "slider"), Slider(r => r.Add(x => x.DefaultValue, 5).Add(x => x.Min, 20).Add(x => x.Max, 40))))));

        await test.SubmitAsync(cut);

        FormSubmitEventArgs submit = Assert.Single(submits);
        Assert.Equal(20d, submit.Values["slider"]);
        Assert.Equal(EventReason.None, submit.Details.Reason);
    }

    [Fact(DisplayName = "validationMode=onSubmit")]
    [Trait("Requirement", "SLD-FRM-07")]
    [Trait("Requirement", "SLD-FRM-04")]
    [Trait("Requirement", "SLD-FRM-15")]
    [Trait("Requirement", "SLD-ARIA-08")]
    [Trait("Requirement", "SLD-ARIA-09")]
    public async Task ValidationMode_onSubmit()
    {
        IRenderedComponent<SliderHost> cut = Host(Part<Form>(f => f.AddChildContent(Field(
            x => x.Add(y => y.Validate, Validator(value => value is double number && number > 90 ? "error" : null)),
            Slider(r => r.Add(x => x.DefaultValue, 99))))));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();
        AngleSharp.Dom.IElement Input() => Inputs(cut)[0];
        bool Has(string testId, string attribute) => cut.Find($"[data-testid={testId}]").HasAttribute(attribute);
        Assert.False(Input().HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        await test.RequestAsync(root, "input-change", 0, [98]);
        Assert.False(Input().HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        await test.SubmitAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal("true", Input().GetAttribute("aria-invalid")));
        Assert.NotEmpty(cut.FindAll("[data-testid=error]"));
        Assert.True(Has("root", "data-invalid"));
        Assert.True(Has("thumb", "data-invalid"));

        await test.RequestAsync(root, "input-change", 0, [10]);
        cut.WaitForAssertion(() => Assert.False(Input().HasAttribute("aria-invalid")));
        await test.CompleteExitAsync(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
        Assert.False(Has("root", "data-invalid"));
        Assert.False(Input().HasAttribute("data-invalid"));
        Assert.True(Has("root", "data-valid"));
        Assert.True(Has("thumb", "data-valid"));

        await test.RequestAsync(root, "input-change", 0, [94]);
        cut.WaitForAssertion(() => Assert.Equal("true", Input().GetAttribute("aria-invalid")));
        Assert.NotEmpty(cut.FindAll("[data-testid=error]"));

        await test.RequestAsync(root, "input-change", 0, [12]);
        cut.WaitForAssertion(() => Assert.False(Input().HasAttribute("aria-invalid")));
        await test.CompleteExitAsync(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "validates once when changed by the user")]
    [Trait("Requirement", "SLD-FRM-04")]
    public async Task Validates_once_when_changed_by_the_user()
    {
        List<object?> calls = [];
        IRenderedComponent<SliderHost> cut = Host(Field(
            x => x.Add(y => y.ValidationMode, ValidationMode.OnChange).Add(y => y.Validate, Validator(value =>
            {
                calls.Add(value);
                return null;
            })),
            Slider(r => r.Add(x => x.DefaultValue, 0))));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "keyboard", 0, [1]);

        cut.WaitForAssertion(() => Assert.Equal(1d, Assert.Single(calls)));
    }

    [Fact(DisplayName = "validates once with an array value for range sliders when changed by the user")]
    [Trait("Requirement", "SLD-FRM-04")]
    [Trait("Requirement", "SLD-FRM-13")]
    public async Task Validates_once_with_an_array_value_for_range_sliders_when_changed_by_the_user()
    {
        List<object?> calls = [];
        IRenderedComponent<SliderHost> cut = Host(Field(
            x => x.Add(y => y.ValidationMode, ValidationMode.OnChange).Add(y => y.Validate, Validator(value =>
            {
                calls.Add(value);
                return null;
            })),
            Slider(r => r.Add(x => x.DefaultValues, new double[] { 0, 5 }), Thumb(0), Thumb(1))));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "keyboard", 0, [1, 5]);

        cut.WaitForAssertion(() => Assert.Equal([1, 5], Assert.IsAssignableFrom<IReadOnlyList<double>>(Assert.Single(calls))));
    }

    [Fact(DisplayName = "revalidates when the controlled value changes externally")]
    [Trait("Requirement", "SLD-FRM-04")]
    [Trait("Requirement", "SLD-EVT-07")]
    public void Revalidates_when_the_controlled_value_changes_externally()
    {
        List<object?> calls = [];
        int changes = 0;
        Func<FieldValidationArguments, ValueTask<FieldValidationResult?>> validate = Validator(value =>
        {
            calls.Add(value);
            return value is double number && number == 5 ? "error" : null;
        });
        RenderFragment Build(double value) => Field(
            x => x.Add(y => y.ValidationMode, ValidationMode.OnChange).Add(y => y.Validate, validate).Add(y => y.Name, "volume"),
            Slider(r => r.Add(x => x.Value, value).Add(x => x.OnValueChange, EventCallback.Factory.Create<ValueChangeEventArgs<double>>(this, _ => changes++))));

        IRenderedComponent<SliderHost> cut = Context.Render<SliderHost>(p => p.AddChildContent(Build(0)));
        Assert.False(Inputs(cut)[0].HasAttribute("aria-invalid"));
        int initialCallCount = calls.Count;

        cut.Render(p => p.AddChildContent(Build(5)));

        cut.WaitForAssertion(() => Assert.Equal(initialCallCount + 1, calls.Count));
        Assert.Equal(5d, calls[^1]);
        cut.WaitForAssertion(() => Assert.Equal("true", Inputs(cut)[0].GetAttribute("aria-invalid")));
        Assert.Equal(0, changes);
    }
}
