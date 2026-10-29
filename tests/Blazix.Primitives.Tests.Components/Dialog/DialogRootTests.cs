using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogRoot.test.tsx</c> that bUnit observes: the labels (DLG-DOM-23, STA-09, ARIA-05..07),
/// a close's trigger (DLG-API-11, DIS-09, EVT-03), the internal openchange as the session's relay (DLG-STA-15, EVT-02,
/// EVT-04, EVT-06), the internal backdrop's structure (DLG-STR-10/11), trigger ownership (DLG-API-14, STA-12, ARIA-02/03,
/// EVT-07, EDGE-15) and the nested report (DLG-STA-05). Base UI runs the trigger-mode titles under <c>describe.for</c> with
/// its three TestDialog components; here each is a theory over the same three shapes. A press, Escape or an outside press is
/// the <c>interactions</c> module's report or decision, driven into the twins as the module sends it.
/// </summary>
public sealed class DialogRootTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    /// <summary>Gets Base UI's three trigger modes.</summary>
    public static TheoryData<string> TriggerModes => new() { "contained", "detached", "multiple" };

    [Theory(DisplayName = "ARIA attributes")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-DOM-23")]
    [Trait("Requirement", "DLG-ARIA-05")]
    [Trait("Requirement", "DLG-ARIA-06")]
    [Trait("Requirement", "DLG-ARIA-07")]
    public void ARIA_attributes(string mode)
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.TriggerMode, mode)
            .Add(x => x.ControlledOpen, true)
            .Add(x => x.Modal, false)
            .Add(x => x.WithTitle, true)
            .Add(x => x.WithDescription, true)
            .Add(x => x.WithBackdrop, true));

        IElement popup = cut.Find("[role=dialog]");
        Assert.Equal(cut.Find("h2").Id, popup.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find("p").Id, popup.GetAttribute("aria-describedby"));
    }

    [Theory(DisplayName = "keeps accessible names and descriptions in sync when label parts change")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-STA-09")]
    [Trait("Requirement", "DLG-ARIA-05")]
    [Trait("Requirement", "DLG-ARIA-06")]
    public async Task Keeps_accessible_names_and_descriptions_in_sync_when_label_parts_change(string mode)
    {
        RenderFragment labels = builder =>
        {
            builder.OpenComponent<DynamicLabels>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.TriggerMode, mode)
            .Add(x => x.ControlledOpen, true)
            .Add(x => x.Modal, false)
            .Add(x => x.PopupExtra, labels));

        string? firstTitleId = cut.Find("h2").Id;
        string? firstDescriptionId = cut.Find("p").Id;
        Assert.Equal(firstTitleId, cut.Find("[role=dialog]").GetAttribute("aria-labelledby"));
        Assert.Equal(firstDescriptionId, cut.Find("[role=dialog]").GetAttribute("aria-describedby"));

        await cut.Find("[data-testid=change-labels]").ClickAsync(new());
        Assert.Contains("Title 2", cut.Find("h2").TextContent, StringComparison.Ordinal);
        string? secondTitleId = cut.Find("h2").Id;
        string? secondDescriptionId = cut.Find("p").Id;
        Assert.Equal(secondTitleId, cut.Find("[role=dialog]").GetAttribute("aria-labelledby"));
        Assert.Equal(secondDescriptionId, cut.Find("[role=dialog]").GetAttribute("aria-describedby"));
        Assert.NotEqual(firstTitleId, secondTitleId);
        Assert.NotEqual(firstDescriptionId, secondDescriptionId);

        await cut.Find("[data-testid=change-labels]").ClickAsync(new());
        Assert.Empty(cut.FindAll("h2"));
        Assert.False(cut.Find("[role=dialog]").HasAttribute("aria-labelledby"));
        Assert.False(cut.Find("[role=dialog]").HasAttribute("aria-describedby"));
    }

    [Theory(DisplayName = "reports no trigger when closing with an initial trigger id that is not mounted")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-API-11")]
    [Trait("Requirement", "DLG-DIS-09")]
    [Trait("Requirement", "DLG-EVT-03")]
    public async Task Reports_no_trigger_when_closing_with_an_initial_trigger_id_that_is_not_mounted(string mode)
    {
        // Base UI's body renders the same Root for every mode, with no trigger.
        _ = mode;
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.WithTrigger, false)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.DefaultTriggerId, "missing-trigger")
            .Add(x => x.Modal, false));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());

        Assert.Equal(["false:imperative-action"], cut.Instance.OpenChanges);
        Assert.Equal([null], cut.Instance.TriggerIds);
    }

    [Theory(DisplayName = "emits a single internal openchange when closing on Escape")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-STA-15")]
    [Trait("Requirement", "DLG-EVT-02")]
    [Trait("Requirement", "DLG-EVT-06")]
    public async Task Emits_a_single_internal_openchange_when_closing_on_Escape(string mode)
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.TriggerMode, mode)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.WithClose, true));
        int before = DialogTestContext.UpdateOptions(test.InteractionsHandle).Count;

        string answer = await test.EscapeAsync(cut);

        Assert.Equal("prevent-and-stop", answer);
        List<JsonElement> relays = NewRelays(before);
        JsonElement relay = Assert.Single(relays);
        Assert.Equal("escape-key", relay.GetProperty("reason").GetString());
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-closed"));
    }

    [Theory(DisplayName = "does not emit internal openchange when an Escape close is canceled")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-STA-15")]
    [Trait("Requirement", "DLG-EVT-04")]
    public async Task Does_not_emit_internal_openchange_when_an_Escape_close_is_canceled(string mode)
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.TriggerMode, mode)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.WithClose, true)
            .Add(x => x.OnOpenChange, args =>
            {
                if (!args.Value)
                {
                    args.Details.Cancel();
                }
            }));
        int before = DialogTestContext.UpdateOptions(test.InteractionsHandle).Count;

        string answer = await test.EscapeAsync(cut);

        Assert.Equal("stop", answer);
        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Equal(["false:escape-key"], cut.Instance.OpenChanges);
        Assert.Empty(NewRelays(before));
    }

    [Theory(DisplayName = "should render an internal backdrop when `true`")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-STR-10")]
    [Trait("Requirement", "DLG-STR-11")]
    public void Should_render_an_internal_backdrop_when_true(string mode)
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.TriggerMode, mode)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Modal, true));

        IElement popup = cut.Find("[role=dialog]");
        IElement guard = popup.PreviousElementSibling!;
        Assert.True(guard.HasAttribute("data-base-ui-focus-guard"));
        Assert.Equal("presentation", guard.PreviousElementSibling!.GetAttribute("role"));
    }

    [Theory(DisplayName = "should not render an internal backdrop when `false`")]
    [MemberData(nameof(TriggerModes))]
    [Trait("Requirement", "DLG-STR-10")]
    public void Should_not_render_an_internal_backdrop_when_false(string mode)
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.TriggerMode, mode)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Modal, false));

        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Empty(cut.FindAll("[role=presentation]"));
    }

    [Fact(DisplayName = "keeps trigger ownership when another trigger mounts while open")]
    [Trait("Requirement", "DLG-API-14")]
    [Trait("Requirement", "DLG-STA-12")]
    [Trait("Requirement", "DLG-ARIA-02")]
    [Trait("Requirement", "DLG-ARIA-03")]
    [Trait("Requirement", "DLG-EVT-07")]
    [Trait("Requirement", "DLG-EDGE-15")]
    public async Task Keeps_trigger_ownership_when_another_trigger_mounts_while_open()
    {
        IRenderedComponent<DialogOwnershipHarness> cut = test.Context.Render<DialogOwnershipHarness>();
        IElement trigger1 = cut.Find("#trigger-1");

        await test.PressAsync(cut, "trigger-1", open: true);
        string? controls = cut.Find("#trigger-1").GetAttribute("aria-controls");
        Assert.Equal(cut.Find("[role=dialog]").Id, controls);

        await cut.Find("[data-testid=mount-trigger-2]").ClickAsync(new());

        Assert.Equal("true", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
        Assert.Equal(controls, cut.Find("#trigger-1").GetAttribute("aria-controls"));
        Assert.Equal("false", cut.Find("#trigger-2").GetAttribute("aria-expanded"));
        Assert.False(cut.Find("#trigger-2").HasAttribute("aria-controls"));
        Assert.NotNull(trigger1);
    }

    // DLG-STA-01 and STA-05 restated over the C# store (design point 7 of M4 track 8): a nested root's report lands in the
    // markup of the batch that opened it, not after it; Drawer's count is M6's.
    // DLG-EVT-05 as written (DLG §16 Q1 resolved as observed): the call is recorded at once, so a callback that prevents the
    // unmount and then cancels its own close still keeps the next close that is not canceled mounted, until Unmount().
    [Fact(DisplayName = "port: preventUnmountOnClose called in a canceled close keeps the next close mounted until unmount")]
    [Trait("Requirement", "DLG-EVT-05")]
    public async Task PreventUnmountOnClose_in_a_canceled_close_keeps_the_next_close_mounted_until_unmount()
    {
        int closes = 0;
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Modal, false)
            .Add(x => x.OnOpenChange, args =>
            {
                if (!args.Value && closes++ == 0)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                    args.Details.Cancel();
                }
            }));

        await test.RequestAsync(cut, open: false, "escape-key", DialogTestContext.EscapeJson);
        Assert.NotNull(cut.Find("[role=dialog]").GetAttribute("data-open"));
        await test.RequestAsync(cut, open: false, "escape-key", DialogTestContext.EscapeJson);
        await test.CompletePopupTransitionAsync(cut, open: false);

        IElement popup = cut.Find("[role=dialog]");
        Assert.NotNull(popup.GetAttribute("data-closed"));
        Assert.Null(popup.GetAttribute("hidden"));
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    // Issue 147 as chosen by the user 2026-10-29 ((B1) with issue 148, ADR 04 Amendments 2026-10-29): the call announces the
    // retained close to the popup's transitions module at once, and under Dialog's call-time opt-out a close the callback
    // then cancels keeps it, since the next close that is not canceled stays mounted (DLG-EVT-05).
    [Fact(DisplayName = "port: under Dialog's call-time opt-out a canceled close keeps the retained close's announcement")]
    [Trait("Requirement", "DLG-EVT-05")]
    public async Task Under_the_call_time_opt_out_a_canceled_close_keeps_the_announcement()
    {
        int closes = 0;
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Modal, false)
            .Add(x => x.OnOpenChange, args =>
            {
                if (!args.Value && closes++ == 0)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                    args.Details.Cancel();
                }
            }));
        cut.WaitForAssertion(() => Assert.Contains(test.Context.JSInterop.Invocations, invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"openSelector\"", StringComparison.Ordinal)));

        await test.RequestAsync(cut, open: false, "escape-key", DialogTestContext.EscapeJson);

        Assert.Equal([true], test.Context.JSInterop.Invocations.Where(invocation => invocation.Identifier == "retainExit").Select(invocation => (bool)invocation.Arguments[1]!));
    }

    [Fact(DisplayName = "port: a nested dialog's open count lands in the batch that opened it, and its unmount's in the batch that removed it")]
    [Trait("Requirement", "DLG-STA-05")]
    public void A_nested_dialogs_open_count_lands_in_the_batch_that_opened_it()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 2).Add(x => x.Level2Open, false));
        Assert.False(cut.Find("[data-testid=level-1-popup]").HasAttribute("data-nested-dialog-open"));

        cut.Render(p => p.Add(x => x.Level2Open, true));
        Assert.True(cut.Find("[data-testid=level-1-popup]").HasAttribute("data-nested-dialog-open"));

        cut.Render(p => p.Add(x => x.Level2Mounted, false));
        Assert.False(cut.Find("[data-testid=level-1-popup]").HasAttribute("data-nested-dialog-open"));
    }

    // The relays the session's updates carried since `before`, one per committed open change.
    private List<JsonElement> NewRelays(int before)
    {
        List<JsonElement> relays = [];
        int lastRevision = -1;
        IReadOnlyList<string?> updates = DialogTestContext.UpdateOptions(test.InteractionsHandle);
        for (int i = before; i < updates.Count; i++)
        {
            using JsonDocument document = JsonDocument.Parse(updates[i]!);
            if (document.RootElement.TryGetProperty("session", out _) && document.RootElement.TryGetProperty("relay", out JsonElement relay) && relay.ValueKind == JsonValueKind.Object)
            {
                int revision = relay.GetProperty("revision").GetInt32();
                if (revision != lastRevision && (relays.Count > 0 || revision > RevisionBefore(before)))
                {
                    lastRevision = revision;
                    relays.Add(relay.Clone());
                }
            }
        }

        return relays;
    }

    private int RevisionBefore(int before)
    {
        IReadOnlyList<string?> updates = DialogTestContext.UpdateOptions(test.InteractionsHandle);
        IEnumerable<string?> sent = [.. DialogTestContext.AttachOptions(test.InteractionsModule), .. updates.Take(before)];
        int revision = -1;
        foreach (string? options in sent)
        {
            using JsonDocument document = JsonDocument.Parse(options!);
            if (document.RootElement.TryGetProperty("session", out _) && document.RootElement.TryGetProperty("relay", out JsonElement relay) && relay.ValueKind == JsonValueKind.Object)
            {
                revision = Math.Max(revision, relay.GetProperty("revision").GetInt32());
            }
        }

        return revision;
    }
}
