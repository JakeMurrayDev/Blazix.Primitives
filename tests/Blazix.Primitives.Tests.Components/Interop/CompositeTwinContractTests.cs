using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>composite</c> module (ADR 06 §1, L2): bUnit's JS interop mock records every field C#
/// sends on <c>attach</c> and <c>update</c>, and the tests drive <see cref="CompositeTwin"/>'s <c>[JSInvokable]</c>
/// callbacks and its two decision points.
/// </summary>
public sealed class CompositeTwinContractTests
{
    private const string DefaultsJson =
        """{"orientation":"both","loopFocus":true,"enableHomeAndEndKeys":false,"stopEventPropagation":true,"highlightItemOnHover":false,"modifierKeys":[],"direction":"ltr","items":[],"selectOnNavigation":false,"reportFocusLeave":false,"reportHighlight":false,"reportHandledKeys":false}""";

    private const string KeydownJson = """
        {"kind":"KeyboardEvent","type":"keydown","key":"ArrowDown","code":"ArrowDown","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":true,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-3","tagName":"SPAN"},"defaultPrevented":false,"timeStamp":12.5}
        """;

    private const string FocusJson = """
        {"kind":"FocusEvent","type":"focusin","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-4","tagName":"SPAN"},"defaultPrevented":false,"timeStamp":20}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CMP-STA-10")]
    public async Task Attach_imports_the_composite_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/composite.js");
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using CompositeTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<CompositeOptions> attached = await twin.AttachAsync("base-ui-1", new CompositeOptions());
        await attached.UpdateAsync(new CompositeOptions
        {
            Orientation = "vertical",
            LoopFocus = false,
            EnableHomeAndEndKeys = true,
            StopEventPropagation = false,
            HighlightItemOnHover = true,
            ModifierKeys = ["Shift", "Alt"],
            DisabledIndices = [1, 3],
            Direction = "rtl",
            Items = [new CompositeItemRegistration("base-ui-2", null), new CompositeItemRegistration("base-ui-3", "5")],
            SelectOnNavigation = true,
            ReportFocusLeave = true,
            ReportHighlight = true,
            ReportHandledKeys = true,
        });
        await attached.DisposeAsync();
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(DefaultsJson, attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<CompositeTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"orientation":"vertical","loopFocus":false,"enableHomeAndEndKeys":true,"stopEventPropagation":false,"highlightItemOnHover":true,"modifierKeys":["Shift","Alt"],"disabledIndices":[1,3],"direction":"rtl","items":[{"id":"base-ui-2"},{"id":"base-ui-3","tabIndex":"5"}],"selectOnNavigation":true,"reportFocusLeave":true,"reportHighlight":true,"reportHandledKeys":true}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    // M3 track 1, E3 (ADR 04 Amendments 2026-10-26, chosen by the user): sent only when set, so a group that uses none
    // of them sends what it sent before.
    [Fact]
    [Trait("Requirement", "CMP-STA-08")]
    [Trait("Requirement", "CMP-KEY-08")]
    public void The_items_flags_and_deriveDisabledIndices_are_sent_only_when_set()
    {
        CompositeOptions options = new()
        {
            DeriveDisabledIndices = true,
            Items =
            [
                new CompositeItemRegistration("a", null, Disabled: true, FocusableWhenDisabled: true),
                new CompositeItemRegistration("b", "0", Disabled: true),
                new CompositeItemRegistration("c", null),
            ],
        };

        string json = JsonSerializer.Serialize(options, BlazixJsonContext.Default.CompositeOptions);

        Assert.Contains("\"deriveDisabledIndices\":true", json, StringComparison.Ordinal);
        Assert.Contains(
            """
            "items":[{"id":"a","disabled":true,"focusableWhenDisabled":true},{"id":"b","tabIndex":"0","disabled":true},{"id":"c"}]
            """,
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("deriveDisabledIndices", JsonSerializer.Serialize(new CompositeOptions(), BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
    }

    // M3 track 1, E2 (ADR 04 Amendments 2026-10-26, chosen by the user): the items report, asked for only when set.
    [Fact]
    [Trait("Requirement", "CMP-EVT-03")]
    public void ReportItems_is_sent_only_when_set_and_OnItemsChange_raises_ItemsChanged_with_the_ids_in_order()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<IReadOnlyList<string>> changes = [];
        twin.ItemsChanged += changes.Add;

        twin.OnItemsChange("""{"ids":["base-ui-3","base-ui-1"]}""");
        twin.OnItemsChange("""{"ids":[]}""");

        Assert.Equal(2, changes.Count);
        Assert.Equal(["base-ui-3", "base-ui-1"], changes[0]);
        Assert.Empty(changes[1]);
        Assert.Contains("\"reportItems\":true", JsonSerializer.Serialize(new CompositeOptions { ReportItems = true }, BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
        Assert.DoesNotContain("reportItems", JsonSerializer.Serialize(new CompositeOptions(), BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
    }

    // M3 track 1, E1 (ADR 04 Amendments 2026-10-26, chosen by the user): the registration-only mode, sent only when set.
    [Fact]
    [Trait("Requirement", "CMP-STR-03")]
    public void RegistrationOnly_is_sent_only_when_set()
    {
        string json = JsonSerializer.Serialize(
            new CompositeOptions { RegistrationOnly = true, Items = [new CompositeItemRegistration("base-ui-1", null)] },
            BlazixJsonContext.Default.CompositeOptions);

        Assert.Contains("\"registrationOnly\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"items\":[{\"id\":\"base-ui-1\"}]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("registrationOnly", JsonSerializer.Serialize(new CompositeOptions(), BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
    }

    // M3 track 1, E5 (ADR 04 Amendments 2026-10-26, chosen by the user): the active-item follow, sent only when set.
    [Fact]
    [Trait("Requirement", "CMP-STA-22")]
    [Trait("Requirement", "CMP-EDGE-09")]
    public void FollowActiveItem_is_sent_only_when_set()
    {
        Assert.Contains("\"followActiveItem\":true", JsonSerializer.Serialize(new CompositeOptions { FollowActiveItem = true }, BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
        Assert.DoesNotContain("followActiveItem", JsonSerializer.Serialize(new CompositeOptions(), BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CMP-API-01")]
    [Trait("Requirement", "CMP-API-02")]
    [Trait("Requirement", "CMP-KEY-15")]
    public void The_options_default_to_the_mechanism_defaults_with_DOM_disabled_detection_and_no_style()
    {
        CompositeOptions options = new();

        Assert.Equal(DefaultsJson, JsonSerializer.Serialize(options, BlazixJsonContext.Default.CompositeOptions));
        Assert.Null(options.DisabledIndices);
        Assert.Null(options.Style);
    }

    [Fact]
    [Trait("Requirement", "CMP-EVT-01")]
    public void OnHighlightChange_raises_HighlightChanged_with_the_index()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<int> changes = [];
        twin.HighlightChanged += changes.Add;

        twin.OnHighlightChange("""{"index":2}""");
        twin.OnHighlightChange("""{"index":0}""");

        Assert.Equal(new[] { 2, 0 }, changes);
    }

    [Fact]
    [Trait("Requirement", "RDG-FOC-04")]
    [Trait("Requirement", "RDG-FOC-05")]
    public void OnFocusLeave_raises_FocusLeft_with_the_event()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<EventSnapshot> left = [];
        twin.FocusLeft += left.Add;

        twin.OnFocusLeave($$"""{"event":{{FocusJson.Replace("focusin", "focusout")}}}""");

        EventSnapshot snapshot = Assert.Single(left);
        Assert.Equal(EventKind.FocusEvent, snapshot.Kind);
        Assert.Equal("focusout", snapshot.Type);
        Assert.Equal("base-ui-4", snapshot.Target?.Id);
    }

    // Option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23.
    [Fact]
    [Trait("Requirement", "CMP-EVT-12")]
    public void OnKeyHandled_raises_KeyHandled_with_the_key_event()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<EventSnapshot> handled = [];
        twin.KeyHandled += handled.Add;

        twin.OnKeyHandled($$"""{"event":{{KeydownJson}}}""");

        EventSnapshot snapshot = Assert.Single(handled);
        Assert.Equal("keydown", snapshot.Type);
        Assert.Equal("ArrowDown", snapshot.Key);
        Assert.Equal("base-ui-3", snapshot.Target?.Id);
    }

    [Fact]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "EVD-EVT-26")]
    public void Decide_on_navigate_runs_the_navigation_decider_which_answers_stop_after_PreventPartHandler()
    {
        using BunitContext context = new();
        List<EventSnapshot> seen = [];
        CompositeTwin twin = new(context.JSInterop.JSRuntime)
        {
            NavigationDecider = snapshot =>
            {
                seen.Add(snapshot);
                KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();
                args.PreventPartHandler();
                return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
            },
        };

        Assert.Equal("navigate", CompositeTwin.NavigatePoint);
        Assert.Equal("stop", twin.Decide(CompositeTwin.NavigatePoint, KeydownJson));
        EventSnapshot snapshot = Assert.Single(seen);
        Assert.Equal("ArrowDown", snapshot.Key);
        Assert.True(snapshot.ShiftKey);
        Assert.Equal("base-ui-3", snapshot.Target?.Id);
    }

    [Fact]
    [Trait("Requirement", "RAD-FOC-05")]
    public void Decide_on_select_runs_the_selection_decider_which_answers_stop_after_PreventPartHandler()
    {
        using BunitContext context = new();
        List<EventSnapshot> seen = [];
        CompositeTwin twin = new(context.JSInterop.JSRuntime)
        {
            SelectionDecider = snapshot =>
            {
                seen.Add(snapshot);
                FocusEventArgs args = new() { Type = snapshot.Type };
                args.PreventPartHandler();
                return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
            },
        };

        Assert.Equal("select", CompositeTwin.SelectPoint);
        Assert.Equal("stop", twin.Decide(CompositeTwin.SelectPoint, FocusJson));
        Assert.Equal("base-ui-4", Assert.Single(seen).Target?.Id);
    }

    [Fact]
    [Trait("Requirement", "CMP-EVT-12")]
    public void Without_a_decider_or_on_another_point_the_verdict_is_run()
    {
        using BunitContext context = new();
        CompositeTwin undecided = new(context.JSInterop.JSRuntime);
        CompositeTwin decided = new(context.JSInterop.JSRuntime)
        {
            NavigationDecider = _ => DecisionPolicy.Stop,
            SelectionDecider = _ => DecisionPolicy.Stop,
        };

        Assert.Equal("run", undecided.Decide(CompositeTwin.NavigatePoint, KeydownJson));
        Assert.Equal("run", undecided.Decide(CompositeTwin.SelectPoint, FocusJson));
        Assert.Equal("run", decided.Decide("activate", KeydownJson));
    }

    // M5 track 1 (issue 159; ADR 04 Amendments 2026-11-03 (1), chosen by the user): a list handle's session, list options
    // and role are sent only when set, and each list option only when it differs from Base UI's default.
    [Fact]
    [Trait("Requirement", "CMP-STR-06")]
    public void A_list_handle_sends_its_session_its_list_options_and_one_role_and_each_option_only_when_set()
    {
        string defaults = JsonSerializer.Serialize(new CompositeOptions(), BlazixJsonContext.Default.CompositeOptions);
        Assert.Equal(DefaultsJson, defaults);

        CompositeOptions floating = new()
        {
            SessionToken = "session-1",
            List = new CompositeListOptions(),
            Floating = new CompositeFloatingOptions(),
            Items = [new CompositeItemRegistration("a", null) { Label = "Alpha", Index = 0 }, new CompositeItemRegistration("b", null) { Label = string.Empty, TextId = "t", OmitTabIndex = true }],
        };
        string json = JsonSerializer.Serialize(floating, BlazixJsonContext.Default.CompositeOptions);
        Assert.Contains("""
            "items":[{"id":"a","label":"Alpha","index":0},{"id":"b","label":"","textId":"t","omitTabIndex":true}]
            """.Trim(), json, StringComparison.Ordinal);
        Assert.EndsWith(""","sessionToken":"session-1","list":{},"floating":{}}""", json, StringComparison.Ordinal);

        CompositeListOptions every = new()
        {
            Enabled = false,
            LoopFocus = true,
            Orientation = "horizontal",
            TriggerOrientation = "vertical",
            Rtl = true,
            Virtual = true,
            Nested = true,
            ParentOrientation = "both",
            FocusItemOnOpen = false,
            AllowEscape = true,
            OpenOnArrowKeyDown = false,
            FocusItemOnHover = false,
            ResetOnPointerLeave = false,
            DisabledIndices = [],
            DeriveDisabledIndices = true,
            SelectedIndex = 2,
            Grid = true,
            KeepOpenOnCrossOrientationKey = true,
            NestedReturnFocusElement = 0,
            RelayElement = 1,
            Highlight = new CompositeHighlightRequest(null, 3),
            Typeahead = new CompositeTypeaheadOptions { ResetMs = 500, Skip = "element", Labels = ["one", null] },
            Cols = 3,
            ItemCount = 40,
        };
        Assert.Equal(
            """{"enabled":false,"loopFocus":true,"orientation":"horizontal","triggerOrientation":"vertical","rtl":true,"virtual":true,"nested":true,"parentOrientation":"both","focusItemOnOpen":false,"allowEscape":true,"openOnArrowKeyDown":false,"focusItemOnHover":false,"resetOnPointerLeave":false,"disabledIndices":[],"deriveDisabledIndices":true,"selectedIndex":2,"grid":true,"keepOpenOnCrossOrientationKey":true,"nestedReturnFocusElement":0,"relayElement":1,"highlight":{"revision":3},"typeahead":{"resetMs":500,"skip":"element","labels":["one",null]},"cols":3,"itemCount":40}""",
            JsonSerializer.Serialize(every, BlazixJsonContext.Default.CompositeListOptions));
        Assert.Contains(""","reference":{}}""", JsonSerializer.Serialize(new CompositeOptions { Reference = new CompositeReferenceOptions() }, BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
        Assert.Contains(""","trigger":{}}""", JsonSerializer.Serialize(new CompositeOptions { Trigger = new CompositeTriggerOptions() }, BlazixJsonContext.Default.CompositeOptions), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CMP-EVT-04")]
    [Trait("Requirement", "CMP-EVT-06")]
    [Trait("Requirement", "CMP-EVT-10")]
    public void The_list_reports_raise_their_events_with_the_payloads()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<CompositeListHighlight> highlights = [];
        List<CompositeListOpenRequest> requests = [];
        List<CompositeListMatch> matches = [];
        twin.ListHighlightChanged += highlights.Add;
        twin.ListOpenRequested += requests.Add;
        twin.ListMatched += matches.Add;

        twin.OnListHighlight($$"""{"index":2,"itemId":"base-ui-7","event":{{KeydownJson}},"revision":1}""");
        twin.OnListHighlight("""{"index":null,"itemId":null,"event":null,"revision":1}""");
        twin.OnListOpenRequest($$"""{"open":true,"reason":"list-navigation","event":{{KeydownJson}},"triggerId":"base-ui-1"}""");
        twin.OnListMatch($$"""{"index":1,"event":{{KeydownJson}}}""");

        Assert.Equal(2, highlights.Count);
        Assert.Equal((2, "base-ui-7", "keydown", 1), (highlights[0].Index, highlights[0].ItemId, highlights[0].Event?.Type, highlights[0].Revision));
        Assert.Equal((null, null, null), (highlights[1].Index, highlights[1].ItemId, highlights[1].Event));
        CompositeListOpenRequest request = Assert.Single(requests);
        Assert.Equal((true, "list-navigation", "base-ui-1"), (request.Open, request.Reason, request.TriggerId));
        Assert.Equal(1, Assert.Single(matches).Index);
    }

    // Design point 9 (a) of M5 track 6 (settled by the orchestrator 2026-11-04; ADR 04 Amendments 2026-11-04): the highlight
    // report's label, absent when no item is highlighted or the item has none.
    [Fact(DisplayName = "port: the highlight report's label is read by its module name, and an absent one is null")]
    [Trait("Requirement", "CMP-EVT-04")]
    [Trait("Requirement", "MNU-EVT-10")]
    public void The_highlight_reports_label_is_read_by_its_module_name_and_an_absent_one_is_null()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<CompositeListHighlight> highlights = [];
        twin.ListHighlightChanged += highlights.Add;

        twin.OnListHighlight("""{"index":1,"itemId":"base-ui-7","event":null,"revision":0,"label":"Second"}""");
        twin.OnListHighlight("""{"index":null,"itemId":null,"event":null,"revision":0}""");

        Assert.Equal(["Second", null], highlights.Select(highlight => highlight.Label));
    }

    // ADR 04 Amendments 2026-11-03 (3): the Shift+Tab close's point, per twin.
    [Fact]
    [Trait("Requirement", "CMP-KEY-24")]
    [Trait("Requirement", "CMP-EVT-08")]
    public void Decide_on_leave_runs_the_leave_decider_and_answers_run_without_one()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        Assert.Equal("leave", CompositeTwin.LeavePoint);
        Assert.Equal("run", twin.Decide(CompositeTwin.LeavePoint, KeydownJson));

        EventSnapshot? seen = null;
        twin.LeaveDecider = snapshot =>
        {
            seen = snapshot;
            return DecisionPolicy.Stop;
        };

        Assert.Equal("stop", twin.Decide(CompositeTwin.LeavePoint, KeydownJson));
        Assert.Equal("ArrowDown", seen?.Key);
    }

    // Design point 5 (b), chosen by the user 2026-11-04 (M5 track 1, issue 159): a long order report arrives in parts of
    // one publication; the twin raises ItemsChanged once with the joined ids, and an incomplete older publication is
    // dropped when a newer one starts.
    [Fact]
    [Trait("Requirement", "CMP-EVT-03")]
    public void A_chunked_order_report_raises_ItemsChanged_once_with_the_joined_ids_and_drops_an_incomplete_older_publication()
    {
        using BunitContext context = new();
        CompositeTwin twin = new(context.JSInterop.JSRuntime);
        List<IReadOnlyList<string>> changes = [];
        twin.ItemsChanged += changes.Add;

        twin.OnItemsChange("""{"ids":["a","b"],"publication":1,"part":0,"parts":2}""");
        twin.OnItemsChange("""{"ids":["x"],"publication":2,"part":0,"parts":2}""");
        twin.OnItemsChange("""{"ids":["y","z"],"publication":2,"part":1,"parts":2}""");
        twin.OnItemsChange("""{"ids":["c"],"publication":1,"part":1,"parts":2}""");
        twin.OnItemsChange("""{"ids":["w"]}""");

        Assert.Equal(2, changes.Count);
        Assert.Equal(["x", "y", "z"], changes[0]);
        Assert.Equal(["w"], changes[1]);
    }
}
