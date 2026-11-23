using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// AutocompleteRoot reads RendererInfo.IsInteractive (design point 8 (a) of M5 track 11), which bUnit requires a test to set:
// the Autocomplete tests render as an interactive Server renderer, over the Combobox test context's faked modules.
/// <summary>Creates the Autocomplete tests' context.</summary>
internal static class AutocompleteTesting
{
    /// <summary>Creates a Combobox test context whose renderer reports itself as interactive.</summary>
    /// <returns>The context.</returns>
    public static ComboboxTestContext CreateContext()
    {
        ComboboxTestContext test = new();
        test.Context.Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return test;
    }
}
