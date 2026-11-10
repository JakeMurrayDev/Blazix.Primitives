using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.7 at L2: the input's own handlers. The `composite` list handle declines the consumer's onkeydown and runs it from the
// part's own handler, once per event (a decision on WebAssembly runs it inside the dispatch instead).
/// <summary>Combobox's Input at L2.</summary>
public sealed class ComboboxInputTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the consumer's onkeydown on the input runs once per keydown")]
    [Trait("Requirement", "RND-API-04")]
    public async Task The_consumers_onkeydown_runs_once_per_keydown()
    {
        List<string> keys = [];
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.AddComponentParameter(1, "data-testid", "input");
            builder.AddComponentParameter(2, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => keys.Add(e.Key)));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)Extra));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").KeyDown(new KeyboardEventArgs { Key = "a" }));

        Assert.Equal(["a"], keys);
    }
}
