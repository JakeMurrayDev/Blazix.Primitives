using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The button mechanism as a button-like part uses it (button-behavior.md, UBT; ADR 04 §1 button row, §3 decision point
// `activate`, fallback run); lifted from Button (M1 track 4) by CollapsibleTrigger (M1 track 6), which its hand-over
// proposed, so Button, CollapsibleTrigger and later Toggle share one copy.
//
// Base UI merges the consumer's props *before* the mechanism (`[elementProps, getButtonProps]`), so the mechanism gates
// the consumer's click, mousedown, pointerdown, keydown and keyup handlers (UBT-EVT-01/03). The pipeline merges the
// consumer after the part, so the part declines those handlers (ForwardsConsumerHandler, ADR 02 Amendments 2026-10-21)
// and the pipeline leaves them off the element:
// - while disabled, all five, so Blazor has nothing to invoke (the module default-prevents click and pointerdown);
// - always, keydown and keyup, which the part runs itself from its own handler. On WebAssembly the module's activation
//   decision point runs the consumer's key handler synchronously inside the dispatch (a `PreventPartHandler()` there
//   answers `stop`), and Blazor then dispatches the same event; the part handler skips that one dispatch so the
//   consumer's handler runs once. On Server the decision falls back to `run` (ADR 01 relaxation table) and the part
//   handler always runs the consumer's.
// Every other handler is forwarded unchanged (UBT-PTR-04), and a substitute element's own handlers are not gated.
// The hidden-input bridge (UBT-EVT-09; ADR 12 Amendments 2026-10-22 item 4, M2 track 4) is shared here by Switch, Checkbox
// and Radio, which turn HiddenInput on: the module forwards root clicks to the input the part renders after its element,
// and each toggle reaches the part's ToggleRequest, inside the `toggle` decision on WebAssembly (a refusal reverts the box
// natively) or from the module's report otherwise (a refusal raises RestoreRequested, so the part restores the input
// through the `dom` primitive). The consumer's onclick must run before the toggle (SWT-PTR-09), so with the bridge on it
// is declined too: the `activate` decision runs it on WebAssembly with a one-shot marker, and the part's own onclick
// handler runs it otherwise (on Server after the toggle, the order of the ADR 01 row "EVD-EVT-26 against composite/button
// key handling").
// The attributes of UBT-ALG-03 are ButtonAttributeWriter's. Attach: by Element; by the generated id under a substitute
// callback; by a late id drawn after the first render when a component root leaves no Element (ADR 02 decision 1).
/// <summary>The button mechanism of one button-like part: its consumer handler gate, its attributes and its <see cref="ButtonTwin"/>.</summary>
internal sealed class ButtonBehavior : IDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly ILogger logger;
    private readonly ButtonDevelopmentLog developmentLog;
    private readonly PartIdGenerator idGenerator;
    private readonly Action stateChanged;
    private readonly Func<Exception, Task> dispatchException;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AttributeValue consumerKeyDown;
    private AttributeValue consumerKeyUp;
    private AttributeValue consumerClick;
    private DecidedClick? decidedClick;
    private DecidedKey? decidedKeyDown;
    private DecidedKey? decidedKeyUp;
    private ButtonTwin? twin;
    private TwinHandle<ButtonOptions>? handle;
    private ButtonOptions? sentOptions;
    private string? lateId;

    /// <summary>Initializes a new instance of the <see cref="ButtonBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="logger">The logger of the part, which receives the declared-mode messages.</param>
    /// <param name="developmentLog">The once-per-session set of the declared-mode messages.</param>
    /// <param name="idGenerator">The source of the late id of a component root.</param>
    /// <param name="stateChanged">Renders the part again; called to render a late id.</param>
    /// <param name="dispatchException">Routes an exception of a consumer handler that ran in a decision to the part.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler, which calls <see cref="HandleKeyUpAsync"/>.</param>
    /// <param name="onClick">
    /// The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>; needed only with
    /// <see cref="HiddenInput"/>.
    /// </param>
    public ButtonBehavior(
        IJSRuntime jsRuntime,
        ILogger logger,
        ButtonDevelopmentLog developmentLog,
        PartIdGenerator idGenerator,
        Action stateChanged,
        Func<Exception, Task> dispatchException,
        EventCallback onKeyDown,
        EventCallback onKeyUp,
        EventCallback onClick = default)
    {
        this.jsRuntime = jsRuntime;
        this.logger = logger;
        this.developmentLog = developmentLog;
        this.idGenerator = idGenerator;
        this.stateChanged = stateChanged;
        this.dispatchException = dispatchException;
        this.onKeyDown = onKeyDown;
        this.onKeyUp = onKeyUp;
        this.onClick = onClick;
    }

    /// <summary>Gets or sets a value indicating whether the element ignores user interaction.</summary>
    /// <value>The part's effective disabled state.</value>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the element is declared a native <c>button</c>.</summary>
    /// <value>The part's <c>NativeButton</c> parameter.</value>
    public bool Native { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a disabled element stays focusable.</summary>
    /// <value><see langword="null"/> when the part does not specify it.</value>
    public bool? FocusableWhenDisabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the element's clicks are forwarded to the hidden input the part renders after it.</summary>
    /// <value><see langword="true"/> for Switch, Checkbox and Radio; the default is <see langword="false"/>.</value>
    public bool HiddenInput { get; set; }

    /// <summary>Gets or sets a value indicating whether the bridged input refuses user toggles.</summary>
    /// <value>The part's <c>ReadOnly</c> parameter.</value>
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets a value indicating whether an <c>Enter</c> keydown is prevented and never activates the element.</summary>
    /// <value><see langword="true"/> for Checkbox and Radio; the default is <see langword="false"/>.</value>
    public bool SuppressEnter { get; set; }

    /// <summary>Gets or sets the part's request for a toggle of its hidden input.</summary>
    /// <value>
    /// A function that receives the input's new <c>checked</c> value, or <see langword="null"/> when the toggle is decided
    /// inside the click and the part infers it, and the click's snapshot; its task's result is <see langword="true"/> when
    /// the part's value afterwards equals the input's new value. <see langword="null"/> ignores toggles.
    /// </value>
    public Func<bool?, EventSnapshot, Task<bool>>? ToggleRequest { get; set; }

    /// <summary>Gets or sets the action that restores the hidden input after a toggle the part refused once the browser had applied it.</summary>
    /// <value>The part's restore, which raises the <c>dom</c> primitive's restore count; or <see langword="null"/>.</value>
    public Action? RestoreRequested { get; set; }

    /// <summary>Gets the id the behavior drew for an element that a component root rendered.</summary>
    /// <value>The late id, or <see langword="null"/> while none was needed.</value>
    public string? LateId => lateId;

    /// <summary>Gets a task that completes when the behavior is attached to the element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        if (twin is not null)
        {
            twin.NativeButtonMismatched -= ReportNativeButtonMismatch;
            twin.Toggled -= OnToggled;
            twin.ActivationDecider = null;
            twin.ToggleDecider = null;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }
    }

    /// <summary>Finds the consumer's key handlers, which the part declines and runs itself.</summary>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void SetConsumerAttributes(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        consumerKeyDown = default;
        consumerKeyUp = default;
        consumerClick = default;
        if (additionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in additionalAttributes)
        {
            bool keyDown = IsKeyDown(pair.Key);
            bool click = IsClick(pair.Key);
            if (!keyDown && !click && !IsKeyUp(pair.Key))
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (!MergeProps.IsHandler(pair.Key, value))
            {
                continue;
            }

            if (click)
            {
                consumerClick = value;
            }
            else if (keyDown)
            {
                consumerKeyDown = value;
            }
            else
            {
                consumerKeyUp = value;
            }
        }
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns>
    /// <see langword="false"/> for the key handlers, for <c>onclick</c> with <see cref="HiddenInput"/>, and for the press
    /// handlers while disabled.
    /// </returns>
    public bool ForwardsConsumerHandler(string eventName)
    {
        return !(IsKeyDown(eventName) || IsKeyUp(eventName) || (HiddenInput && IsClick(eventName)) || (Disabled && IsGated(eventName)));
    }

    /// <summary>Adds the button attributes and the part's key handlers to a render.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    /// <param name="consumerId">The part's id when the consumer supplied one or a generated one was drawn.</param>
    /// <param name="composite"><see langword="true"/> for an item of a composite widget.</param>
    public void AddAttributes(AttributeList attributes, string? consumerId, bool composite)
    {
        if (lateId is not null && consumerId is null)
        {
            attributes.Set("id", lateId);
        }

        ButtonAttributeWriter.Write(attributes, Native, Disabled, FocusableWhenDisabled, composite);

        if (!Disabled)
        {
            if (consumerKeyDown.IsHandler)
            {
                attributes.Set("onkeydown", onKeyDown);
            }

            if (consumerKeyUp.IsHandler)
            {
                attributes.Set("onkeyup", onKeyUp);
            }

            if (HiddenInput && consumerClick.IsHandler)
            {
                attributes.Set("onclick", onClick);
            }
        }
    }

    /// <summary>Attaches the twin after the first render that showed the element, then sends options that changed.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback or component root.</param>
    /// <param name="id">The part's id (the consumer's or the generated one), or <see langword="null"/>.</param>
    /// <param name="composite"><see langword="true"/> for an item of a composite widget.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(bool enabled, ElementReference? element, string? id, bool composite)
    {
        if (!enabled)
        {
            // Nothing is rendered: the element the module listened on is gone.
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                await released.DisposeAsync();
            }

            return;
        }

        if (handle is not null)
        {
            ButtonOptions? next = NextOptions(composite);
            if (next is not null)
            {
                sentOptions = next;
                await handle.UpdateAsync(next);
            }

            return;
        }

        string? elementId = null;
        if (element is null)
        {
            elementId = id ?? lateId;
            if (elementId is null)
            {
                // A substitute whose root is a component leaves no element reference: render an id, attach next time.
                lateId = idGenerator.Next();
                stateChanged();
                return;
            }
        }

        twin ??= CreateTwin();
        ButtonOptions options = Options(composite);
        sentOptions = options;
        handle = elementId is null
            ? await twin.AttachAsync(element!.Value, options)
            : await twin.AttachAsync(elementId, options);
        attachedSource.TrySetResult();
    }

    /// <summary>Runs the consumer's <c>onkeydown</c> handler from the part's own, unless a decision already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs e) => RunConsumerKeyHandlerAsync(e, consumerKeyDown, ref decidedKeyDown);

    /// <summary>Runs the consumer's <c>onclick</c> handler from the part's own, unless a decision already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleClickAsync(MouseEventArgs e)
    {
        DecidedClick? pending = decidedClick;
        decidedClick = null;
        if (!consumerClick.IsHandler || Disabled || (pending is { } click && click == DecidedClick.From(e)))
        {
            return Task.CompletedTask;
        }

        return consumerClick.InvokeAsync(e);
    }

    /// <summary>Runs the consumer's <c>onkeyup</c> handler from the part's own, unless a decision already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs e) => RunConsumerKeyHandlerAsync(e, consumerKeyUp, ref decidedKeyUp);

    private static bool IsGated(string name)
    {
        return string.Equals(name, "onclick", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "onmousedown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "onpointerdown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClick(string name) => string.Equals(name, "onclick", StringComparison.OrdinalIgnoreCase);

    private static bool IsKeyDown(string name) => string.Equals(name, "onkeydown", StringComparison.OrdinalIgnoreCase);

    private static bool IsKeyUp(string name) => string.Equals(name, "onkeyup", StringComparison.OrdinalIgnoreCase);

    private ButtonTwin CreateTwin()
    {
        ButtonTwin created = new(jsRuntime) { ActivationDecider = Decide, ToggleDecider = DecideToggle };
        created.NativeButtonMismatched += ReportNativeButtonMismatch;
        created.Toggled += OnToggled;
        return created;
    }

    private ButtonOptions Options(bool composite)
    {
        return new ButtonOptions
        {
            Disabled = Disabled,
            FocusableWhenDisabled = FocusableWhenDisabled,
            Native = Native,
            Composite = composite,
            HiddenInput = HiddenInput,
            ReadOnly = ReadOnly,
            SuppressEnter = SuppressEnter,
        };
    }

    // Compares before constructing, so an unchanged re-render allocates no options record.
    private ButtonOptions? NextOptions(bool composite)
    {
        if (sentOptions is { } sent && sent.Disabled == Disabled && sent.FocusableWhenDisabled == FocusableWhenDisabled && sent.Native == Native && sent.Composite == composite
            && sent.HiddenInput == HiddenInput && sent.ReadOnly == ReadOnly && sent.SuppressEnter == SuppressEnter)
        {
            return null;
        }

        return Options(composite);
    }

    private void ReportNativeButtonMismatch(bool expectedNative) => developmentLog.ReportNativeButtonMismatch(logger, expectedNative);

    // The activation decision point, on WebAssembly only (ButtonTwin.ActivationDecider): the consumer's key handler
    // runs here, inside the dispatch, so its PreventPartHandler() can stop the activation (UBT-KEY-03/04/06).
    private DecisionPolicy Decide(EventSnapshot snapshot)
    {
        if (string.Equals(snapshot.Type, "click", StringComparison.Ordinal))
        {
            return DecideClick(snapshot);
        }

        bool keyUp = string.Equals(snapshot.Type, "keyup", StringComparison.Ordinal);
        AttributeValue consumer = keyUp ? consumerKeyUp : consumerKeyDown;
        if (!consumer.IsHandler || Disabled)
        {
            return DecisionPolicy.Run;
        }

        KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();
        DecidedKey decided = DecidedKey.From(args);
        if (keyUp)
        {
            decidedKeyUp = decided;
        }
        else
        {
            decidedKeyDown = decided;
        }

        try
        {
            // Only the synchronous part of the handler can veto; an asynchronous rest runs on (ADR 04 §3).
            Task task = consumer.InvokeAsync(args);
            if (!task.IsCompleted)
            {
                _ = ObserveAsync(task);
            }
            else if (task.IsFaulted)
            {
                _ = dispatchException(task.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
        }

        return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // The bridge's root click on WebAssembly: the consumer's declined onclick runs here, before the module forwards the
    // click to the input, and a PreventPartHandler() there stops the forward (SWT-PTR-09).
    private DecisionPolicy DecideClick(EventSnapshot snapshot)
    {
        if (!HiddenInput || !consumerClick.IsHandler || Disabled)
        {
            return DecisionPolicy.Run;
        }

        MouseEventArgs args = new()
        {
            Type = snapshot.Type,
            Detail = snapshot.Detail,
            Buttons = snapshot.Buttons,
            AltKey = snapshot.AltKey,
            CtrlKey = snapshot.CtrlKey,
            MetaKey = snapshot.MetaKey,
            ShiftKey = snapshot.ShiftKey,
        };
        decidedClick = DecidedClick.From(args);
        try
        {
            RunSynchronousPart(consumerClick.InvokeAsync(args));
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
        }

        return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // The `toggle` decision, on WebAssembly only: the part's request runs inside the input's click. Only a request that
    // completes synchronously can refuse the toggle (ADR 04 §3); one that does not keeps it and restores the input later.
    private DecisionPolicy DecideToggle(EventSnapshot snapshot)
    {
        if (ToggleRequest is not { } request)
        {
            return DecisionPolicy.Run;
        }

        Task<bool> task;
        try
        {
            task = request(null, snapshot);
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
            return DecisionPolicy.Run;
        }

        if (task.IsCompletedSuccessfully)
        {
            return task.Result ? DecisionPolicy.Run : DecisionPolicy.Stop;
        }

        _ = CompleteToggleAsync(task);
        return DecisionPolicy.Run;
    }

    // The module's report of a toggle the browser already applied (Server, and WebAssembly without a decider).
    private void OnToggled(ButtonToggle toggle)
    {
        if (ToggleRequest is not { } request)
        {
            return;
        }

        try
        {
            _ = CompleteToggleAsync(request(toggle.Checked, toggle.Event));
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
        }
    }

    private async Task CompleteToggleAsync(Task<bool> task)
    {
        try
        {
            if (!await task)
            {
                RestoreRequested?.Invoke();
            }
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }

    private void RunSynchronousPart(Task task)
    {
        if (!task.IsCompleted)
        {
            _ = ObserveAsync(task);
        }
        else if (task.IsFaulted)
        {
            _ = dispatchException(task.Exception!.GetBaseException());
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }

    private Task RunConsumerKeyHandlerAsync(KeyboardEventArgs e, AttributeValue consumer, ref DecidedKey? decided)
    {
        // The decider already ran the consumer's handler for the event Blazor dispatches now.
        DecidedKey? pending = decided;
        decided = null;
        if (!consumer.IsHandler || Disabled || (pending is { } key && key == DecidedKey.From(e)))
        {
            return Task.CompletedTask;
        }

        return consumer.InvokeAsync(e);
    }

    /// <summary>The click a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedClick(long Detail, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedClick From(MouseEventArgs e) => new(e.Detail, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }

    /// <summary>The key event a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedKey(string Key, string Code, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedKey From(KeyboardEventArgs e) => new(e.Key, e.Code, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}
