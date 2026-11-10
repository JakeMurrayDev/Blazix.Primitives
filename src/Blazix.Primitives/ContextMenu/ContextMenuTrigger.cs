using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CTX §3.2 state object (context-menu/trigger/ContextMenuTrigger.tsx:194-196, 220-225).
/// <summary>Represents the state of a context menu trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the context menu is open; projected as <c>data-popup-open</c> and <c>data-pressed</c>.</param>
[PartState]
public sealed partial record ContextMenuTriggerState(bool Open);

// context-menu/trigger/ContextMenuTriggerDataAttributes.ts (CTX-DOM-01).
/// <summary>Provides the names of the data attributes a context menu trigger renders.</summary>
public static class ContextMenuTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the context menu is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = "data-popup-open";

    /// <summary>Represents the attribute present while the context menu is open: <c>data-pressed</c>.</summary>
    public const string Pressed = "data-pressed";
}

// Base UI's ContextMenu.Trigger (context-menu/trigger/ContextMenuTrigger.tsx); the contract is
// docs/components/context-menu.md §3.2, §6.2. A div with no parameters of its own (CTX-API-06), no role, tabindex, ARIA
// or focus guards (CTX-STR-08), its state from the context menu's menu (CTX-STA-01). One ContextMenuTwin handle on its
// element (ADR 04 Amendments 2026-10-18; design points 1-3 of M5 track 7) owns the gestures: the module reports the open
// request (reason `trigger-press`, the `contextmenu` or `touchstart` event, no trigger: CTX-EVT-02, 03) and the
// `cancel-open` close (CTX-EVT-05), which this part requests from the menu. The consumer's `oncontextmenu` and `ontouch*`
// handlers are declined and run once per dispatch through the module (design point 3 (a)): `press` on WebAssembly, the
// `OnPress` report on Server, whose veto refuses the open the report precedes (Server: relaxed, the module's own side
// effects of the vetoed gesture remain).
/// <summary>
/// Represents an area that opens the context menu on a right click or a long press, Base UI's <c>ContextMenu.Trigger</c>,
/// rendered as a <c>div</c>.
/// </summary>
/// <remarks>
/// A consumer's <c>oncontextmenu</c>, <c>ontouchstart</c>, <c>ontouchmove</c>, <c>ontouchend</c> and
/// <c>ontouchcancel</c> handlers run before the trigger's own handling of the event; calling <c>PreventPartHandler()</c>
/// on the event arguments skips it. The browser's own context menu stays suppressed over the trigger while the context
/// menu is enabled.
/// </remarks>
public sealed class ContextMenuTrigger : PartBase<ContextMenuTriggerState>
{
    // ContextMenuTrigger.tsx:210-212.
    private const string TouchCalloutStyle = "-webkit-touch-callout: none";

    private static readonly StateAttributesMapping<ContextMenuTriggerState> s_mapping = PopupStateMapping.CreatePressableTrigger<ContextMenuTriggerState>();
    private static readonly string[] s_handlerEvents = ["contextmenu", "touchstart", "touchmove", "touchend", "touchcancel"];
    private static readonly IReadOnlyList<string>?[] s_handlerLists = new IReadOnlyList<string>?[1 << 5];

    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AttributeValue[] handlers = new AttributeValue[5];
    private readonly bool[] vetoed = new bool[5];
    private MenuSubscription? subscription;
    private MenuSelection<bool>? open;
    private MenuSelection<bool>? disabled;
    private ContextMenuTriggerState state = new(false);
    private ContextMenuTwin? twin;
    private TwinHandle<ContextMenuOptions>? handle;
    private ContextMenuOptions? sentOptions;
    private int handlerMask;
    private bool attaching;
    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ContextMenuRootContext? Root { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ContextMenuTriggerState State
    {
        get
        {
            bool isOpen = open!.Value;
            if (state.Open != isOpen)
            {
                state = new ContextMenuTriggerState(isOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ContextMenuTriggerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the trigger's handle has attached.</summary>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is not inside a <see cref="ContextMenuRoot"/>.</exception>
    protected override void OnInitialized()
    {
        // CTX-EDGE-01 (ContextMenuRootContext.ts:28-33).
        ContextMenuRootContext root = Root ?? throw MenuParts.MissingRoot("ContextMenu");
        MenuRootContext menu = root.Menu!;
        subscription = menu.Subscribe(StateHasChanged);
        open = subscription.Select(MenuSelectors.Open);
        disabled = subscription.Select(MenuSelectors.Disabled);
        twin = new ContextMenuTwin(JSRuntime) { PressDecider = DecidePress };
        twin.OpenRequested += OnOpenRequested;
        twin.CancelOpenRequested += OnCancelOpenRequested;
        twin.Pressed += OnPressed;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        handlerMask = 0;
        for (int i = 0; i < s_handlerEvents.Length; i++)
        {
            handlers[i] = default;
        }

        if (AdditionalAttributes is { } attributes)
        {
            foreach (KeyValuePair<string, object> pair in attributes)
            {
                int index = HandlerIndex(pair.Key);
                if (index < 0)
                {
                    continue;
                }

                AttributeValue value = AttributeValue.FromObject(pair.Value);
                if (MergeProps.IsHandler(pair.Key, value))
                {
                    handlers[index] = value;
                    handlerMask |= 1 << index;
                }
            }
        }

        base.OnParametersSet();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _ = firstRender;
        if (disposed || attaching)
        {
            return;
        }

        ContextMenuOptions options = BuildOptions();
        if (handle is null)
        {
            if (Element is null && Id is null)
            {
                return;
            }

            attaching = true;
            try
            {
                handle = Element is { } element ? await twin!.AttachAsync(element, options) : await twin!.AttachAsync(Id!, options);
            }
            finally
            {
                attaching = false;
            }

            sentOptions = options;
            attachedSource.TrySetResult();
            options = BuildOptions();
        }

        if (!options.Equals(sentOptions))
        {
            sentOptions = options;
            await handle.UpdateAsync(options);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            subscription?.Dispose();
            if (twin is not null)
            {
                twin.OpenRequested -= OnOpenRequested;
                twin.CancelOpenRequested -= OnCancelOpenRequested;
                twin.Pressed -= OnPressed;
                _ = ReleaseAsync(handle, twin);
            }
        }

        base.Dispose(disposing);
    }

    // CTX-API-06: the five handlers the module runs.
    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => HandlerIndex(eventName) < 0;

    // ContextMenuTrigger.tsx:210-212: the part's style comes first, so the consumer's declarations win.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("style", TouchCalloutStyle);

    private static int HandlerIndex(string eventName)
    {
        if (eventName.Length < 3 || !eventName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        ReadOnlySpan<char> name = eventName.AsSpan(2);
        for (int i = 0; i < s_handlerEvents.Length; i++)
        {
            if (name.Equals(s_handlerEvents[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // The handler names by mask, built once per mask, so an unchanged render sends nothing and allocates nothing.
    private static IReadOnlyList<string>? HandlerList(int mask)
    {
        if (mask == 0)
        {
            return null;
        }

        IReadOnlyList<string>? list = s_handlerLists[mask];
        if (list is null)
        {
            List<string> names = [];
            for (int i = 0; i < s_handlerEvents.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    names.Add(s_handlerEvents[i]);
                }
            }

            list = names.ToArray();
            s_handlerLists[mask] = list;
        }

        return list;
    }

    private static MouseEventArgs ToMouseArgs(ContextMenuPress press, EventSnapshot snapshot) => new()
    {
        Type = snapshot.Type,
        Detail = snapshot.Detail,
        ScreenX = press.ScreenX,
        ScreenY = press.ScreenY,
        ClientX = press.ClientX,
        ClientY = press.ClientY,
        OffsetX = press.OffsetX,
        OffsetY = press.OffsetY,
        PageX = press.PageX,
        PageY = press.PageY,
        Button = press.Button,
        Buttons = press.Buttons,
        AltKey = snapshot.AltKey,
        CtrlKey = snapshot.CtrlKey,
        MetaKey = snapshot.MetaKey,
        ShiftKey = snapshot.ShiftKey,
    };

    private static TouchEventArgs ToTouchArgs(ContextMenuPress press, EventSnapshot snapshot) => new()
    {
        Type = snapshot.Type,
        Detail = snapshot.Detail,
        Touches = ToTouchPoints(press.Touches),
        TargetTouches = ToTouchPoints(press.TargetTouches),
        ChangedTouches = ToTouchPoints(press.ChangedTouches),
        AltKey = snapshot.AltKey,
        CtrlKey = snapshot.CtrlKey,
        MetaKey = snapshot.MetaKey,
        ShiftKey = snapshot.ShiftKey,
    };

    private static TouchPoint[] ToTouchPoints(IReadOnlyList<ContextMenuTouch>? touches)
    {
        if (touches is null || touches.Count == 0)
        {
            return [];
        }

        TouchPoint[] points = new TouchPoint[touches.Count];
        for (int i = 0; i < points.Length; i++)
        {
            ContextMenuTouch touch = touches[i];
            points[i] = new TouchPoint
            {
                Identifier = touch.Identifier,
                ClientX = touch.ClientX,
                ClientY = touch.ClientY,
                ScreenX = touch.ScreenX,
                ScreenY = touch.ScreenY,
                PageX = touch.PageX,
                PageY = touch.PageY,
            };
        }

        return points;
    }

    private static async Task ReleaseAsync(TwinHandle<ContextMenuOptions>? released, ContextMenuTwin releasedTwin)
    {
        try
        {
            if (released is not null)
            {
                await released.DisposeAsync();
            }

            await releasedTwin.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit went away with the trigger.
        }
    }

    private ContextMenuOptions BuildOptions()
    {
        ContextMenuRootContext root = Root!;
        bool isDisabled = disabled!.Value;
        IReadOnlyList<string>? names = HandlerList(handlerMask);
        if (sentOptions is { } sent && sent.Disabled == isDisabled && ReferenceEquals(sent.Handlers, names)
            && string.Equals(sent.Token, root.Menu!.SessionToken, StringComparison.Ordinal) && string.Equals(sent.RootId, root.RootId, StringComparison.Ordinal))
        {
            return sent;
        }

        return new ContextMenuOptions
        {
            Part = ContextMenuTwin.TriggerPart,
            Token = root.Menu!.SessionToken,
            RootId = root.RootId,
            Disabled = isDisabled,
            Handlers = names,
        };
    }

    // WebAssembly only, synchronously inside the dispatch: the consumer's handler, and its veto as the verdict.
    private DecisionPolicy DecidePress(ContextMenuPress press) => RunConsumerHandler(press) ? DecisionPolicy.Stop : DecisionPolicy.Run;

    // Server: the event of a handler the module did not decide, ahead of the open it may veto.
    private void OnPressed(ContextMenuPress press) => _ = RunConsumerHandler(press);

    // Runs the consumer's handler for the press's event and records whether it vetoed the part's handling.
    private bool RunConsumerHandler(ContextMenuPress press)
    {
        EventSnapshot snapshot = press.Event ?? EventSnapshot.CreatePlaceholder();
        int index = HandlerIndex("on" + snapshot.Type);
        if (index < 0 || !handlers[index].IsHandler)
        {
            return false;
        }

        EventArgs args = index == 0 ? ToMouseArgs(press, snapshot) : ToTouchArgs(press, snapshot);
        try
        {
            Task handler = handlers[index].InvokeAsync(args);
            if (handler.IsFaulted)
            {
                _ = DispatchExceptionAsync(handler.Exception!.GetBaseException());
            }
            else if (!handler.IsCompleted)
            {
                _ = ObserveAsync(handler);
            }
        }
        catch (Exception exception)
        {
            _ = DispatchExceptionAsync(exception);
        }

        bool prevented = args.PartHandlerPrevented();
        vetoed[index] = prevented;
        return prevented;
    }

    // CTX-PTR-01, CTX-PTR-06, CTX-EVT-02, CTX-EVT-03: the open request has no trigger and never writes the open interaction
    // type (CTX-STA-09); a trigger-less request never dedupes (MenuStore.cs, CTX-STA-03, PTR-14).
    private void OnOpenRequested(ContextMenuOpenRequest request)
    {
        // The handler of the gesture's event: `contextmenu` for a right click, `touchstart` for a long press.
        int index = string.Equals(request.PointerType, "touch", StringComparison.Ordinal) ? 1 : 0;
        bool refused = vetoed[index];
        vetoed[index] = false;
        if (refused || disposed)
        {
            return;
        }

        EventSnapshot snapshot = request.Event ?? EventSnapshot.CreatePlaceholder();
        _ = RequestAsync(() => Root!.Menu!.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress, snapshot)));
    }

    // CTX-PTR-05, CTX-EVT-05.
    private void OnCancelOpenRequested(ContextMenuCancelOpen request)
    {
        if (!disposed)
        {
            EventSnapshot snapshot = request.Event ?? EventSnapshot.CreatePlaceholder();
            _ = RequestAsync(() => Root!.Menu!.CloseTreeAsync(EventReason.CancelOpen, snapshot));
        }
    }

    private async Task RequestAsync(Func<Task> request)
    {
        try
        {
            await InvokeAsync(request);
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private async Task ObserveAsync(Task handler)
    {
        try
        {
            await handler;
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }
}
