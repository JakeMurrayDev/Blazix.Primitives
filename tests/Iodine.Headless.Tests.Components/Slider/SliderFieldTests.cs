using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using static Iodine.Headless.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// SliderRoot.test.tsx's Field and form blocks, the [L2] rows (SLD §8): SliderField composed into the Root (ADR 05
// Amendments 2026-11-18 item 2). The module's reports are driven by hand; the browser's FormData and the focus of a blocked
// submission are L4.
/// <summary>The slider's field half: its flags, its registration, its validation and its EditContext participation.</summary>
public sealed class SliderFieldTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    private static RenderFragment InField(Action<FragmentParameters<FieldRoot>> field, RenderFragment slider) =>
        Part<FieldRoot>(f =>
        {
            field(f);
            f.AddChildContent(slider);
        });

    private static RenderFragment Slider(Action<FragmentParameters<SliderRoot>>? root = null, params RenderFragment[] thumbs) =>
        Part<SliderRoot>(r =>
        {
            r.AddUnmatched("data-testid", "root");
            root?.Invoke(r);
            r.AddChildContent(Anatomy(thumbs));
        });

    private static IRenderedComponent<SliderHost> Host(BunitContext context, RenderFragment content) =>
        context.Render<SliderHost>(p => p.AddChildContent(content));

    [Fact(DisplayName = "should receive disabled prop from Field.Root")]
    [Trait("Requirement", "SLD-FRM-08")]
    [Trait("Requirement", "SLD-API-05")]
    public void Should_receive_disabled_prop_from_Field_Root()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(f => f.Add(x => x.Disabled, true), Slider()));

        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-disabled"));
        Assert.True(Inputs(cut)[0].HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should receive name prop from Field.Root")]
    [Trait("Requirement", "SLD-FRM-08")]
    [Trait("Requirement", "SLD-API-10")]
    public void Should_receive_name_prop_from_Field_Root()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(f => f.Add(x => x.Name, "field-slider"), Slider(r => r.Add(x => x.Name, "own"))));

        Assert.Equal("field-slider", Inputs(cut)[0].GetAttribute("name"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("name"));
    }

    [Fact(DisplayName = "submits to an external form when `form` is provided")]
    [Trait("Requirement", "SLD-FRM-10")]
    [Trait("Requirement", "SLD-API-10")]
    public void Submits_to_an_external_form_when_form_is_provided()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, Slider(r => r.Add(x => x.Name, "volume").Add(x => x.Form, "external"), Thumb()));

        Assert.Equal("external", Inputs(cut)[0].GetAttribute("form"));
        Assert.Equal("volume", Inputs(cut)[0].GetAttribute("name"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("form"));
    }

    [Fact(DisplayName = "Field.Label")]
    [Trait("Requirement", "SLD-FRM-12")]
    [Trait("Requirement", "SLD-DOM-15")]
    [Trait("Requirement", "SLD-ARIA-07")]
    public void Field_Label()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Fragments(
            Part<FieldLabel>(l => l.AddUnmatched("data-testid", "field-label").AddChildContent("Volume")),
            Slider())));

        IElement label = cut.Find("[data-testid=field-label]");
        IElement input = Inputs(cut)[0];
        Assert.Equal(label.GetAttribute("for"), input.Id);
        Assert.Equal(label.Id, cut.Find("[data-testid=root]").GetAttribute("aria-labelledby"));
        Assert.Equal(label.Id, input.GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "Field.Description")]
    [Trait("Requirement", "SLD-ARIA-08")]
    [Trait("Requirement", "SLD-ARIA-09")]
    [Trait("Requirement", "SLD-API-15")]
    public void Field_Description()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Fragments(
            Part<FieldDescription>(d => d.AddUnmatched("data-testid", "description").AddChildContent("Help")),
            Slider(r => r.AddUnmatched("aria-describedby", "external-description")))));

        string descriptionId = cut.Find("[data-testid=description]").Id!;
        Assert.Equal($"external-description {descriptionId}", cut.Find("[data-testid=root]").GetAttribute("aria-describedby"));
        Assert.Equal(descriptionId, Inputs(cut)[0].GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "port: a value change inside a field never sets data-filled")]
    [Trait("Requirement", "SLD-STA-22")]
    public async Task A_value_change_inside_a_field_never_sets_data_filled()
    {
        SliderControlState? seen = null;
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Part<SliderRoot>(r =>
        {
            r.AddUnmatched("data-testid", "root");
            r.AddChildContent(Part<SliderControl>(c =>
            {
                c.Add(x => x.ClassValue, s =>
                {
                    seen = s;
                    return null;
                });
                c.AddChildContent(Thumb());
            }));
        })));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();

        await test.RequestAsync(root, "keyboard", 0, [1]);

        Assert.Equal("1", cut.Find("input").GetAttribute("aria-valuenow"));
        Assert.Empty(cut.FindAll("[data-filled]"));
        Assert.False(seen!.Filled);
    }

    [Fact(DisplayName = "[data-touched]")]
    [Trait("Requirement", "SLD-FRM-05")]
    [Trait("Requirement", "SLD-FRM-15")]
    [Trait("Requirement", "SLD-STA-02")]
    public async Task Data_touched()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Slider()));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-touched"));

        await test.FocusAsync(root, 0, focused: true);
        await test.FocusAsync(root, 0, focused: false, outside: true);

        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-touched"));
        Assert.True(cut.Find("[data-testid=thumb]").HasAttribute("data-touched"));
    }

    [Fact(DisplayName = "port: a keyboard candidate marks the field touched even when it is canceled")]
    [Trait("Requirement", "SLD-FRM-05")]
    [Trait("Requirement", "SLD-FRM-16")]
    public async Task Port_a_keyboard_candidate_marks_the_field_touched_even_when_it_is_canceled()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Slider(r => r.Add(x => x.OnValueChange, EventCallback.Factory.Create<ValueChangeEventArgs<double>>(this, e => e.Details.Cancel())))));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "keyboard", 0, [1]);

        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-touched"));
        Assert.Equal("0", Inputs(cut)[0].GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "SLD-STA-21")]
    [Trait("Requirement", "SLD-FRM-15")]
    public async Task Data_dirty()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Slider()));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "input-change", 0, [50]);

        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "[data-dirty] with a range value")]
    [Trait("Requirement", "SLD-STA-21")]
    public async Task Data_dirty_with_a_range_value()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Slider(r => r.Add(x => x.DefaultValues, new double[] { 20, 50 }), Thumb(0), Thumb(1))));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();

        await test.RequestAsync(root, "input-change", 1, [20, 60]);
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));

        await test.RequestAsync(root, "input-change", 1, [20, 50]);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "[data-focused]")]
    [Trait("Requirement", "SLD-FOC-04")]
    [Trait("Requirement", "SLD-FRM-15")]
    public async Task Data_focused()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(_ => { }, Slider()));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();

        await test.FocusAsync(root, 0, focused: true);
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-focused"));

        await test.FocusAsync(root, 0, focused: false, outside: true);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "does not commit field validation when moving focus between range thumbs")]
    [Trait("Requirement", "SLD-FOC-04")]
    [Trait("Requirement", "SLD-FRM-06")]
    public async Task Does_not_commit_field_validation_when_moving_focus_between_range_thumbs()
    {
        int validations = 0;
        IRenderedComponent<SliderHost> cut = Host(Context, InField(
            f => f.Add(x => x.ValidationMode, ValidationMode.OnBlur).Add(x => x.Validate, (Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>)(_ =>
            {
                validations++;
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })),
            Slider(r => r.Add(x => x.DefaultValues, new double[] { 20, 50 }), Thumb(0), Thumb(1))));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();

        await test.FocusAsync(root, 0, focused: true);
        await test.FocusAsync(root, 0, focused: false, outside: false);
        await test.FocusAsync(root, 1, focused: true);
        Assert.Equal(0, validations);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-touched"));

        await test.FocusAsync(root, 1, focused: false, outside: true);
        Assert.Equal(1, validations);
    }

    [Fact(DisplayName = "validationMode=onChange")]
    [Trait("Requirement", "SLD-FRM-04")]
    [Trait("Requirement", "SLD-FRM-07")]
    [Trait("Requirement", "SLD-FRM-13")]
    [Trait("Requirement", "SLD-ARIA-08")]
    public async Task ValidationMode_onChange()
    {
        IRenderedComponent<SliderHost> cut = Host(Context, InField(
            f => f.Add(x => x.ValidationMode, ValidationMode.OnChange).Add(x => x.Validate, (Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>)(args =>
                ValueTask.FromResult<FieldValidationResult?>(args.Value is double value && value > 50 ? "too high" : null))),
            Slider()));
        IRenderedComponent<SliderRoot> root = cut.FindComponent<SliderRoot>();

        await test.RequestAsync(root, "input-change", 0, [60]);
        cut.WaitForAssertion(() => Assert.Equal("true", Inputs(cut)[0].GetAttribute("aria-invalid")));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));
        Assert.True(cut.Find("[data-testid=thumb]").HasAttribute("data-invalid"));

        await test.RequestAsync(root, "input-change", 0, [40]);
        cut.WaitForAssertion(() => Assert.False(Inputs(cut)[0].HasAttribute("aria-invalid")));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-valid"));
    }

    [Fact(DisplayName = "receives an array value for range sliders")]
    [Trait("Requirement", "SLD-STA-12")]
    [Trait("Requirement", "SLD-FRM-13")]
    public async Task Receives_an_array_value_for_range_sliders()
    {
        object? received = null;
        IRenderedComponent<SliderHost> cut = Host(Context, InField(
            f => f.Add(x => x.ValidationMode, ValidationMode.OnChange).Add(x => x.Validate, (Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>)(args =>
            {
                received = args.Value;
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })),
            Slider(r => r.Add(x => x.DefaultValues, new double[] { 20, 50 }), Thumb(0), Thumb(1))));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "input-change", 0, [30, 50]);

        cut.WaitForAssertion(() => Assert.Equal([30, 50], Assert.IsAssignableFrom<IReadOnlyList<double>>(received)));
    }

    [Fact(DisplayName = "does not call validate on change when validationMode is omitted")]
    [Trait("Requirement", "SLD-FRM-09")]
    [Trait("Requirement", "SLD-FRM-04")]
    public async Task Does_not_call_validate_on_change_when_validationMode_is_omitted()
    {
        int validations = 0;
        IRenderedComponent<SliderHost> cut = Host(Context, InField(
            f => f.Add(x => x.Validate, (Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>)(_ =>
            {
                validations++;
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })),
            Slider()));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "input-change", 0, [30]);

        Assert.Equal(0, validations);
    }

    // ADR 05 Amendments 2026-10-22 and 2026-11-18: EditContext participation over ValueExpression.
    [Fact(DisplayName = "notifies the cascaded EditContext of every committed change")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "SLD-FRM-02")]
    public async Task Notifies_the_cascaded_EditContext_of_every_committed_change()
    {
        Volume model = new();
        EditContext editContext = new(model);
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        IRenderedComponent<BoundVolume> cut = Context.Render<BoundVolume>(p => p.Add(x => x.Model, model).Add(x => x.EditContext, editContext));

        await test.RequestAsync(cut.FindComponent<SliderRoot>(), "input-change", 0, [40]);

        Assert.Equal(40, model.Level);
        Assert.Equal([new FieldIdentifier(model, nameof(Volume.Level))], changed);
        Assert.Equal("Level", Inputs(cut)[0].GetAttribute("name"));
    }

    public sealed class Volume
    {
        public double? Level { get; set; } = 20;
    }

    /// <summary>A consumer that binds a slider to its model with <c>@bind-Value</c> inside an edit context.</summary>
    public sealed class BoundVolume : ComponentBase
    {
        [Parameter]
        public Volume Model { get; set; } = null!;

        [Parameter]
        public EditContext EditContext { get; set; } = null!;

        /// <inheritdoc />
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddComponentParameter(1, "Value", EditContext);
            builder.AddComponentParameter(2, "IsFixed", true);
            builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<SliderRoot>(0);
                inner.AddComponentParameter(1, nameof(SliderRoot.Value), Model.Level);
                inner.AddComponentParameter(2, nameof(SliderRoot.ValueChanged), EventCallback.Factory.Create<double?>(this, value => Model.Level = value));
                inner.AddComponentParameter(3, nameof(SliderRoot.ValueExpression), (System.Linq.Expressions.Expression<Func<double?>>)(() => Model.Level));
                inner.AddComponentParameter(4, nameof(SliderRoot.ChildContent), Anatomy());
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
