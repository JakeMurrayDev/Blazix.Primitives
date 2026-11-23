using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): Autocomplete inside a Field at L2 (AUT-FRM-01, 02, 03, 05, 07; AutocompleteRoot.test.tsx's Field
// titles) over the engine's None paths: the Input is the field's control outside the popup, and the field's value is the
// displayed text, the inline completion included (§16 Q5).
/// <summary>Autocomplete inside a Field at L2.</summary>
public sealed class AutocompleteFieldTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "Field.Label")]
    [Trait("Requirement", "AUT-FRM-07")]
    public void Field_label_points_at_the_input()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items });
        IElement input = cut.Find("[data-testid=input]");

        Assert.False(string.IsNullOrEmpty(input.Id));
        Assert.Equal(input.Id, cut.Find("label").GetAttribute("for"));
    }

    [Fact(DisplayName = "submits the typed input value when wrapped in Field.Root")]
    [Trait("Requirement", "AUT-FRM-02")]
    public void The_input_takes_the_field_name()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items }, name: "search");

        Assert.Equal("search", cut.Find("[data-testid=input]").GetAttribute("name"));
    }

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "AUT-FRM-03")]
    public async Task Typing_marks_the_field_dirty_and_filled()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items });
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-filled"));

        await TypeAsync(cut, "ap");

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when already filled with defaultValue")]
    [Trait("Requirement", "AUT-FRM-03")]
    public void A_default_value_fills_the_field()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items, ["DefaultValue"] = "ap" });

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "[data-touched]")]
    [Trait("Requirement", "AUT-FRM-05")]
    public async Task Blur_touches_the_field()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items });
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-touched"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-touched"));
    }

    [Fact(DisplayName = "port: the field's value is the displayed text, so an inline completion fills it")]
    [Trait("Requirement", "AUT-FRM-01")]
    [Trait("Requirement", "AUT-STA-04")]
    public async Task An_inline_completion_fills_the_field()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items, ["Mode"] = AutocompleteMode.Inline, ["DefaultOpen"] = true });
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-filled"));

        string itemId = cut.Find("[data-testid=item-apple]").Id!;
        string json = $$"""{"index":0,"itemId":"{{itemId}}","event":{{ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        await cut.InvokeAsync(() => twin.OnListHighlight(json));

        Assert.Equal("apple", cut.FindComponent<AutocompleteHarness>().Instance.Root!.Store.State.InputValue);
        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "prop: validationMode=onChange")]
    [Trait("Requirement", "AUT-FRM-03")]
    public async Task Validation_mode_on_change_validates_the_typed_text()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items }, field: field => field
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                validated.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(args.Value as string == "ap" ? "error" : null);
            }));

        await TypeAsync(cut, "ap");

        Assert.Contains("ap", validated);
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    [Fact(DisplayName = "prop: validationMode=onBlur")]
    [Trait("Requirement", "AUT-FRM-05")]
    public async Task Validation_mode_on_blur_validates_on_blur()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items }, field: field => field
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>(args.Value as string == "ap" ? "error" : null)));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await TypeAsync(cut, "ap");
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-invalid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    [Fact(DisplayName = "[data-valid]")]
    [Trait("Requirement", "AUT-FRM-04")]
    [Trait("Requirement", "AUT-FRM-05")]
    public async Task Data_valid_after_a_valid_blur()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items }, field: field => field.Add(x => x.ValidationMode, ValidationMode.OnBlur));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-valid"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await TypeAsync(cut, "banana");
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-valid")));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-invalid"));
    }

    [Fact(DisplayName = "[data-focused]")]
    [Trait("Requirement", "AUT-FRM-05")]
    public async Task Data_focused_follows_the_input()
    {
        IRenderedComponent<FieldRoot> cut = Render(new() { ["Items"] = s_items });

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-focused"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "AUT-FRM-03")]
    public async Task Clears_external_errors_on_change()
    {
        void Field(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FieldRoot>(0);
            builder.AddComponentParameter(1, nameof(FieldRoot.Name), "search");
            builder.AddComponentParameter(2, nameof(FieldRoot.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<AutocompleteHarness>(0);
                inner.AddComponentParameter(1, nameof(AutocompleteHarness.RootParameters), new Dictionary<string, object?> { ["Items"] = s_items });
                inner.CloseComponent();
                inner.OpenComponent<FieldError>(2);
                inner.AddComponentParameter(3, "data-testid", "error");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<Form> cut = test.Context.Render<Form>(p => p.Add(x => x.Errors, new FormErrors { ["search"] = "test" }).Add(x => x.ChildContent, (RenderFragment)Field));
        Assert.Contains("test", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid"));

        string json = $$"""{"text":"b","typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => twin.OnInputChange(json));

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-invalid")));
    }

    private IRenderedComponent<FieldRoot> Render(Dictionary<string, object?> root, string? name = null, Action<ComponentParameterCollectionBuilder<FieldRoot>>? field = null) =>
        test.Context.Render<FieldRoot>(p =>
        {
            p.Add(x => x.Name, name);
            field?.Invoke(p);
            p.AddChildContent(builder =>
            {
                builder.OpenComponent<FieldLabel>(0);
                builder.AddComponentParameter(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Search")));
                builder.CloseComponent();
                builder.OpenComponent<AutocompleteHarness>(2);
                builder.AddComponentParameter(3, nameof(AutocompleteHarness.RootParameters), root);
                builder.CloseComponent();
            });
        });

    private Task TypeAsync(IRenderedComponent<FieldRoot> cut, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }
}
