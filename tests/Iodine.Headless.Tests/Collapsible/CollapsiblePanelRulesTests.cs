using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Collapsible;

/// <summary>
/// COL-STA-08's <c>[L1]</c> row: <c>CollapsiblePanel.test.tsx</c> "does not unmount the panel when true", on the panel's
/// hidden condition and render predicate as pure C# (COL-STA-08, COL-DOM-16) and the mounted flag of the shared state as
/// the opening render and the end of the exit change it (COL-STA-04, TRN-ALG-01 through TransitionStatusRules).
/// </summary>
public sealed class CollapsiblePanelRulesTests
{
    [Fact(DisplayName = "does not unmount the panel when true")]
    [Trait("Requirement", "COL-STA-08")]
    [Trait("Requirement", "COL-DOM-16")]
    [Trait("Requirement", "COL-STA-04")]
    public void Does_not_unmount_the_panel_when_true()
    {
        CollapsibleRootContext root = new("base-ui-1", logger: null);
        root.OpenState.Update(valueSupplied: false, value: false, defaultValueSupplied: false, defaultValue: false);
        root.ApplyParameters(disabled: false, firstSet: true);

        // Closed: hidden, and only a kept panel renders its element.
        Assert.True(Hidden(root));
        Assert.True(CollapsiblePanelBehavior.RendersElement(keepMounted: true, hiddenUntilFound: false, root.State.Open, root.State.Mounted));
        Assert.False(CollapsiblePanelBehavior.RendersElement(keepMounted: false, hiddenUntilFound: false, root.State.Open, root.State.Mounted));
        Assert.True(CollapsiblePanelBehavior.RendersElement(keepMounted: false, hiddenUntilFound: true, root.State.Open, root.State.Mounted));

        // Opening mounts it in the same render, with the starting status (COL-STA-05).
        root.ApplyOpen(true);
        Assert.Equal((true, true, TransitionStatus.Starting), (root.State.Open, root.State.Mounted, root.State.TransitionStatus));
        Assert.False(Hidden(root));

        // Closing keeps it mounted (and not hidden) until its exit has ended.
        root.MirrorStatus(TransitionStatus.Ending);
        root.ApplyOpen(false);
        Assert.Equal((false, true), (root.State.Open, root.State.Mounted));
        Assert.False(Hidden(root));
        Assert.True(CollapsiblePanelBehavior.RendersElement(keepMounted: false, hiddenUntilFound: false, root.State.Open, root.State.Mounted));

        root.CompleteExit();
        Assert.Equal((false, null, 1), (root.State.Mounted, root.State.TransitionStatus, root.State.UnmountRequests));
        Assert.True(Hidden(root));
        Assert.True(CollapsiblePanelBehavior.RendersElement(keepMounted: true, hiddenUntilFound: false, root.State.Open, root.State.Mounted));
        Assert.False(CollapsiblePanelBehavior.RendersElement(keepMounted: false, hiddenUntilFound: false, root.State.Open, root.State.Mounted));
    }

    [Fact(DisplayName = "an exit that ends after a reopen leaves the panel mounted")]
    [Trait("Requirement", "COL-ANI-16")]
    public void An_exit_that_ends_after_a_reopen_leaves_the_panel_mounted()
    {
        CollapsibleRootContext root = new("base-ui-1", logger: null);
        root.OpenState.Update(valueSupplied: false, value: false, defaultValueSupplied: true, defaultValue: true);
        root.ApplyParameters(disabled: false, firstSet: true);

        root.ApplyOpen(false);
        root.ApplyOpen(true);
        root.CompleteExit();

        Assert.True(root.State.Mounted);
        Assert.Equal(0, root.State.UnmountRequests);
    }

    [Fact(DisplayName = "the panel id is the registered one, the default one, or none after the panel unregistered")]
    [Trait("Requirement", "COL-STA-09")]
    [Trait("Requirement", "COL-EDGE-09")]
    public void The_panel_id_is_the_registered_one_the_default_one_or_none_after_the_panel_unregistered()
    {
        CollapsibleRootContext root = new("base-ui-1", logger: null);
        Assert.Equal("base-ui-1", root.State.PanelId);

        root.RegisterPanel("custom");
        Assert.Equal("custom", root.State.PanelId);
        // An unregistration of an id that is not the registered one changes nothing (the StrictMode case).
        root.UnregisterPanel("other");
        Assert.Equal("custom", root.State.PanelId);
        root.UnregisterPanel("custom");
        Assert.Null(root.State.PanelId);

        root.RegisterPanel(null);
        Assert.Equal("base-ui-1", root.State.PanelId);
        root.UnregisterPanel(null);
        Assert.Null(root.State.PanelId);
        root.RegisterPanel(null);
        Assert.Equal("base-ui-1", root.State.PanelId);
    }

    private static bool Hidden(Store<CollapsibleRootStoreState> root) => CollapsiblePanelBehavior.IsHidden(root.State.Open, root.State.Mounted);
}
