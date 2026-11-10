using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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

    private static IElement FieldElement(IRenderedComponent<FieldRoot> cut) => cut.Find("[data-testid=field]");

    private IRenderedComponent<FieldRoot> Render(Action<ComponentParameterCollectionBuilder<FieldRoot>> field, Dictionary<string, object?> root, bool withTrigger = false)
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
        }

        return test.Context.Render<FieldRoot>(p =>
        {
            p.AddUnmatched("data-testid", "field");
            field(p);
            p.AddChildContent((RenderFragment)Content);
        });
    }
}
