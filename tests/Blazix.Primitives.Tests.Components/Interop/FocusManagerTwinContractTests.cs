using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>focus-manager</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-27): bUnit's JS interop
/// mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive
/// <see cref="FocusManagerTwin"/>'s <c>[JSInvokable]</c> callbacks.
/// </summary>
public sealed class FocusManagerTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/focus-manager.js";

    private const string FocusOutJson = """
        {"kind":"FocusEvent","type":"focusout","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":12.5}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FLT-API-05")]
    [Trait("Requirement", "FLT-FOC-19")]
    [Trait("Requirement", "FLT-FOC-20")]
    [Trait("Requirement", "FLT-EVT-02")]
    public async Task Attach_imports_the_focus_manager_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using FocusManagerTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<FocusManagerOptions> attached = await twin.AttachAsync("base-ui-1", new FocusManagerOptions());
        ElementReference initial = new("initial-target");
        ElementReference final = new("final-target");
        TwinReferences references = new();
        references.Add(initial);
        references.Add(final);

        // A repeated attach of the same element is a no-op (ADR 06 Amendments 2026-10-18 rule 2).
        Assert.Same(attached, await twin.AttachAsync("base-ui-1", new FocusManagerOptions { Modal = false }));
        await attached.UpdateAsync(new FocusManagerOptions
        {
            Modal = false,
            Disabled = true,
            InitialFocus = false,
            InitialFocusElement = 0,
            InitialFocusId = "base-ui-2",
            ReturnFocus = false,
            ReturnFocusElement = 1,
            ReturnFocusId = "base-ui-3",
            ExplicitReturnFocus = true,
            CloseInteractionType = "keyboard",
            RestoreFocus = FocusManagerTwin.RestorePopup,
            CloseOnFocusOut = false,
            OpenInteractionType = null,
            OpenSelector = "[data-open]",
            TreatPopupAsFloatingElement = true,
            DomReferenceId = "base-ui-4",
            TriggerFocusGuards = true,
            PreviousFocusableElement = 3,
            NextFocusableElement = 4,
            InsideElements = [2],
            InsideElementIds = ["base-ui-5"],
            Relay = new OpenChangeRelay(3, "trigger-press", EventKind.PointerEvent, "click", "mouse", "base-ui-6", 2, "escape-key"),
            SessionToken = "base-ui-8",
            Answered = 2,
        }, references);
        await attached.DisposeAsync();
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"modal":true,"disabled":false,"initialFocus":true,"returnFocus":true,"restoreFocus":"none","closeOnFocusOut":true,"openInteractionType":"","treatPopupAsFloatingElement":false,"triggerFocusGuards":false,"answered":0}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<FocusManagerTwin>>(attach.Arguments[2]);

        // A programmatic open's null interaction type is sent, not omitted (FLT-FOC-20).
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"modal":false,"disabled":true,"initialFocus":false,"initialFocusElement":0,"initialFocusId":"base-ui-2","returnFocus":false,"returnFocusElement":1,"returnFocusId":"base-ui-3","explicitReturnFocus":true,"closeInteractionType":"keyboard","restoreFocus":"popup","closeOnFocusOut":false,"openInteractionType":null,"openSelector":"[data-open]","treatPopupAsFloatingElement":true,"domReferenceId":"base-ui-4","triggerFocusGuards":true,"previousFocusableElement":3,"nextFocusableElement":4,"insideElements":[2],"insideElementIds":["base-ui-5"],"relay":{"revision":3,"reason":"trigger-press","eventKind":"PointerEvent","eventType":"click","pointerType":"mouse","triggerId":"base-ui-6","coalescedCloseRevision":2,"coalescedCloseReason":"escape-key"},"sessionToken":"base-ui-8","answered":2}""",
            update.Arguments[0]);

        // Element targets cross beside the options, revived by Blazor; the options carry their indices (ADR 04
        // Amendments 2026-10-27, element-typed option values).
        Assert.Equal([initial, final], Assert.IsType<object[]>(update.Arguments[1]));
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "FLT-FOC-11")]
    [Trait("Requirement", "FLT-FOC-10")]
    public async Task A_focus_out_report_becomes_one_close_request_with_the_focus_out_reason_and_the_snapshot()
    {
        await using FocusManagerTwin twin = new(new NoRuntime());
        List<FocusOutCloseRequest> requests = [];
        twin.FocusOutCloseRequested += requests.Add;

        twin.OnFocusOutClose($$"""{"event":{{FocusOutJson}},"interactionType":"keyboard"}""");

        FocusOutCloseRequest request = Assert.Single(requests);
        Assert.Equal(EventReason.FocusOut, request.Reason);
        Assert.Equal(EventKind.FocusEvent, request.Event.Kind);
        Assert.Equal("focusout", request.Event.Type);
        Assert.Equal("base-ui-7", request.Event.Target!.Id);
        Assert.Equal(12.5, request.Event.TimeStamp);
        Assert.Equal("keyboard", request.InteractionType);
    }

    [Fact]
    [Trait("Requirement", "FLT-FOC-10")]
    [Trait("Requirement", "FLT-EDGE-08")]
    public async Task Each_interaction_type_report_raises_its_event_once_with_its_literal()
    {
        await using FocusManagerTwin twin = new(new NoRuntime());
        List<string> types = [];
        twin.InteractionTypeChanged += types.Add;

        twin.OnInteractionTypeChange("""{"type":"touch"}""");
        twin.OnInteractionTypeChange("""{"type":""}""");
        twin.OnInteractionTypeChange("""{"type":"keyboard"}""");

        Assert.Equal(["touch", "", "keyboard"], types);
    }

    [Fact]
    public async Task The_module_has_no_decision_point()
    {
        await using FocusManagerTwin twin = new(new NoRuntime());

        Assert.Equal("run", twin.Decide("guard", FocusOutJson));
        Assert.Equal("run", twin.Decide("escape", FocusOutJson));
    }

    // ADR 16 Amendments 2026-10-27: options records with value equality, so an unchanged render sends no update.
    [Fact]
    public void Options_compare_by_value_including_the_lists()
    {
        FocusManagerOptions first = new() { DomReferenceId = "base-ui-4", InsideElements = [1, 2], InsideElementIds = ["a"] };
        FocusManagerOptions same = new() { DomReferenceId = "base-ui-4", InsideElements = [1, 2], InsideElementIds = ["a"] };

        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, same with { InsideElements = [2, 1] });
        Assert.NotEqual(first, same with { InsideElementIds = null });
        Assert.NotEqual(first, same with { OpenInteractionType = null });
        Assert.NotEqual(first, same with { Style = "color: red" });
        Assert.NotEqual(first, same with { PreviousFocusableElement = 1 });
        Assert.NotEqual(first, same with { NextFocusableElement = 1 });
        Assert.NotEqual(first, same with { Answered = 1 });
        Assert.NotEqual(first, same with { SessionToken = "base-ui-8" });
        Assert.NotEqual(first, same with { Relay = new OpenChangeRelay(1, "trigger-press", EventKind.MouseEvent, "click", null, null) });
        Assert.Equal(
            first with { Relay = new OpenChangeRelay(1, "trigger-press", EventKind.MouseEvent, "click", null, null) },
            same with { Relay = new OpenChangeRelay(1, "trigger-press", EventKind.MouseEvent, "click", null, null) });
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}
