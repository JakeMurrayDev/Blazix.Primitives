using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The nested-drawer titles of <c>DrawerPopup.test.tsx</c> (titles verbatim; DRW-STA-18, STR-10, DOM-03, DOM-08, DOM-22),
/// <c>DialogRoot.test.tsx</c>'s "reports nested drawer counts before passive effects" (DLG-STA-05, DLG-EVT-10, DRW-STA-18),
/// and the port titles of DRW-STR-10 through a dialog and DLG-DIS-10's drawer sentence. The counts are the dialog store's,
/// reported by each root in the commit path (DLG-STA-05): a nested drawer reports itself in the drawer count, a nested
/// Dialog or AlertDialog only in the dialog count. Base UI's MutationObserver on the parent popup's style is the parent
/// root's count probe, which records every drawer count the store notifies. "Before passive effects" is the render batch:
/// the parent popup shows the new count in the batch that opened, closed or removed the nested drawer.
/// </summary>
public sealed class DrawerNestingTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not treat dialogs inside nested drawers as nested drawers")]
    [Trait("Requirement", "DRW-STA-18")]
    [Trait("Requirement", "DRW-STR-10")]
    [Trait("Requirement", "DRW-DOM-08")]
    [Trait("Requirement", "DRW-DOM-22")]
    public Task Does_not_treat_dialogs_inside_nested_drawers_as_nested_drawers() => DialogsInsideNestedDrawersAsync("dialog-inside");

    [Fact(DisplayName = "does not treat alert dialogs inside nested drawers as nested drawers")]
    [Trait("Requirement", "DRW-STA-18")]
    [Trait("Requirement", "DRW-STR-10")]
    [Trait("Requirement", "DRW-DOM-08")]
    [Trait("Requirement", "DRW-DOM-22")]
    public Task Does_not_treat_alert_dialogs_inside_nested_drawers_as_nested_drawers() => DialogsInsideNestedDrawersAsync("alert-inside");

    [Fact(DisplayName = "keeps the parent nested state when a closed handle-backed nested drawer mounts")]
    [Trait("Requirement", "DRW-STA-18")]
    [Trait("Requirement", "DRW-DOM-03")]
    public void Keeps_the_parent_nested_state_when_a_closed_handle_backed_nested_drawer_mounts()
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p.Add(x => x.Shape, "siblings"));

        Assert.Single(cut.FindAll("[data-testid=open-child]"));
        Assert.Empty(cut.FindAll("[data-testid=closed-child]"));
        IElement parent = cut.Find("[data-testid=parent-popup]");
        Assert.True(parent.HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));
        Assert.Equal("1", NestedDrawers(parent));
    }

    [Fact(DisplayName = "clears parent nested drawer state as soon as a nested drawer closes")]
    [Trait("Requirement", "DRW-STA-18")]
    [Trait("Requirement", "DRW-DOM-03")]
    [Trait("Requirement", "DRW-DOM-08")]
    public async Task Clears_parent_nested_drawer_state_as_soon_as_a_nested_drawer_closes()
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p.Add(x => x.Shape, "close-nested"));
        IElement parent = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("0", NestedDrawers(parent));
        Assert.False(parent.HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));

        await test.PressAsync(cut, "open-nested", open: true);
        parent = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("1", NestedDrawers(parent));
        Assert.Equal(string.Empty, parent.GetAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));

        // The nested drawer's exit has not ended (no transitions module): its popup is still there.
        await cut.Find("[data-testid=close-nested]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Single(cut.FindAll("[data-testid=child-popup]"));
        parent = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("0", NestedDrawers(parent));
        Assert.False(parent.HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));
    }

    // DialogRoot.test.tsx constructs a drawer-mode DialogInteractions under a parent store and records when the parent hears
    // the counts. Here the parent is a drawer, whose popup renders the drawer count: it shows the nested drawer's report in
    // the batch that opened it and the cleared count in the batch that removed it, each in one render of the parent popup.
    [Fact(DisplayName = "reports nested drawer counts before passive effects")]
    [Trait("Requirement", "DLG-STA-05")]
    [Trait("Requirement", "DLG-EVT-10")]
    [Trait("Requirement", "DRW-STA-18")]
    [Trait("Requirement", "DRW-DOM-03")]
    public void Reports_nested_drawer_counts_before_passive_effects()
    {
        List<bool> parentStates = [];
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p
            .Add(x => x.ChildOpen, false)
            .Add(x => x.ParentClassValue, state =>
            {
                parentStates.Add(state.NestedDrawerOpen);
                return null;
            }));
        Assert.Equal("0", NestedDrawers(cut.Find("[data-testid=parent-popup]")));
        Assert.Equal("0", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-drawers"));

        parentStates.Clear();
        cut.Render(p => p.Add(x => x.ChildOpen, true));
        IElement parent = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("1", NestedDrawers(parent));
        Assert.True(parent.HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));
        Assert.Equal("1", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-drawers"));
        Assert.Equal("1", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-dialogs"));
        Assert.True(parentStates[^1]);

        parentStates.Clear();
        cut.Render(p => p.Add(x => x.ChildOpen, true).Add(x => x.ChildMounted, false));
        Assert.Empty(cut.FindAll("[data-testid=child-popup]"));
        parent = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("0", NestedDrawers(parent));
        Assert.False(parent.HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));
        Assert.Equal("0", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-drawers"));
        Assert.False(parentStates[^1]);
    }

    [Fact(DisplayName = "port: a drawer nested through a dialog reports to the nearest enclosing drawer root, and counts as one nested drawer")]
    [Trait("Requirement", "DRW-STR-10")]
    [Trait("Requirement", "DRW-STA-18")]
    [Trait("Requirement", "DRW-DOM-08")]
    public void A_drawer_nested_through_a_dialog_reports_to_the_nearest_enclosing_drawer_root()
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p.Add(x => x.Shape, "through-dialog"));

        DrawerRootContext parent = cut.Instance.Parent!.DrawerContext;
        Assert.Same(parent, cut.Instance.Child!.DrawerContext.Parent);
        Assert.Null(parent.Parent);
        Assert.Equal(2, parent.Dialog.NestedOpenDialogCount);
        Assert.Equal("1", NestedDrawers(cut.Find("[data-testid=parent-popup]")));
        Assert.Equal("0", NestedDrawers(cut.Find("[data-testid=child-popup]")));
    }

    // DLG-DIS-10's drawer sentence: drawer mode only adds the drawer count report; unlike alert mode it forces no modality, no
    // pointer dismissal and no role. The Dialog parent counts the drawer as one nested dialog and one nested drawer.
    [Fact(DisplayName = "port: a nested drawer raises only the parent's nested drawer count, not its forced values")]
    [Trait("Requirement", "DLG-DIS-10")]
    [Trait("Requirement", "DLG-STA-05")]
    [Trait("Requirement", "DRW-STR-01")]
    public void A_nested_drawer_raises_only_the_parents_nested_drawer_count()
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p
            .Add(x => x.Shape, "dialog-parent")
            .Add(x => x.ChildOpen, true)
            .Add(x => x.ChildModal, (ModalValue?)false));

        IElement parent = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("1", Variable(parent, "--nested-dialogs"));
        Assert.True(parent.HasAttribute("data-nested-dialog-open"));
        Assert.Equal("1", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-dialogs"));
        Assert.Equal("1", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-drawers"));

        IElement state = cut.Find("[data-testid=alert-dialog-state]");
        Assert.Equal("false", state.GetAttribute("data-modal"));
        Assert.Equal("false", state.GetAttribute("data-disable-pointer-dismissal"));
        Assert.Equal("dialog", state.GetAttribute("data-role"));
        Assert.Equal("dialog", cut.Find("[data-testid=child-popup]").GetAttribute("role"));

        // The probe has no parameters, so it does not render again; the store is read directly.
        cut.Render(p => p.Add(x => x.Shape, "dialog-parent").Add(x => x.ChildOpen, true).Add(x => x.ChildModal, (ModalValue?)ModalValue.TrapFocus));
        Assert.Equal(DialogModality.TrapFocus, cut.Instance.Child!.DrawerContext.Dialog.Modality);

        cut.Render(p => p.Add(x => x.Shape, "dialog-parent").Add(x => x.ChildOpen, false).Add(x => x.ChildModal, (ModalValue?)ModalValue.TrapFocus));
        Assert.Equal("0", cut.Find("[data-testid=parent-counts]").GetAttribute("data-nested-drawers"));
        Assert.False(cut.Find("[data-testid=parent-popup]").HasAttribute("data-nested-dialog-open"));
    }

    // Both titles: the parent counts one nested drawer before and after the nested drawer's own dialog opens, never two; the
    // nested drawer counts its dialog only as a dialog. DRW-DOM-22: neither viewport renders data-nested-dialog-open, though
    // the nested drawer's viewport has a nested dialog open; nor does either drawer popup, which has no --nested-dialogs
    // (DLG-DOM-19, DLG-DOM-22 not adopted). DRW-DOM-22's "even when a consumer passes it" is not asserted: the port forwards
    // a consumer's data-nested-dialog-open (reported to the track, src not changed here).
    private async Task DialogsInsideNestedDrawersAsync(string shape)
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p.Add(x => x.Shape, shape));
        Assert.Equal("1", NestedDrawers(cut.Find("[data-testid=parent-popup]")));
        Assert.Equal("0", NestedDrawers(cut.Find("[data-testid=child-popup]")));
        Assert.False(cut.Find("[data-testid=child-popup]").HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));

        await test.PressAsync(cut, "open-dialog", open: true);

        Assert.Single(cut.FindAll("[data-testid=dialog-popup]"));
        IElement parent = cut.Find("[data-testid=parent-popup]");
        IElement child = cut.Find("[data-testid=child-popup]");
        Assert.Equal("1", NestedDrawers(parent));
        Assert.Equal("0", NestedDrawers(child));
        Assert.False(child.HasAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));
        Assert.Contains(1, cut.Instance.ParentDrawerCounts);
        Assert.DoesNotContain(2, cut.Instance.ParentDrawerCounts);

        IDialogRootContext childStore = cut.Instance.Child!.DrawerContext.Dialog;
        Assert.Equal(1, childStore.NestedOpenDialogCount);
        Assert.Equal(0, childStore.NestedOpenDrawerCount);
        Assert.Equal(2, cut.Instance.Parent!.DrawerContext.Dialog.NestedOpenDialogCount);

        foreach (IElement element in new[] { parent, child, cut.Find("[data-testid=parent-viewport]"), cut.Find("[data-testid=child-viewport]") })
        {
            Assert.False(element.HasAttribute("data-nested-dialog-open"));
        }

        Assert.DoesNotContain("--nested-dialogs", parent.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("--nested-dialogs", child.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
    }

    private static string? NestedDrawers(IElement popup) => Variable(popup, DrawerPopupCssVars.NestedDrawers);

    private static string? Variable(IElement element, string name) => DrawerPopupTests.Variable(element, name);
}
