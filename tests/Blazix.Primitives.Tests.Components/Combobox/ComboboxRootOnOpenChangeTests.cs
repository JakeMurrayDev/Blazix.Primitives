using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-07 and EVT-01 at L2 (ComboboxRoot.test.tsx, describe "prop: onOpenChange"): the callback reports the open of a press
// of the input and the close of an outside press, both the `interactions` module's reports.
/// <summary>ComboboxRoot's OnOpenChange at L2.</summary>
public sealed class ComboboxRootOnOpenChangeTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "fires when opening and closing")]
    [Trait("Requirement", "CMB-API-07")]
    [Trait("Requirement", "CMB-EVT-01")]
    public async Task Fires_when_opening_and_closing()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.StaticItems, ["a"])
            .Add(x => x.Extra, Trigger));

        await test.PressAsync(cut, "[data-testid=input]", "input-press");
        Assert.NotEmpty(cut.Instance.OpenChanges);
        Assert.True(cut.Instance.OpenChanges[^1].Open);

        await test.DismissAsync(cut, "outside-press");
        Assert.False(cut.Instance.OpenChanges[^1].Open);
    }

    private static RenderFragment Trigger => builder =>
    {
        builder.OpenComponent<ComboboxTrigger>(0);
        builder.AddComponentParameter(1, "data-testid", "trigger");
        builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Open")));
        builder.CloseComponent();
    };
}
