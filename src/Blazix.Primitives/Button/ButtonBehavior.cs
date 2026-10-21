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
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AttributeValue consumerKeyDown;
    private AttributeValue consumerKeyUp;
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
    public ButtonBehavior(
        IJSRuntime jsRuntime,
        ILogger logger,
        ButtonDevelopmentLog developmentLog,
        PartIdGenerator idGenerator,
        Action stateChanged,
        Func<Exception, Task> dispatchException,
        EventCallback onKeyDown,
        EventCallback onKeyUp)
    {
        this.jsRuntime = jsRuntime;
        this.logger = logger;
        this.developmentLog = developmentLog;
        this.idGenerator = idGenerator;
        this.stateChanged = stateChanged;
        this.dispatchException = dispatchException;
        this.onKeyDown = onKeyDown;
        this.onKeyUp = onKeyUp;
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
            twin.ActivationDecider = null;
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
        if (additionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in additionalAttributes)
        {
            bool keyDown = IsKeyDown(pair.Key);
            if (!keyDown && !IsKeyUp(pair.Key))
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (!MergeProps.IsHandler(pair.Key, value))
            {
                continue;
            }

            if (keyDown)
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
    /// <returns><see langword="false"/> for the key handlers, and for the press handlers while disabled.</returns>
    public bool ForwardsConsumerHandler(string eventName)
    {
        return !(IsKeyDown(eventName) || IsKeyUp(eventName) || (Disabled && IsGated(eventName)));
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

    private static bool IsKeyDown(string name) => string.Equals(name, "onkeydown", StringComparison.OrdinalIgnoreCase);

    private static bool IsKeyUp(string name) => string.Equals(name, "onkeyup", StringComparison.OrdinalIgnoreCase);

    private ButtonTwin CreateTwin()
    {
        ButtonTwin created = new(jsRuntime) { ActivationDecider = Decide };
        created.NativeButtonMismatched += ReportNativeButtonMismatch;
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
        };
    }

    // Compares before constructing, so an unchanged re-render allocates no options record.
    private ButtonOptions? NextOptions(bool composite)
    {
        if (sentOptions is { } sent && sent.Disabled == Disabled && sent.FocusableWhenDisabled == FocusableWhenDisabled && sent.Native == Native && sent.Composite == composite)
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

    /// <summary>The key event a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedKey(string Key, string Code, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedKey From(KeyboardEventArgs e) => new(e.Key, e.Code, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}
