using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196; the brief's L2 plan, PR 245's final review fix 1): render counts through ClassValue, quoted in the
// PR (the lesson of issue 110), and design point 8's interactive flag adding no render.
/// <summary>Autocomplete's render counts at L2.</summary>
public sealed class AutocompleteMeasurementTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: an arrow press in Both mode renders the Value and the highlighted item only, not the Input or the Clear")]
    [Trait("Requirement", "AUT-STA-05")]
    [Trait("Requirement", "ADR16-7")]
    public async Task An_arrow_press_in_both_renders_the_input_and_clear_and_no_item()
    {
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>
        {
            ["Items"] = s_items, ["DefaultOpen"] = true, ["Mode"] = AutocompleteMode.Both, ["DefaultValue"] = "a",
        }));
        int input = cut.Instance.InputStates.Count;
        int clear = cut.Instance.ClearStates.Count;
        int items = cut.Instance.ItemStates.Count;

        string json = $$"""{"index":0,"itemId":"{{cut.Find("[data-testid=item-apple]").Id}}","event":{{ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        await cut.InvokeAsync(() => twin.OnListHighlight(json));

        Assert.Equal("apple", cut.Instance.Root!.Store.State.InputValue);
        Assert.Equal("apple", cut.Find("[data-testid=trigger]").TextContent);
        // Measured 2026-11-12: the Input's text reaches it through its update with no render, the Clear's state is unchanged,
        // and the newly highlighted item renders once for its mirrored Highlighted, as Combobox's arrow does.
        Assert.Equal(0, cut.Instance.InputStates.Count - input);
        Assert.Equal(0, cut.Instance.ClearStates.Count - clear);
        Assert.Equal(1, cut.Instance.ItemStates.Count - items);
    }

    [Fact(DisplayName = "port: the interactive flag adds no render: a static and an interactive renderer render the parts equally often")]
    [Trait("Requirement", "AUT-EDGE-03")]
    [Trait("Requirement", "ADR16-7")]
    public void The_interactive_flag_adds_no_render()
    {
        int Renders(bool interactive)
        {
            using ComboboxTestContext context = new();
            context.Context.Renderer.SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
            IRenderedComponent<AutocompleteHarness> cut = context.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["Name"] = "tag" }));
            return cut.Instance.InputStates.Count + cut.Instance.ClearStates.Count;
        }

        Assert.Equal(Renders(interactive: false), Renders(interactive: true));
    }
}
