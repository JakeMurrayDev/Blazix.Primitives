using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): the brief's proposed layers for AUT-STA-10 and AUT-EDGE-01 (Test: none): the query is not frozen
// on close in None mode, and the port's text diagnostics (ADR 05 Amendments 2026-10-18 item 1, the row's port clause).
/// <summary>Autocomplete's STA-10 and EDGE-01 at L2.</summary>
public sealed class AutocompleteStateRowsTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    private readonly CapturingLogger<AutocompleteRoot<string>> logger = new();

    // The logger is registered before the renderer exists, which setting the renderer's info creates.
    public AutocompleteStateRowsTests()
    {
        test.Context.Services.AddSingleton<ILogger<AutocompleteRoot<string>>>(logger);
        test.Context.Renderer.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", isInteractive: true));
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: no query is frozen on close, so the exiting list follows the filled text")]
    [Trait("Requirement", "AUT-STA-10")]
    public async Task No_query_is_frozen_on_close()
    {
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["DefaultOpen"] = true }));
        string json = $$"""{"text":"a","typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => twin.OnInputChange(json));
        Assert.Equal(["apple", "banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.False(cut.Instance.Root!.Store.State.Lifecycle.EffectiveOpen);
        Assert.Equal(["banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "port: a text that stops being controlled logs the controlled-state warning once, naming Autocomplete")]
    [Trait("Requirement", "AUT-EDGE-01")]
    public void A_text_that_stops_being_controlled_warns_once()
    {
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["Value"] = "a" }));
        Assert.Empty(logger.Entries);

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items }));

        (LogLevel _, string message) = Assert.Single(logger.Entries);
        Assert.StartsWith("A component is changing the controlled value state of Autocomplete to be uncontrolled.\n", message, StringComparison.Ordinal);
    }
}
