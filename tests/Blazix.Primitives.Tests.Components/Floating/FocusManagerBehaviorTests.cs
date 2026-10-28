using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <see cref="FocusManagerBehavior"/> at L2 over the test-only <see cref="TestFocusManaged"/> (design point 6 of the
/// focus-manager track, settled by the orchestrator 2026-10-27): the inside guards' render rule (FLT-DOM-15's C# half),
/// the publication to an enclosing portal (FLT-FOC-18's C# half), the first attach inside a portal after the portal's
/// move, the options sent from the parameter set (design point 1, chosen by the user 2026-10-27), the element targets
/// through the references array (FLT-API-05), the focus-out close routed and answered (FLT-FOC-11) and the close type
/// classified (FLT-FOC-10). The modules are mocked; their behaviour is L3 and L4.
/// </summary>
public sealed class FocusManagerBehaviorTests : IDisposable
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/focus-manager.js";
    private const string PortalModulePath = "./_content/Blazix.Primitives/js/portal.js";

    private const string FocusOutJson = """
        {"kind":"FocusEvent","type":"focusout","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":12.5}
        """;

    private readonly PopupTestContext test = new();
    private readonly BunitJSModuleInterop module;
    private readonly BunitJSModuleInterop handle;

    public FocusManagerBehaviorTests()
    {
        module = test.Context.JSInterop.SetupModule(ModulePath);
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        BunitJSModuleInterop portalModule = test.Context.JSInterop.SetupModule(PortalModulePath);
        BunitJSModuleInterop portalHandle = portalModule.SetupModule("attach", _ => true);
        portalHandle.SetupVoid("update", _ => true).SetVoidResult();
        portalHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Theory(DisplayName = "port: the inside guards render before and after the popup iff the manager is enabled and modal or inside a portal")]
    [Trait("Requirement", "FLT-DOM-15")]
    [Trait("Requirement", "FLT-DOM-13")]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public void The_inside_guards_render_iff_enabled_and_modal_or_inside_a_portal(bool modal, bool inPortal, bool disabled, bool renders)
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            RenderFragment managed = content =>
            {
                content.OpenComponent<TestFocusManaged>(0);
                content.AddComponentParameter(1, nameof(TestFocusManaged.Modal), modal);
                content.AddComponentParameter(2, nameof(TestFocusManaged.Disabled), disabled);
                content.CloseComponent();
            };
            if (inPortal)
            {
                builder.OpenComponent<TestPopupPortal>(0);
                builder.AddComponentParameter(1, nameof(TestPopupPortal.ChildContent), managed);
                builder.CloseComponent();
            }
            else
            {
                builder.AddContent(2, managed);
            }
        });

        IElement popup = cut.Find("[role=\"dialog\"]");
        IReadOnlyList<IElement> guards = cut.FindAll("[data-base-ui-focus-guard][data-type=\"inside\"]");
        if (!renders)
        {
            Assert.Empty(guards);
            return;
        }

        Assert.Equal(2, guards.Count);
        Assert.Same(guards[0], popup.PreviousElementSibling);
        Assert.Same(guards[1], popup.NextElementSibling);
        foreach (IElement guard in guards)
        {
            Assert.Equal("SPAN", guard.TagName);
            Assert.Equal("0", guard.GetAttribute("tabindex"));
            Assert.Equal("true", guard.GetAttribute("aria-hidden"));
            Assert.Equal(VisuallyHidden.Style, guard.GetAttribute("style"));
        }
    }

    [Fact(DisplayName = "port: an enabled manager inside a portal publishes modal, open and closeOnFocusOut to it, and withdraws when disabled or removed")]
    [Trait("Requirement", "FLT-FOC-18")]
    [Trait("Requirement", "FLT-DOM-14")]
    public async Task An_enabled_manager_inside_a_portal_publishes_to_it_and_withdraws_when_disabled_or_removed()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) =>
            {
                builder.OpenComponent<TestPopupPortal>(0);
                builder.AddComponentParameter(1, nameof(TestPopupPortal.ChildContent), (RenderFragment)(content =>
                {
                    if (host.Get<bool>("rendered"))
                    {
                        content.OpenComponent<TestFocusManaged>(0);
                        content.AddComponentParameter(1, nameof(TestFocusManaged.Modal), host.Get<bool>("modal"));
                        content.AddComponentParameter(2, nameof(TestFocusManaged.Disabled), host.Get<bool>("disabled"));
                        content.CloseComponent();
                    }
                }));
                builder.CloseComponent();
            },
            new() { ["rendered"] = true, ["modal"] = false, ["disabled"] = false });
        PopupPortalContext portal = cut.FindComponent<TestPopupPortal>().Instance.PortalContext;
        PopupTestHost host = cut.Instance;

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-base-ui-focus-guard][data-type=\"outside\"]").Count));
        Assert.Equal(new PortalFocusPublication(Modal: false, Open: true, CloseOnFocusOut: true), portal.Publication);

        await host.SetAsync("disabled", true);
        Assert.Null(portal.Publication);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-focus-guard][data-type=\"outside\"]")));

        await host.SetAsync("modal", true);
        await host.SetAsync("disabled", false);
        Assert.Equal(new PortalFocusPublication(Modal: true, Open: true, CloseOnFocusOut: true), portal.Publication);
        Assert.Empty(cut.FindAll("[data-base-ui-focus-guard][data-type=\"outside\"]"));

        await host.SetAsync("rendered", false);
        Assert.Null(portal.Publication);
    }

    [Fact(DisplayName = "port: the first attach inside a portal waits for the portal's move")]
    [Trait("Requirement", "FLT-FOC-06")]
    public void The_first_attach_inside_a_portal_waits_for_the_portal_s_move()
    {
        PopupPortalContext portal = new();
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<CascadingValue<PopupPortalContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<PopupPortalContext>.Value), portal);
            builder.AddComponentParameter(2, nameof(CascadingValue<PopupPortalContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<PopupPortalContext>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<TestFocusManaged>(0);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(module.Invocations["attach"]);

        portal.CompleteMove();

        cut.WaitForAssertion(() => Assert.Single(module.Invocations["attach"]));
        Assert.True(cut.FindComponent<TestFocusManaged>().Instance.Behavior.TwinAttached.IsCompleted);
    }

    [Fact(DisplayName = "port: changed inputs are sent from the parameter set, before the render that applies them, and unchanged inputs send nothing")]
    [Trait("Requirement", "ADR01-commit-path-update")]
    [Trait("Requirement", "FLT-FOC-19")]
    public async Task Changed_inputs_are_sent_from_the_parameter_set_and_unchanged_inputs_send_nothing()
    {
        IRenderedComponent<TestFocusManaged> managed = await RenderAttachedAsync();

        // A render whose parameters changed nothing sends nothing.
        managed.Render();
        Assert.Empty(handle.Invocations["update"]);

        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with { Disabled = true, SessionToken = "base-ui-9" }));

        // No render ran: the update left from the parameter set itself.
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Contains("\"disabled\":true", (string)update.Arguments[0]!, StringComparison.Ordinal);
        Assert.Contains("\"sessionToken\":\"base-ui-9\"", (string)update.Arguments[0]!, StringComparison.Ordinal);

        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with { Disabled = true, SessionToken = "base-ui-9" }));

        Assert.Single(handle.Invocations["update"]);
    }

    [Fact(DisplayName = "port: element targets cross as references, id targets as ids, and false sends no focus")]
    [Trait("Requirement", "FLT-API-05")]
    [Trait("Requirement", "FLT-KEY-06")]
    public async Task Element_targets_cross_as_references_id_targets_as_ids_and_false_sends_no_focus()
    {
        IRenderedComponent<TestFocusManaged> managed = await RenderAttachedAsync();
        ElementReference initial = new("initial-target");
        ElementReference previous = new("previous-target");
        ElementReference next = new("next-target");
        ElementReference inside = new("inside-target");

        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with
        {
            InitialFocus = new FocusTargetValue(initial),
            ReturnFocus = new FocusTargetValue("final-target"),
            PreviousFocusableElement = previous,
            NextFocusableElement = next,
            InsideElements = [inside],
        }));
        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with
        {
            InitialFocus = new FocusTargetValue(false),
            ReturnFocus = new FocusTargetValue(new ElementReference()),
        }));

        IReadOnlyList<JSRuntimeInvocation> updates = handle.Invocations["update"];
        Assert.Equal(2, updates.Count);
        string targets = (string)updates[0].Arguments[0]!;
        Assert.Contains("\"initialFocus\":true,\"initialFocusElement\":0,", targets, StringComparison.Ordinal);
        Assert.Contains("\"returnFocus\":true,\"returnFocusId\":\"final-target\",", targets, StringComparison.Ordinal);
        Assert.Contains("\"previousFocusableElement\":1,\"nextFocusableElement\":2,\"insideElements\":[3],", targets, StringComparison.Ordinal);
        Assert.Equal([initial, previous, next, inside], Assert.IsType<object[]>(updates[0].Arguments[1]));

        // An element the render has not captured yet names nothing: the module takes the default target.
        string fallback = (string)updates[1].Arguments[0]!;
        Assert.Contains("\"initialFocus\":false,", fallback, StringComparison.Ordinal);
        Assert.Contains("\"returnFocus\":true,", fallback, StringComparison.Ordinal);
        Assert.DoesNotContain("\"initialFocusElement\"", fallback, StringComparison.Ordinal);
        Assert.DoesNotContain("\"returnFocusElement\"", fallback, StringComparison.Ordinal);
        Assert.Single(updates[1].Arguments);
    }

    [Fact(DisplayName = "port: a focus-out close request is routed to the popup, and the answered count rises whether or not it closed")]
    [Trait("Requirement", "FLT-FOC-11")]
    public async Task A_focus_out_close_request_is_routed_and_answered_whether_or_not_it_closed()
    {
        List<EventSnapshot> requests = [];
        IRenderedComponent<TestFocusManaged> managed = await RenderAttachedAsync(onFocusOutClose: snapshot =>
        {
            // The consumer cancels: nothing renders.
            requests.Add(snapshot);
            return Task.CompletedTask;
        });
        FocusManagerTwin twin = Twin();

        await managed.InvokeAsync(() => twin.OnFocusOutClose($$"""{"event":{{FocusOutJson}},"interactionType":"keyboard"}"""));
        await managed.InvokeAsync(() => twin.OnFocusOutClose($$"""{"event":{{FocusOutJson}},"interactionType":"keyboard"}"""));

        Assert.Equal(2, requests.Count);
        Assert.Equal("focusout", requests[0].Type);
        Assert.Equal(2, managed.Instance.Behavior.Answered);
        IReadOnlyList<JSRuntimeInvocation> updates = handle.Invocations["update"];
        Assert.Equal(2, updates.Count);
        Assert.Contains("\"answered\":1", (string)updates[0].Arguments[0]!, StringComparison.Ordinal);
        Assert.Contains("\"answered\":2", (string)updates[1].Arguments[0]!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the close type is classified when the close commits, from its snapshot and the type the module recorded")]
    [Trait("Requirement", "FLT-FOC-10")]
    public async Task The_close_type_is_classified_when_the_close_commits()
    {
        IRenderedComponent<TestFocusManaged> managed = await RenderAttachedAsync();
        FocusManagerTwin twin = Twin();
        EventSnapshot focusOut = new() { Kind = EventKind.FocusEvent, Type = "focusout" };
        EventSnapshot escape = new() { Kind = EventKind.KeyboardEvent, Type = "keydown", Key = "Escape" };

        twin.OnInteractionTypeChange("""{"type":"touch"}""");
        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with { Open = false, CloseEvent = focusOut }));
        Assert.Equal(InteractionType.Touch, managed.Instance.Behavior.RecordedInteractionType);

        // A type recorded after the close changes nothing that was sent.
        twin.OnInteractionTypeChange("""{"type":"mouse"}""");
        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with { Open = false, CloseEvent = focusOut }));
        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(managed.Instance.Inputs() with { Open = false, CloseEvent = escape }));

        IReadOnlyList<JSRuntimeInvocation> updates = handle.Invocations["update"];
        Assert.Equal(2, updates.Count);
        Assert.Contains("\"closeInteractionType\":\"touch\"", (string)updates[0].Arguments[0]!, StringComparison.Ordinal);
        Assert.Contains("\"closeInteractionType\":\"keyboard\"", (string)updates[1].Arguments[0]!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: setting inputs equal to the current ones allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public async Task Setting_inputs_equal_to_the_current_ones_allocates_nothing()
    {
        ElementReference inside = new("inside-target");
        IRenderedComponent<TestFocusManaged> managed = await RenderAttachedAsync();
        FocusManagerInputs first = managed.Instance.Inputs() with
        {
            InitialFocus = new FocusTargetValue("initial-target"),
            ReturnFocus = new FocusTargetValue(inside),
            InsideElements = [inside],
            Relay = new OpenChangeRelay(1, "trigger-press", EventKind.MouseEvent, "click", null, null),
        };
        FocusManagerInputs same = first with { InsideElements = [inside], Relay = first.Relay! with { } };
        await managed.InvokeAsync(() => managed.Instance.Behavior.SetInputs(first));
        FocusManagerBehavior behavior = managed.Instance.Behavior;
        for (int i = 0; i < 20; i++)
        {
            behavior.SetInputs(same);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        behavior.SetInputs(same);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    // ADR 16 rule 7 (the PopupPortalBehavior precedent, ade21f2): the options are built at attach and when an input
    // changed, never for a render that changed nothing.
    [Fact(DisplayName = "port: a render after attach with unchanged inputs builds no options and allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_render_after_attach_with_unchanged_inputs_builds_no_options_and_allocates_nothing()
    {
        IRenderedComponent<TestFocusManaged> managed = await RenderAttachedAsync();
        FocusManagerBehavior behavior = managed.Instance.Behavior;
        ElementReference? element = managed.Instance.Element;
        for (int i = 0; i < 20; i++)
        {
            await behavior.AfterRenderAsync(element, null);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        Task rendered = behavior.AfterRenderAsync(element, null);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await rendered;

        Assert.Empty(handle.Invocations["update"]);
        Assert.Equal(0, allocated);
    }

    // Found at L4 on a cold Server host: a popup closed before its module loaded is gone when `attach` reaches the
    // browser, and the module's JSException would end the circuit from OnAfterRenderAsync (the CollapsiblePanelBehavior
    // precedent).
    [Fact(DisplayName = "port: an attach that fails because the popup left the document before it arrived is dropped, and a later render attaches again")]
    [Trait("Requirement", "FLT-FOC-06")]
    public async Task An_attach_that_fails_because_the_popup_left_is_dropped_and_a_later_render_attaches_again()
    {
        GoneElementRuntime runtime = new();
        await using FocusManagerBehavior behavior = new(runtime, work => work(), _ => Task.CompletedTask);

        await behavior.AfterRenderAsync(new ElementReference("gone"), null);
        await behavior.AfterRenderAsync(new ElementReference("gone"), null);

        Assert.False(behavior.TwinAttached.IsCompleted);
        Assert.Equal(2, runtime.Attaches);
    }

    private async Task<IRenderedComponent<TestFocusManaged>> RenderAttachedAsync(Func<EventSnapshot, Task>? onFocusOutClose = null)
    {
        IRenderedComponent<TestFocusManaged> managed = test.Context.Render<TestFocusManaged>(p => p.Add(x => x.OnFocusOutClose, onFocusOutClose));
        await managed.Instance.Behavior.TwinAttached;
        Assert.Single(module.Invocations["attach"]);
        return managed;
    }

    /// <summary>A runtime whose module's <c>attach</c> fails as the module does for an element no longer in the document.</summary>
    /// <remarks>BL0016 asks callers of JS interop to guard the call; this is the runtime itself, which only answers.</remarks>
#pragma warning disable BL0016
    private sealed class GoneElementRuntime : IJSRuntime, IJSObjectReference
    {
        public int Attaches { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => Answer<TValue>(identifier);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => Answer<TValue>(identifier);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private ValueTask<TValue> Answer<TValue>(string identifier)
        {
            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            Attaches++;
            return ValueTask.FromException<TValue>(new JSException("Blazix: no element for the reference"));
        }
    }
#pragma warning restore BL0016

    private FocusManagerTwin Twin() =>
        Assert.IsType<DotNetObjectReference<FocusManagerTwin>>(Assert.Single(module.Invocations["attach"]).Arguments[2]).Value;
}
