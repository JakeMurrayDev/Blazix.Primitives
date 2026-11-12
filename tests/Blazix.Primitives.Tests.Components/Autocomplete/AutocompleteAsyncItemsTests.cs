using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): AutocompleteRoot.test.tsx's "asynchronous items" at L2 (AUT-EDGE-04, API-11, API-16, STA-11,
// PTR-06): items that arrive after the typing take the automatic highlight unless a clear, a close or a blur discarded the
// request. L2 asserts the highlight C# requests (the store's ActiveIndex); data-highlighted and aria-activedescendant are the
// list handle's to write (L4), the brief's Deliverable 6.
/// <summary>Autocomplete's asynchronous items at L2.</summary>
public sealed class AutocompleteAsyncItemsTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_results = ["32", "320"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "highlights a candidate arriving after typing")]
    [Trait("Requirement", "AUT-EDGE-04")]
    [Trait("Requirement", "AUT-API-11")]
    public async Task Highlights_a_candidate_arriving_after_typing()
    {
        IRenderedComponent<AutocompleteAsyncHost> cut = test.Context.Render<AutocompleteAsyncHost>();
        await TypeAsync(cut, "32");

        cut.Render(p => p.Add(x => x.Items, s_results));

        Assert.Equal(0, Store(cut).State.ActiveIndex);
        Assert.Equal(2, cut.FindAll("[role=option]").Count);
    }

    [Fact(DisplayName = "highlights asynchronous results while an inline input stays focused")]
    [Trait("Requirement", "AUT-EDGE-04")]
    [Trait("Requirement", "AUT-API-16")]
    public async Task Highlights_asynchronous_results_while_an_inline_input_stays_focused()
    {
        IRenderedComponent<AutocompleteAsyncHost> cut = test.Context.Render<AutocompleteAsyncHost>(p => p.Add(x => x.InlineLayout, true));
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await TypeAsync(cut, "32");

        cut.Render(p => p.Add(x => x.Items, (IReadOnlyList<string>)["32"]));

        Assert.Equal(0, Store(cut).State.ActiveIndex);
    }

    [Theory(DisplayName = "handles asynchronous results after an inline input blurs with autoHighlight=%s")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "AUT-EDGE-04")]
    [Trait("Requirement", "AUT-STA-11")]
    public async Task Handles_asynchronous_results_after_an_inline_input_blurs(bool always)
    {
        IRenderedComponent<AutocompleteAsyncHost> cut = test.Context.Render<AutocompleteAsyncHost>(p => p
            .Add(x => x.InlineLayout, true)
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["AutoHighlight"] = always ? AutoHighlightValue.Always : (AutoHighlightValue?)true }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await TypeAsync(cut, "32");
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.Render(p => p.Add(x => x.Items, (IReadOnlyList<string>)["32"]));

        Assert.Equal(always ? 0 : null, Store(cut).State.ActiveIndex);
    }

    [Fact(DisplayName = "discards the request when the clear button clears the query")]
    [Trait("Requirement", "AUT-EDGE-04")]
    [Trait("Requirement", "AUT-PTR-06")]
    public async Task Discards_the_request_when_the_clear_button_clears_the_query()
    {
        IRenderedComponent<AutocompleteAsyncHost> cut = test.Context.Render<AutocompleteAsyncHost>();
        await TypeAsync(cut, "32");
        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        cut.Render(p => p.Add(x => x.Items, (IReadOnlyList<string>)["32"]));

        Assert.Equal(string.Empty, Store(cut).State.InputValue);
        Assert.Null(Store(cut).State.ActiveIndex);
    }

    [Theory(DisplayName = "discards the request when closing with keepMounted=%s")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "AUT-EDGE-04")]
    public async Task Discards_the_request_when_closing(bool keepMounted)
    {
        IRenderedComponent<AutocompleteAsyncHost> cut = test.Context.Render<AutocompleteAsyncHost>(p => p.Add(x => x.KeepMounted, keepMounted));
        await TypeAsync(cut, "32");
        Assert.True(Store(cut).State.Lifecycle.EffectiveOpen);
        await SessionRequestAsync(cut, open: false, "escape-key", ComboboxTestContext.Key("Escape"));
        await cut.InvokeAsync(() => Store(cut).CompleteTransitionAsync(false));
        Assert.False(Store(cut).State.Lifecycle.EffectiveOpen);
        await SessionRequestAsync(cut, open: true, "trigger-press", ComboboxTestContext.Click);

        cut.Render(p => p.Add(x => x.Items, (IReadOnlyList<string>)["32"]));

        Assert.Equal("32", Store(cut).State.InputValue);
        Assert.Null(Store(cut).State.ActiveIndex);
    }

    [Fact(DisplayName = "discards the request when a controlled popup closes")]
    [Trait("Requirement", "AUT-EDGE-04")]
    public async Task Discards_the_request_when_a_controlled_popup_closes()
    {
        IRenderedComponent<AutocompleteAsyncHost> cut = test.Context.Render<AutocompleteAsyncHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true }));
        await TypeAsync(cut, "32");

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = false }));
        await cut.InvokeAsync(() => Store(cut).CompleteTransitionAsync(false));
        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true }));
        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true }).Add(x => x.Items, (IReadOnlyList<string>)["32"]));

        Assert.Equal("32", Store(cut).State.InputValue);
        Assert.Null(Store(cut).State.ActiveIndex);
    }

    private static ComboboxStore<string> Store(IRenderedComponent<AutocompleteAsyncHost> cut) => cut.Instance.Root!.Store;

    private Task SessionRequestAsync(IRenderedComponent<AutocompleteAsyncHost> cut, bool open, string reason, string eventJson)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{eventJson}},"triggerId":null}""";
        InteractionsTwin twin = test.SessionTwin;
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    private Task TypeAsync(IRenderedComponent<AutocompleteAsyncHost> cut, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }
}
