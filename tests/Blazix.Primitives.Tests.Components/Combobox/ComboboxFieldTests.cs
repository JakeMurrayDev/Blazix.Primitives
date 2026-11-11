using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §8 at L2 (ComboboxRoot.test.tsx's and ComboboxTrigger.test.tsx's Field titles): the root's id is the field's control
// id, so Field.Label points at the input; a selection marks the field dirty and filled and validates in onChange mode; a
// blur of the input commits in onBlur mode; the field's disabled state and name reach the parts and the hidden input.
/// <summary>Combobox inside a Field at L2.</summary>
public sealed class ComboboxFieldTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "Field.Label")]
    [Trait("Requirement", "CMB-ARIA-16")]
    [Trait("Requirement", "CMB-FRM-06")]
    public void Field_Label_points_at_the_input()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new());
        IElement input = cut.Find("[data-testid=input]");

        Assert.NotNull(input.Id);
        Assert.Equal(input.Id, cut.Find("label").GetAttribute("for"));
        Assert.Equal(cut.Find("label").Id, input.GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "port: a selection marks the field dirty and filled and validates the value in onChange mode")]
    [Trait("Requirement", "CMB-FRM-03")]
    [Trait("Requirement", "CMB-FRM-04")]
    public async Task A_selection_marks_the_field_dirty_and_filled_and_validates()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldRoot> cut = Render(
            field => field
                .Add(x => x.ValidationMode, ValidationMode.OnChange)
                .Add(x => x.Validate, args =>
                {
                    validated.Add(args.Value);
                    return ValueTask.FromResult<FieldValidationResult?>(null);
                }),
            new() { ["DefaultOpen"] = true });
        Assert.False(FieldElement(cut).HasAttribute("data-filled"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.True(FieldElement(cut).HasAttribute("data-dirty"));
        Assert.True(FieldElement(cut).HasAttribute("data-filled"));
        Assert.Contains("banana", validated);
    }

    [Fact(DisplayName = "validates the $name value when blurred")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task Validates_the_value_when_blurred()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldRoot> cut = Render(
            field => field
                .Add(x => x.ValidationMode, ValidationMode.OnBlur)
                .Add(x => x.Validate, args =>
                {
                    validated.Add(args.Value);
                    return ValueTask.FromResult<FieldValidationResult?>(null);
                }),
            new() { ["DefaultValue"] = "apple" });

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        Assert.Equal(["apple"], validated);
        Assert.True(FieldElement(cut).HasAttribute("data-touched"));
    }

    [Fact(DisplayName = "should inherit disabled state from Field.Root")]
    [Trait("Requirement", "CMB-API-44")]
    [Trait("Requirement", "CMB-FRM-06")]
    public void Should_inherit_disabled_state_from_Field_Root()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => field.Add(x => x.Disabled, true), new(), withTrigger: true);

        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=input]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: the hidden input submits under the field's name")]
    [Trait("Requirement", "CMB-FRM-06")]
    [Trait("Requirement", "CMB-FRM-02")]
    public void The_hidden_input_submits_under_the_fields_name()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => field.Add(x => x.Name, "fruit"), new() { ["DefaultValue"] = "apple", ["Name"] = "own" });

        Assert.Equal("apple", cut.Find("input[name=fruit]").GetAttribute("value"));
        Assert.Empty(cut.FindAll("input[name=own]"));
    }

    [Fact(DisplayName = "port: the trigger's blur, as the module reports it, touches the field")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task The_triggers_reported_blur_touches_the_field()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new(), withTrigger: true);
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("onblur"));
        Blazix.Primitives.Interop.ComboboxTwin twin = test.TwinOf(Blazix.Primitives.Interop.ComboboxTwin.TriggerPart);

        await cut.InvokeAsync(() => twin.OnBlur(ComboboxTestContext.Click));

        Assert.True(FieldElement(cut).HasAttribute("data-touched"));
    }

    // ComboboxRoot.test.tsx's Field titles (PR 193 delta review B1): the field's state reaches the input and the trigger as
    // data attributes, and the field's name, disabled state, description and validation reach the parts.
    [Fact(DisplayName = "[data-touched]")]
    [Trait("Requirement", "CMB-DOM-02")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task Data_touched()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new(), withTrigger: true);
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-touched"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-touched"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-touched"));
    }

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "CMB-DOM-02")]
    [Trait("Requirement", "CMB-FRM-03")]
    public async Task Data_dirty()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new() { ["DefaultOpen"] = true }, withTrigger: true);
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "removes [data-dirty] in multiple mode after returning to the initial value")]
    [Trait("Requirement", "CMB-FRM-03")]
    public async Task Removes_data_dirty_in_multiple_mode_after_returning_to_the_initial_value()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new() { ["Multiple"] = true, ["DefaultOpen"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple"] }, withTrigger: true);
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());
        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "keeps [data-dirty] when the same values are reselected in a different order")]
    [Trait("Requirement", "CMB-FRM-03")]
    public async Task Keeps_data_dirty_when_the_same_values_are_reselected_in_a_different_order()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new() { ["Multiple"] = true, ["DefaultOpen"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple", "banana"] }, withTrigger: true);
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when filled")]
    [Trait("Requirement", "CMB-DOM-02")]
    [Trait("Requirement", "CMB-FRM-04")]
    public async Task Adds_data_filled_attribute_when_filled()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new() { ["DefaultOpen"] = true }, withTrigger: true);
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-filled"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-filled"));
        Assert.False(cut.Find("[data-testid=list]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when already filled")]
    [Trait("Requirement", "CMB-FRM-04")]
    public void Adds_data_filled_attribute_when_already_filled()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new() { ["DefaultValue"] = "apple" }, withTrigger: true);

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "[data-invalid]")]
    [Trait("Requirement", "CMB-DOM-02")]
    [Trait("Requirement", "CMB-FRM-05")]
    public void Data_invalid()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => field.Add(x => x.Invalid, true), new(), withTrigger: true);

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-invalid"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-invalid"));
    }

    [Fact(DisplayName = "[data-valid]")]
    [Trait("Requirement", "CMB-DOM-02")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task Data_valid()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => field.Add(x => x.ValidationMode, ValidationMode.OnBlur), new() { ["DefaultOpen"] = true }, withTrigger: true);
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-valid"));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-invalid"));
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-valid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-valid")));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-valid"));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-invalid"));
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-invalid"));
    }

    [Fact(DisplayName = "prop: validate")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task Prop_validate()
    {
        IRenderedComponent<FieldRoot> cut = Render(
            field => field.Add(x => x.ValidationMode, ValidationMode.OnBlur).Add(x => x.Validate, _ => ValueTask.FromResult<FieldValidationResult?>("error")),
            new());
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-invalid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    [Fact(DisplayName = "prop: validationMode=onChange")]
    [Trait("Requirement", "CMB-FRM-03")]
    public async Task Prop_validationMode_onChange()
    {
        IRenderedComponent<FieldRoot> cut = Render(
            field => field.Add(x => x.ValidationMode, ValidationMode.OnChange).Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>(args.Value as string == "apple" ? "error" : null)),
            new() { ["DefaultOpen"] = true });
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-invalid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    // CMB-FRM-05 (session S's L4 repro of "prop: validationMode=onSubmit"): with no callback that renders the consumer, nothing
    // renders the root after a selection, and the field still observes the change (AriaCombobox.tsx:1294-1323).
    [Fact(DisplayName = "port: a selection validates in onChange mode without a render of the root")]
    [Trait("Requirement", "CMB-FRM-03")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task A_selection_validates_in_onChange_mode_without_a_render_of_the_root()
    {
        List<string?> validated = [];
        IRenderedComponent<FieldRoot> cut = Render(
            field => field.Add(x => x.ValidationMode, ValidationMode.OnChange).Add(x => x.Validate, args =>
            {
                validated.Add(args.Value as string);
                return ValueTask.FromResult<FieldValidationResult?>(args.Value as string == "apple" ? "error" : null);
            }),
            new()
            {
                ["DefaultOpen"] = true,
                ["OnValueChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(new object(), _ => { }),
                ["OnOpenChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(new object(), _ => { }),
                ["OnInputValueChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(new object(), _ => { }),
            });

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());

        cut.WaitForAssertion(() => Assert.Equal(["apple"], validated));
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    [Fact(DisplayName = "prop: validationMode=onBlur")]
    [Trait("Requirement", "CMB-FRM-05")]
    public async Task Prop_validationMode_onBlur()
    {
        IRenderedComponent<FieldRoot> cut = Render(
            field => field.Add(x => x.ValidationMode, ValidationMode.OnBlur).Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>(args.Value as string == "apple" ? "error" : null)),
            new() { ["DefaultOpen"] = true });

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());
        // Selecting an item must not validate until the input is blurred.
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-invalid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    [Fact(DisplayName = "should receive name prop from Field.Root")]
    [Trait("Requirement", "CMB-API-25")]
    [Trait("Requirement", "CMB-FRM-01")]
    public void Should_receive_name_prop_from_Field_Root()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => field.Add(x => x.Name, "field-combobox"), new());

        Assert.Single(cut.FindAll("input[aria-hidden=true][name=field-combobox]"));
    }

    [Fact(DisplayName = "should receive disabled prop from Field.Root")]
    [Trait("Requirement", "CMB-API-30")]
    [Trait("Requirement", "CMB-FRM-06")]
    public void Should_receive_disabled_prop_from_Field_Root()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => field.Add(x => x.Disabled, true), new(), withTrigger: true);

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "Field.Description")]
    [Trait("Requirement", "CMB-ARIA-16")]
    [Trait("Requirement", "CMB-FRM-06")]
    public void Field_Description()
    {
        IRenderedComponent<FieldRoot> cut = Render(field => { }, new(), withDescription: true);

        Assert.Equal(cut.Find("[data-testid=description]").Id, cut.Find("[data-testid=input]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "CMB-FRM-03")]
    public async Task Clears_external_errors_on_change()
    {
        void Field(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FieldRoot>(0);
            builder.AddComponentParameter(1, nameof(FieldRoot.Name), "combobox");
            builder.AddComponentParameter(2, nameof(FieldRoot.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<ComboboxHarness>(0);
                inner.AddComponentParameter(1, nameof(ComboboxHarness.RootParameters), new Dictionary<string, object?> { ["DefaultOpen"] = true });
                inner.AddComponentParameter(2, nameof(ComboboxHarness.StaticItems), (IReadOnlyList<string>)s_fruits);
                inner.CloseComponent();
                inner.OpenComponent<FieldError>(3);
                inner.AddComponentParameter(4, "data-testid", "error");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<Form> cut = test.Context.Render<Form>(p => p.Add(x => x.Errors, new FormErrors { ["combobox"] = "test" }).Add(x => x.ChildContent, (RenderFragment)Field));
        Assert.Contains("test", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());
        // The Field.Error leaves after its exit transition, which the `transitions` module reports.
        TransitionsTwin[] exits = [.. test.Context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 2 && invocation.Arguments[2] is DotNetObjectReference<TransitionsTwin>)
            .Select(invocation => ((DotNetObjectReference<TransitionsTwin>)invocation.Arguments[2]!).Value)];
        foreach (TransitionsTwin exit in exits)
        {
            await cut.InvokeAsync(() => exit.OnTransitionComplete("""{"open":false}"""));
        }

        Assert.Empty(cut.FindAll("[data-testid=error]"));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "removes [data-dirty] when an equal object value is reselected (different reference)")]
    [Trait("Requirement", "CMB-FRM-03")]
    public async Task Removes_data_dirty_when_an_equal_object_value_is_reselected()
    {
        FruitOption[] options = [new("a", "a"), new("b", "b")];
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxTypedHarness<FruitOption>>(0);
            builder.AddComponentParameter(1, nameof(ComboboxTypedHarness<FruitOption>.RootParameters), new Dictionary<string, object?>
            {
                ["Multiple"] = true,
                ["DefaultOpen"] = true,
                ["Items"] = (IReadOnlyList<FruitOption>)options,
                // An equal value by the comparator, not by reference.
                ["DefaultValues"] = (IReadOnlyList<FruitOption>)[new FruitOption("a", "a")],
                ["IsItemEqualToValue"] = (Func<FruitOption, FruitOption, bool>)((item, value) => item.Id == value.Id),
                ["ItemToStringLabel"] = (Func<FruitOption, string>)(item => item.Label),
            });
            builder.AddComponentParameter(2, nameof(ComboboxTypedHarness<FruitOption>.WithTrigger), true);
            builder.AddComponentParameter(3, nameof(ComboboxTypedHarness<FruitOption>.ItemText), (Func<FruitOption, string>)(item => item.Label));
            builder.CloseComponent();
        }

        IRenderedComponent<FieldRoot> cut = test.Context.Render<FieldRoot>(p => p.AddChildContent((RenderFragment)Content));
        AngleSharp.Dom.IElement Option(string name) => cut.FindAll("[role=option]").Single(option => option.TextContent.Trim() == name);
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => Option("a").Click());
        Assert.True(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await cut.InvokeAsync(() => Option("a").Click());
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
    }

    private static IElement FieldElement(IRenderedComponent<FieldRoot> cut) => cut.Find("[data-testid=field]");

    private IRenderedComponent<FieldRoot> Render(Action<ComponentParameterCollectionBuilder<FieldRoot>> field, Dictionary<string, object?> root, bool withTrigger = false, bool withDescription = false)
    {
        void Extra(RenderTreeBuilder builder)
        {
            if (withTrigger)
            {
                builder.OpenComponent<ComboboxTrigger>(0);
                builder.AddComponentParameter(1, "data-testid", "trigger");
                builder.CloseComponent();
            }
        }

        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FieldLabel>(0);
            builder.AddComponentParameter(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Fruit")));
            builder.CloseComponent();
            builder.OpenComponent<ComboboxHarness>(2);
            builder.AddComponentParameter(3, nameof(ComboboxHarness.RootParameters), root);
            builder.AddComponentParameter(4, nameof(ComboboxHarness.StaticItems), (IReadOnlyList<string>)s_fruits);
            builder.AddComponentParameter(5, nameof(ComboboxHarness.Extra), (RenderFragment)Extra);
            builder.CloseComponent();
            if (withDescription)
            {
                builder.OpenComponent<FieldDescription>(6);
                builder.AddComponentParameter(7, "data-testid", "description");
                builder.CloseComponent();
            }
        }

        return test.Context.Render<FieldRoot>(p =>
        {
            p.AddUnmatched("data-testid", "field");
            field(p);
            p.AddChildContent((RenderFragment)Content);
        });
    }

    // A class, so two options with the same id are different references (Base UI's object literals).
    private sealed class FruitOption(string id, string label)
    {
        public string Id { get; } = id;

        public string Label { get; } = label;
    }
}
