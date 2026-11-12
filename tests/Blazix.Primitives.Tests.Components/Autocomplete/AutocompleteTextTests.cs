using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): Autocomplete's text at L2 (AUT-STA-04..09). The Root owns the committed text and gives the engine
// the displayed text at every parameter set (design point 1 (i), settled by the orchestrator 2026-11-12: the engine always
// considers the text given, AutocompleteRoot.tsx:107-125).
/// <summary>Autocomplete's text at L2.</summary>
public sealed class AutocompleteTextTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: an Input mounted inside the popup keeps the typed text of an autocomplete given no value")]
    [Trait("Requirement", "AUT-STA-09")]
    [Trait("Requirement", "AUT-FOC-01")]
    public async Task An_input_mounted_inside_the_popup_keeps_the_typed_text()
    {
        IRenderedComponent<AutocompletePopupInputHost> cut = test.Context.Render<AutocompletePopupInputHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["DefaultOpen"] = true }));
        await TypeAsync(cut, "ap");
        Assert.Equal("ap", cut.Find("[data-testid=trigger]").TextContent);

        cut.Render(p => p.Add(x => x.ShowInput, false));
        cut.Render(p => p.Add(x => x.ShowInput, true));

        Assert.Equal("ap", cut.Find("[data-testid=trigger]").TextContent);
        Assert.Equal("ap", cut.Instance.Root!.Store.State.InputValue);
    }

    [Fact(DisplayName = "port: with an inline list and no Popup, an item press does not fill the input")]
    [Trait("Requirement", "AUT-PTR-02")]
    [Trait("Requirement", "AUT-API-16")]
    public async Task An_inline_list_without_a_popup_does_not_fill_the_input()
    {
        List<string> texts = [];
        IRenderedComponent<AutocompleteInlineHost> cut = test.Context.Render<AutocompleteInlineHost>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>
        {
            ["Items"] = s_items,
            ["Inline"] = true,
            ["Open"] = true,
            ["OnValueChange"] = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args => texts.Add(args.Value)),
        }));
        await TypeAsync(cut, "an");

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.Equal("an", cut.Instance.Root!.Store.State.InputValue);
        Assert.Equal(["an"], texts);
        Assert.True(cut.Instance.Root!.Store.HasActivatedValue);
    }

    private Task TypeAsync<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }
}
