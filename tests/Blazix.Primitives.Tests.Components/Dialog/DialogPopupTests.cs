using AngleSharp.Dom;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogPopup.test.tsx</c>: the missing root (DLG-EDGE-02), <c>keepMounted</c> (DLG-API-17,
/// DOM-18, ANI-01), the nested count (DLG-STA-04/05, DOM-19) and the style hooks (DLG-STR-12, STA-03, STA-13, DOM-21/22).
/// Titles are Base UI's. Base UI's nested-count titles read <c>getComputedStyle</c> in a browser; the inline declaration C#
/// writes in the commit path is the same value, observed here in the markup of the batch that changed the count. The
/// nested dialogs open and close through their controlled <c>Open</c> (the count's path is the commit, whatever the
/// request's origin); the AlertDialog titles are track 9's.
/// </summary>
public sealed class DialogPopupTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Dialog.Root>")]
    [Trait("Requirement", "DLG-EDGE-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<DialogPopup>());
        Assert.Equal("Blazix: The `DialogRoot` context is missing. Dialog parts must be placed within `<DialogRoot>`.", error.Message);
    }

    [Fact(DisplayName = "should keep the dialog mounted when keepMounted=true")]
    [Trait("Requirement", "DLG-API-17")]
    [Trait("Requirement", "DLG-DOM-18")]
    [Trait("Requirement", "DLG-ANI-01")]
    public void Should_keep_the_dialog_mounted_when_keepMounted_is_true()
    {
        IRenderedComponent<DialogHarness> cut = Closed(keepMounted: true);

        IElement popup = cut.Find("[role=dialog]");
        Assert.True(popup.HasAttribute("hidden"));
    }

    [Theory(DisplayName = "should not keep the dialog mounted when keepMounted=")]
    [InlineData("false")]
    [InlineData("undefined")]
    [Trait("Requirement", "DLG-API-17")]
    [Trait("Requirement", "DLG-ANI-01")]
    public void Should_not_keep_the_dialog_mounted_when_keepMounted_is(string name)
    {
        IRenderedComponent<DialogHarness> cut = Closed(keepMounted: name == "false" ? false : null);

        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "provides the number of open nested dialogs as a CSS variable")]
    [Trait("Requirement", "DLG-STA-04")]
    [Trait("Requirement", "DLG-DOM-19")]
    public void Provides_the_number_of_open_nested_dialogs_as_a_CSS_variable()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p
            .Add(x => x.Levels, 3)
            .Add(x => x.Level2Open, false)
            .Add(x => x.Level3Open, false));
        Assert.Equal("0", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level2Open, true));
        Assert.Equal("1", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level3Open, true));
        Assert.Equal("2", NestedDialogs(cut, 1));
        Assert.Equal("1", NestedDialogs(cut, 2));

        cut.Render(p => p.Add(x => x.Level3Open, false));
        Assert.Equal("1", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level2Open, false));
        Assert.Equal("0", NestedDialogs(cut, 1));
    }

    [Fact(DisplayName = "decrements the count when an open nested dialog is unmounted")]
    [Trait("Requirement", "DLG-STA-05")]
    [Trait("Requirement", "DLG-DOM-19")]
    public void Decrements_the_count_when_an_open_nested_dialog_is_unmounted()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 2));
        Assert.Equal("1", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level2Mounted, false));

        Assert.Equal("0", NestedDialogs(cut, 1));
        Assert.False(cut.Find("[data-testid=level-1-popup]").HasAttribute("data-nested-dialog-open"));
    }

    [Fact(DisplayName = "does not change the count when a closed nested dialog is unmounted")]
    [Trait("Requirement", "DLG-STA-05")]
    public void Does_not_change_the_count_when_a_closed_nested_dialog_is_unmounted()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 2).Add(x => x.Level2Open, false));
        Assert.Equal("0", NestedDialogs(cut, 1));
        Assert.Single(cut.FindAll("[data-testid=level-2-trigger]"));

        cut.Render(p => p.Add(x => x.Level2Mounted, false));

        Assert.Empty(cut.FindAll("[data-testid=level-2-trigger]"));
        Assert.Equal("0", NestedDialogs(cut, 1));
    }

    [Fact(DisplayName = "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog")]
    [Trait("Requirement", "DLG-STR-12")]
    [Trait("Requirement", "DLG-STA-03")]
    [Trait("Requirement", "DLG-STA-13")]
    [Trait("Requirement", "DLG-DOM-21")]
    [Trait("Requirement", "DLG-DOM-22")]
    public void Adds_the_nested_and_nested_dialog_open_style_hooks_if_a_dialog_has_a_parent_dialog()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 3).Add(x => x.Level3Open, false));

        IElement parent = cut.Find("[data-testid=level-1-popup]");
        IElement nested = cut.Find("[data-testid=level-2-popup]");
        Assert.False(parent.HasAttribute("data-nested"));
        Assert.True(nested.HasAttribute("data-nested"));
        Assert.True(parent.HasAttribute("data-nested-dialog-open"));
        Assert.False(nested.HasAttribute("data-nested-dialog-open"));
    }

    private static string NestedDialogs(IRenderedComponent<DialogNestedHarness> cut, int level)
    {
        string style = cut.Find($"[data-testid=level-{level}-popup]").GetAttribute("style") ?? string.Empty;
        const string prefix = "--nested-dialogs: ";
        int start = style.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no --nested-dialogs in \"{style}\"");
        int end = style.IndexOf(';', start);
        return (end < 0 ? style[(start + prefix.Length)..] : style[(start + prefix.Length)..end]).Trim();
    }

    private IRenderedComponent<DialogHarness> Closed(bool? keepMounted) => test.Context.Render<DialogHarness>(p =>
    {
        p.Add(x => x.ControlledOpen, false).Add(x => x.Modal, false).Add(x => x.WithTrigger, false);
        if (keepMounted is { } value)
        {
            p.Add(x => x.KeepMounted, value);
        }
    });

    // DLG-KEY-09 (dialog/popup/DialogPopup.tsx:82-86): the Popup stops the composite keys unconditionally, through the
    // `popup` module's Popup role (PopupElementOptions.StopCompositeKeys, M4 track 5's name), so a dialog opened from a
    // toolbar item never moves the toolbar's focus.
    [Fact(DisplayName = "port: the popup attaches the popup module's Popup role and stops the composite keys")]
    [Trait("Requirement", "DLG-KEY-09")]
    public void The_popup_attaches_the_popup_modules_popup_role_and_stops_the_composite_keys()
    {
        test.Context.Render<DialogHarness>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.Modal, false));

        string options = Assert.Single(DialogTestContext.AttachOptions(test.PopupModule))!;
        Assert.Contains("\"popup\":{\"stopCompositeKeys\":true}", options, StringComparison.Ordinal);
    }
}
