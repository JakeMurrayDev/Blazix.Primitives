using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>AlertDialogRoot.test.tsx</c> (titles verbatim; its suites and its browser-only "modality"
/// title run at L4), and the port titles of the regime alert mode forces (ADL-API-02/03, ADL-STA-01, ADL-DOM-01, ADL-DIS-01,
/// ADL-FOC-02), read from what the parts send the modules. A press outside the popup is the <c>interactions</c> module's
/// decision, so the titles that press a backdrop check here what the session was told and press it at L4. The modules are
/// bUnit's mocks; a trigger press is reported through its twin as the module does on Server, and a close's exit ends when
/// the test completes the popup's transition.
/// </summary>
public sealed class AlertDialogRootTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "ARIA attributes")]
    [Trait("Requirement", "ADL-ARIA-01")]
    [Trait("Requirement", "DLG-ARIA-05")]
    [Trait("Requirement", "DLG-ARIA-06")]
    [Trait("Requirement", "DLG-DOM-23")]
    public void ARIA_attributes()
    {
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p
            .Add(x => x.ControlledOpen, true)
            .Add(x => x.WithBackdrop, true)
            .Add(x => x.WithTitle, true)
            .Add(x => x.WithDescription, true));

        IElement popup = cut.Find("[role=alertdialog]");
        Assert.False(popup.HasAttribute("aria-modal"));
        Assert.Equal(cut.Find("h2").Id, popup.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find("p").Id, popup.GetAttribute("aria-describedby"));
        Assert.Equal("dialog", cut.Find("[data-testid=trigger]").GetAttribute("aria-haspopup"));
    }

    [Fact(DisplayName = "synchronizes trigger ARIA attributes when initially open with a handle")]
    [Trait("Requirement", "ADL-ARIA-04")]
    [Trait("Requirement", "DLG-API-11")]
    [Trait("Requirement", "DLG-API-27")]
    public void Synchronizes_trigger_ARIA_attributes_when_initially_open_with_a_handle()
    {
        AlertDialogHandle<NoPayload> handle = new();
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p
            .Add(x => x.Handle, handle)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.DefaultTriggerId, "trigger")
            .Add(x => x.TriggerId, "trigger"));

        IElement trigger = cut.Find("#trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal(cut.Find("[role=alertdialog]").Id, trigger.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "synchronizes detached trigger ARIA attributes when initially open with a handle")]
    [Trait("Requirement", "ADL-ARIA-04")]
    [Trait("Requirement", "DLG-API-11")]
    [Trait("Requirement", "DLG-API-27")]
    public void Synchronizes_detached_trigger_ARIA_attributes_when_initially_open_with_a_handle()
    {
        AlertDialogHandle<NoPayload> handle = new();
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p
            .Add(x => x.TriggerMode, "detached")
            .Add(x => x.Handle, handle)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.DefaultTriggerId, "trigger")
            .Add(x => x.TriggerId, "trigger"));

        IElement trigger = cut.Find("#trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal(cut.Find("[role=alertdialog]").Id, trigger.GetAttribute("aria-controls"));
        Assert.True(handle.IsOpen);
    }

    // The bystander's renders are counted through its ClassValue (Base UI counts its render function's calls); the close is a
    // click on the Close part, whose exit ends when the popup's transition completes.
    [Fact(DisplayName = "does not re-render inactive detached triggers when the popup opens and closes")]
    [Trait("Requirement", "DLG-EDGE-16")]
    [Trait("Requirement", "DLG-ANI-09")]
    [Trait("Requirement", "DLG-PTR-19")]
    public async Task Does_not_re_render_inactive_detached_triggers_when_the_popup_opens_and_closes()
    {
        int bystander = 0;
        IRenderedComponent<AlertDialogInactiveTriggersHarness> cut = test.Context.Render<AlertDialogInactiveTriggersHarness>(p => p
            .Add(x => x.BystanderClassValue, _ =>
            {
                bystander++;
                return null;
            }));

        bystander = 0;
        await OpenAndCloseAsync();
        await OpenAndCloseAsync();
        Assert.Equal(0, bystander);

        async Task OpenAndCloseAsync()
        {
            await test.PressAsync(cut, "trigger-1", open: true);
            Assert.Single(cut.FindAll("[data-testid=popup]"));
            Assert.Equal("true", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
            await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });
            await test.CompletePopupTransitionAsync(cut, open: false);
            Assert.Empty(cut.FindAll("[data-testid=popup]"));
            Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
        }
    }

    [Fact(DisplayName = "renders a viewport")]
    [Trait("Requirement", "DLG-STR-06")]
    [Trait("Requirement", "DLG-DOM-11")]
    public void Renders_a_viewport()
    {
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p
            .Add(x => x.ControlledOpen, true)
            .Add(x => x.WithTrigger, false)
            .Add(x => x.WithViewport, true));

        Assert.NotNull(cut.Find("[data-testid=viewport] [role=alertdialog]"));
    }

    [Fact(DisplayName = "keeps the trigger data-popup-open attribute and handle.isOpen when a controlled close is vetoed")]
    [Trait("Requirement", "ADL-DIS-02")]
    [Trait("Requirement", "DLG-STA-14")]
    [Trait("Requirement", "DLG-DOM-03")]
    [Trait("Requirement", "DLG-API-27")]
    public async Task Keeps_the_trigger_data_popup_open_attribute_and_handle_isOpen_when_a_controlled_close_is_vetoed()
    {
        AlertDialogHandle<NoPayload> handle = new();
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p
            .Add(x => x.Handle, handle)
            .Add(x => x.ControlledOpen, false)
            .Add(x => x.AcceptClose, false)
            .Add(x => x.TriggerId, "trigger")
            .Add(x => x.WithTitle, true)
            .Add(x => x.WithClose, true));

        await test.PressAsync(cut, "trigger", open: true);
        Assert.Single(cut.FindAll("[role=alertdialog]"));
        Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open"));
        Assert.True(handle.IsOpen);

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });

        Assert.True(cut.Find("[role=alertdialog]").HasAttribute("data-open"));
        Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open"));
        Assert.True(handle.IsOpen);
        Assert.Equal(["true:trigger-press", "false:close-press"], cut.Instance.OpenChanges);
    }

    // The press on the internal backdrop that leaves the dialog open needs a real browser: the same title at L4. Here the
    // remounted root's session is told that no outside press dismisses it.
    [Fact(DisplayName = "rewires the trigger and keeps pointer dismissal disabled after the root remounts while open")]
    [Trait("Requirement", "ADL-STA-01")]
    [Trait("Requirement", "ADL-DIS-01")]
    [Trait("Requirement", "DLG-EDGE-12")]
    [Trait("Requirement", "DLG-ARIA-03")]
    public async Task Rewires_the_trigger_and_keeps_pointer_dismissal_disabled_after_the_root_remounts_while_open()
    {
        IRenderedComponent<AlertDialogRemountHarness> cut = test.Context.Render<AlertDialogRemountHarness>();

        await test.PressAsync(cut, "trigger", open: true);
        IElement popup = cut.Find("[role=alertdialog]");
        Assert.Equal(popup.Id, cut.Find("#trigger").GetAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Mounted, false));
        Assert.Empty(cut.FindAll("[role=alertdialog]"));

        cut.Render(p => p.Add(x => x.Mounted, true));
        Assert.Empty(cut.FindAll("[role=alertdialog]"));

        await test.PressAsync(cut, "trigger", open: true);
        popup = cut.Find("[role=alertdialog]");
        Assert.Equal(popup.Id, cut.Find("#trigger").GetAttribute("aria-controls"));
        Assert.False(LastDismiss(popup.Id!).GetProperty("outsidePress").GetBoolean());
        Assert.True(cut.Instance.Handle.IsOpen);
    }

    // AlertDialogRoot.test.tsx's AlertDialogState reads useDialogRootContext; the probe reads the internal context. The press on
    // the internal backdrop is the same title at L4.
    [Fact(DisplayName = "enforces alert dialog state for handle-backed roots")]
    [Trait("Requirement", "ADL-STA-01")]
    [Trait("Requirement", "ADL-API-02")]
    [Trait("Requirement", "ADL-API-03")]
    [Trait("Requirement", "ADL-ARIA-01")]
    [Trait("Requirement", "ADL-DIS-01")]
    public async Task Enforces_alert_dialog_state_for_handle_backed_roots()
    {
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p
            .Add(x => x.TriggerMode, "detached")
            .Add(x => x.TriggerId, "trigger")
            .Add(x => x.Probe, (RenderFragment)(builder =>
            {
                builder.OpenComponent<AlertDialogStateProbe>(0);
                builder.CloseComponent();
            })));

        IElement state = cut.Find("[data-testid=alert-dialog-state]");
        Assert.Equal("true", state.GetAttribute("data-modal"));
        Assert.Equal("true", state.GetAttribute("data-disable-pointer-dismissal"));
        Assert.Equal("alertdialog", state.GetAttribute("data-role"));

        await test.PressAsync(cut, "trigger", open: true);
        IElement popup = cut.Find("[role=alertdialog]");
        Assert.True(cut.Instance.EffectiveHandle!.IsOpen);
        Assert.False(LastDismiss(popup.Id!).GetProperty("outsidePress").GetBoolean());
    }

    [Fact(DisplayName = "port: an alert dialog's session never dismisses on an outside press, and its focus manager is modal and never closes on focus-out")]
    [Trait("Requirement", "ADL-API-02")]
    [Trait("Requirement", "ADL-API-03")]
    [Trait("Requirement", "ADL-DIS-01")]
    [Trait("Requirement", "ADL-FOC-02")]
    [Trait("Requirement", "ADL-EVT-02")]
    public async Task An_alert_dialogs_session_never_dismisses_on_an_outside_press()
    {
        IRenderedComponent<AlertDialogHarness> cut = test.Context.Render<AlertDialogHarness>(p => p.Add(x => x.WithBackdrop, true).Add(x => x.TriggerId, "trigger"));

        await test.PressAsync(cut, "trigger", open: true);
        IElement popup = cut.Find("[role=alertdialog]");

        JsonElement dismiss = LastDismiss(popup.Id!);
        Assert.False(dismiss.GetProperty("outsidePress").GetBoolean());
        // Escape stays on while the alert dialog is topmost (ADL-KEY-01).
        Assert.False(dismiss.TryGetProperty("escapeKey", out _));

        using JsonDocument focus = JsonDocument.Parse(DialogTestContext.AttachOptions(test.FocusManagerModule)[^1]!);
        Assert.True(focus.RootElement.GetProperty("modal").GetBoolean());
        Assert.False(focus.RootElement.GetProperty("closeOnFocusOut").GetBoolean());
        Assert.Equal(["true:trigger-press"], cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "port: an alert dialog renders its internal backdrop and locks page scroll whenever it is open, with or without a backdrop part")]
    [Trait("Requirement", "ADL-DOM-01")]
    [Trait("Requirement", "ADL-API-02")]
    [Trait("Requirement", "DLG-STR-10")]
    public void An_alert_dialog_renders_its_internal_backdrop_and_locks_page_scroll_whenever_it_is_open()
    {
        foreach (bool withBackdrop in new[] { false, true })
        {
            using DialogTestContext context = new();
            IRenderedComponent<AlertDialogHarness> cut = context.Context.Render<AlertDialogHarness>(p => p
                .Add(x => x.DefaultOpen, true)
                .Add(x => x.WithBackdrop, withBackdrop));

            // The internal backdrop is the portal node's first child (DLG-DOM-06), before a backdrop part.
            IElement internalBackdrop = cut.Find("[data-testid=portal]").FirstElementChild!;
            Assert.Equal("presentation", internalBackdrop.GetAttribute("role"));
            Assert.True(internalBackdrop.HasAttribute("data-base-ui-inert"));
            Assert.Equal(withBackdrop, cut.FindAll("[data-testid=backdrop]").Count == 1);

            using JsonDocument scrollLock = JsonDocument.Parse(Assert.Single(DialogTestContext.AttachOptions(context.ScrollLockModule))!);
            Assert.True(scrollLock.RootElement.GetProperty("enabled").GetBoolean());
        }
    }

    // The session options the dialog whose popup is popupId sent last, through its attach or an update.
    private JsonElement LastDismiss(string popupId)
    {
        JsonElement? dismiss = null;
        IEnumerable<string?> sent = [.. DialogTestContext.AttachOptions(test.InteractionsModule), .. DialogTestContext.UpdateOptions(test.InteractionsHandle)];
        foreach (string? options in sent)
        {
            using JsonDocument document = JsonDocument.Parse(options!);
            if (document.RootElement.TryGetProperty("session", out JsonElement session)
                && session.ValueKind == JsonValueKind.Object
                && session.TryGetProperty("floatingId", out JsonElement floatingId)
                && string.Equals(floatingId.GetString(), popupId, StringComparison.Ordinal)
                && session.TryGetProperty("dismiss", out JsonElement value))
            {
                dismiss = value.Clone();
            }
        }

        return dismiss ?? throw new InvalidOperationException($"No session options for the popup {popupId}.");
    }
}
