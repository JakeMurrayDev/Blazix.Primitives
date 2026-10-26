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
}
