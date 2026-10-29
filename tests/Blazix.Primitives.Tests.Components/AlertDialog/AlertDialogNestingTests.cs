using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The AlertDialog titles of <c>DialogPopup.test.tsx</c> (titles verbatim), which track 8 left open to this track: an
/// AlertDialog root provides the shared dialog context, so nesting and its count cross the families (ADL-STR-03, ADL-STA-02;
/// DLG-STA-04, DLG-STR-12, DLG-DOM-21/22). Base UI reads <c>getComputedStyle</c>; the inline declaration C# writes in the
/// commit path is the same value (DialogPopupTests' reading).
/// </summary>
public sealed class AlertDialogNestingTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "increments for nested alert dialog and decrements on close (cross-type)")]
    [Trait("Requirement", "ADL-STR-03")]
    [Trait("Requirement", "ADL-STA-02")]
    [Trait("Requirement", "ADL-DIS-03")]
    [Trait("Requirement", "DLG-STA-04")]
    [Trait("Requirement", "DLG-DOM-19")]
    public async Task Increments_for_nested_alert_dialog_and_decrements_on_close_cross_type()
    {
        IRenderedComponent<AlertDialogNestingHarness> cut = test.Context.Render<AlertDialogNestingHarness>();

        await test.PressAsync(cut, "open-dialog", open: true);
        Assert.Equal("0", NestedDialogs(cut.Find("[data-testid=parent-dialog]")));

        await test.PressAsync(cut, "open-alert", open: true);
        Assert.Single(cut.FindAll("[data-testid=nested-alert]"));
        Assert.Equal("1", NestedDialogs(cut.Find("[data-testid=parent-dialog]")));

        await cut.Find("[data-testid=close-alert]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal("0", NestedDialogs(cut.Find("[data-testid=parent-dialog]")));
    }

    [Fact(DisplayName = "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent alert dialog")]
    [Trait("Requirement", "ADL-STR-03")]
    [Trait("Requirement", "DLG-STR-12")]
    [Trait("Requirement", "DLG-STA-03")]
    [Trait("Requirement", "DLG-DOM-21")]
    [Trait("Requirement", "DLG-DOM-22")]
    public void Adds_the_nested_and_nested_dialog_open_style_hooks_if_a_dialog_has_a_parent_alert_dialog()
    {
        IRenderedComponent<AlertDialogNestingHarness> cut = test.Context.Render<AlertDialogNestingHarness>(p => p.Add(x => x.Shape, "parent-alert"));

        IElement parent = cut.Find("[data-testid=parent-dialog]");
        IElement nested = cut.Find("[data-testid=nested-dialog]");
        Assert.Equal("alertdialog", parent.GetAttribute("role"));
        Assert.False(parent.HasAttribute("data-nested"));
        Assert.True(nested.HasAttribute("data-nested"));
        Assert.True(parent.HasAttribute("data-nested-dialog-open"));
        Assert.False(nested.HasAttribute("data-nested-dialog-open"));
    }

    // The popup's inline --nested-dialogs declaration, which C# writes in the commit path.
    private static string NestedDialogs(IElement popup)
    {
        string style = popup.GetAttribute("style") ?? string.Empty;
        const string Prefix = "--nested-dialogs: ";
        int start = style.IndexOf(Prefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no --nested-dialogs in \"{style}\"");
        int end = style.IndexOf(';', start);
        return (end < 0 ? style[(start + Prefix.Length)..] : style[(start + Prefix.Length)..end]).Trim();
    }
}
