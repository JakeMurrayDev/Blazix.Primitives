using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): AutocompleteRoot.test.tsx titles the port-coverage count listed as not ported (2026-11-12): the
// filter in Inline and Both modes, the completion against a controlled value and read-only, and the Field titles. L2 asserts
// what C# computes and sends.
/// <summary>Autocomplete's remaining Root titles at L2.</summary>
public sealed class AutocompletePortBatchTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "mode=\"inline\": does not call a custom filter")]
    [Trait("Requirement", "AUT-API-09")]
    [Trait("Requirement", "AUT-STA-07")]
    public async Task Inline_does_not_call_a_custom_filter()
    {
        int calls = 0;
        Func<string, string, Func<string, string>?, bool> filter = (_, _, _) => { calls++; return false; };
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["Mode"] = AutocompleteMode.Inline, ["Filter"] = filter });

        await TypeAsync(cut, "ap");

        Assert.Equal(0, calls);
        Assert.Equal(3, cut.FindAll("[role=option]").Count);
    }

    [Fact(DisplayName = "mode=\"both\": keeps filtering against the typed query during inline completion")]
    [Trait("Requirement", "AUT-STA-07")]
    [Trait("Requirement", "AUT-API-04")]
    public async Task Both_keeps_filtering_against_the_typed_query()
    {
        List<string> queries = [];
        Func<string, string, Func<string, string>?, bool> filter = (item, query, _) => { queries.Add(query); return item.Contains(query, StringComparison.Ordinal); };
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["Mode"] = AutocompleteMode.Both, ["Filter"] = filter });
        await TypeAsync(cut, "a");
        queries.Clear();

        await HighlightAsync(cut, 0, "apple");

        Assert.Equal("apple", Text(cut));
        Assert.All(queries, query => Assert.Equal("a", query));
        Assert.Equal(["apple", "banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "drops a pending inline completion when readOnly is turned on")]
    [Trait("Requirement", "AUT-API-17")]
    [Trait("Requirement", "AUT-STA-06")]
    public async Task Drops_a_pending_completion_when_read_only_is_turned_on()
    {
        Dictionary<string, object?> parameters = new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["Mode"] = AutocompleteMode.Both };
        IRenderedComponent<AutocompleteHarness> cut = Render(parameters);
        await TypeAsync(cut, "a");
        await HighlightAsync(cut, 0, "apple");
        Assert.Equal("apple", Text(cut));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>(parameters) { ["ReadOnly"] = true }));
        Assert.Equal("a", Text(cut));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>(parameters) { ["ReadOnly"] = false }));
        Assert.Equal("a", Text(cut));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when input has content")]
    [Trait("Requirement", "AUT-FRM-03")]
    public async Task Adds_data_filled_when_the_input_has_content()
    {
        IRenderedComponent<FieldRoot> cut = RenderField(field => { });
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-filled"));

        await TypeAsync(cut, "b");

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "[data-invalid]")]
    [Trait("Requirement", "AUT-FRM-07")]
    public void Data_invalid_from_the_field()
    {
        IRenderedComponent<FieldRoot> cut = RenderField(field => field.Add(x => x.Invalid, true));

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-invalid"));
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "Field.Description")]
    [Trait("Requirement", "AUT-FRM-07")]
    public void Field_description_describes_the_input()
    {
        IRenderedComponent<FieldRoot> cut = RenderField(field => { }, withDescription: true);

        Assert.Equal(cut.Find("[data-testid=description]").Id, cut.Find("[data-testid=input]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "prop: validate")]
    [Trait("Requirement", "AUT-FRM-05")]
    public async Task Prop_validate()
    {
        IRenderedComponent<FieldRoot> cut = RenderField(field => field
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>(args.Value as string == "bad" ? "error" : null)));
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await TypeAsync(cut, "bad");

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid")));
    }

    private static string Text(IRenderedComponent<AutocompleteHarness> cut) => cut.Instance.Root!.Store.State.InputValue;

    private IRenderedComponent<AutocompleteHarness> Render(Dictionary<string, object?> parameters) =>
        test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, parameters));

    private IRenderedComponent<FieldRoot> RenderField(Action<ComponentParameterCollectionBuilder<FieldRoot>> field, bool withDescription = false) =>
        test.Context.Render<FieldRoot>(p =>
        {
            field(p);
            p.AddChildContent(builder =>
            {
                builder.OpenComponent<AutocompleteHarness>(0);
                builder.AddComponentParameter(1, nameof(AutocompleteHarness.RootParameters), new Dictionary<string, object?> { ["Items"] = s_items });
                builder.CloseComponent();
                if (withDescription)
                {
                    builder.OpenComponent<FieldDescription>(2);
                    builder.AddComponentParameter(3, "data-testid", "description");
                    builder.CloseComponent();
                }
            });
        });

    private Task TypeAsync<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : IComponent
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    private Task HighlightAsync(IRenderedComponent<AutocompleteHarness> cut, int index, string name)
    {
        string json = $$"""{"index":{{index}},"itemId":"{{cut.Find("[data-testid=item-" + name + "]").Id}}","event":{{ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnListHighlight(json));
    }
}
