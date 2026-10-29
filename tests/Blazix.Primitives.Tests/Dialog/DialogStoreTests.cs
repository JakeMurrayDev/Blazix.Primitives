using System.Reflection;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The Dialog store and handle at L1: DLG-STA-01 restated over the C# store (design point 7 of M4 track 8, settled by the
/// orchestrator 2026-10-28; Base UI constructs DialogStore directly), the values alert mode forces (ADL §3.5, applied in the
/// store sync), the nested report's gates (DLG-STA-04/06) and PSH-API-03 by reflection (OpenWithPayloadAsync on the
/// Dialog family only).
/// </summary>
public sealed class DialogStoreTests
{
    [Fact(DisplayName = "port: a new dialog store is closed, modal, dismissible by a pointer, the dialog role, top level, with no nested dialog and no labels")]
    [Trait("Requirement", "DLG-STA-01")]
    public void A_new_dialog_store_has_the_initial_state()
    {
        DialogStore<NoPayload> store = new(DialogMode.Dialog, "popup-1", "token-1", "backdrop-1");

        DialogStoreState<NoPayload> state = store.State;
        Assert.False(state.Popup.EffectiveOpen);
        Assert.False(state.Popup.Mounted);
        Assert.Null(state.Popup.OpenMethod);
        Assert.Equal("popup-1", state.Popup.FloatingId);
        Assert.Equal(DialogModality.Modal, state.Modality);
        Assert.False(state.DisablePointerDismissal);
        Assert.Equal("dialog", state.Role);
        Assert.False(state.Nested);
        Assert.Equal(0, state.NestedOpenDialogCount);
        Assert.Null(state.TitleElementId);
        Assert.Null(state.DescriptionElementId);
        Assert.Equal("Dialog", store.ComponentName);
    }

    [Fact(DisplayName = "port: the root's modality, pointer dismissal and nesting apply as given in dialog mode")]
    [Trait("Requirement", "DLG-STA-02")]
    public void The_roots_settings_apply_as_given_in_dialog_mode()
    {
        DialogStore<NoPayload> store = new(DialogMode.Dialog, "popup-1", "token-1", "backdrop-1");

        store.ApplyRootSettings(DialogModality.TrapFocus, disablePointerDismissal: true, nested: true);

        Assert.Equal(DialogModality.TrapFocus, store.State.Modality);
        Assert.True(store.State.DisablePointerDismissal);
        Assert.True(store.State.Nested);
        Assert.Equal("dialog", store.State.Role);
    }

    [Fact(DisplayName = "port: alert mode forces a modal dialog without pointer dismissal and with the alertdialog role, whatever the root passes")]
    [Trait("Requirement", "DLG-API-03")]
    [Trait("Requirement", "DLG-API-06")]
    public void Alert_mode_forces_its_values()
    {
        DialogStore<NoPayload> store = new(DialogMode.AlertDialog, "popup-1", "token-1", "backdrop-1");
        Assert.Equal(DialogModality.Modal, store.State.Modality);
        Assert.True(store.State.DisablePointerDismissal);
        Assert.Equal("alertdialog", store.State.Role);

        store.ApplyRootSettings(DialogModality.NonModal, disablePointerDismissal: false, nested: true);

        Assert.Equal(DialogModality.Modal, store.State.Modality);
        Assert.True(store.State.DisablePointerDismissal);
        Assert.Equal("alertdialog", store.State.Role);
        Assert.True(store.State.Nested);
        Assert.Equal("AlertDialog", store.ComponentName);
    }

    [Fact(DisplayName = "port: a nested report counts while the dialog is open or mounted, is dropped while it is neither, and resets once closed and unmounted")]
    [Trait("Requirement", "DLG-STA-04")]
    [Trait("Requirement", "DLG-STA-06")]
    public async Task A_nested_report_counts_only_while_open_or_mounted()
    {
        DialogStore<NoPayload> store = new(DialogMode.Dialog, "popup-1", "token-1", "backdrop-1");
        store.ReportNestedOpen(1, 0);
        Assert.Equal(0, store.State.NestedOpenDialogCount);

        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ImperativeAction));
        store.ReportNestedOpen(2, 0);
        Assert.Equal(2, store.State.NestedOpenDialogCount);

        await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ImperativeAction));
        store.ResetNestedIfClosed();
        Assert.Equal(2, store.State.NestedOpenDialogCount);

        await store.UnmountAsync();
        store.ResetNestedIfClosed();
        Assert.Equal(0, store.State.NestedOpenDialogCount);
    }

    [Fact(DisplayName = "port: OpenWithPayloadAsync is a member of the Dialog family's handle and not of the shared handle")]
    [Trait("Requirement", "PSH-API-03")]
    public void OpenWithPayloadAsync_is_the_Dialog_familys_only()
    {
        MethodInfo? dialog = typeof(DialogHandle<int>).GetMethod("OpenWithPayloadAsync", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(dialog);
        Assert.Equal(typeof(Task), dialog.ReturnType);
        Assert.Equal(typeof(int), Assert.Single(dialog.GetParameters()).ParameterType);
        Assert.Null(typeof(PopupHandle<int>).GetMethod("OpenWithPayloadAsync", BindingFlags.Public | BindingFlags.Instance));
    }
}
