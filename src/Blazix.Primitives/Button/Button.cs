using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// BTN-STA-01; button/Button.tsx:44-49. ADR 08 §3: the state record is sealed, partial and [PartState].
/// <summary>Represents the state of a button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ButtonState(bool Disabled);

// BTN-DOM-01; button/ButtonDataAttributes.ts.
/// <summary>Provides the names of the data attributes a button renders.</summary>
public static class ButtonDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Button (button/Button.tsx); the contract is docs/components/button.md, the mechanism
// docs/architecture/button-behavior.md (UBT). The attributes of UBT-ALG-03 are written here through
// ButtonAttributeWriter; keyboard activation, press suppression and the declared-mode check are the `button` module's,
// reached through one ButtonTwin per part (ADR 04 §1 and §3).
//
// Base UI merges the consumer's props *before* the mechanism (`[elementProps, getButtonProps]`, BTN-DOM-05), so the
// mechanism gates the consumer's click, mousedown, pointerdown, keydown and keyup handlers (UBT-EVT-01/03). The
// pipeline merges the consumer after the part, so the part declines those handlers (ForwardsConsumerHandler, ADR 02
// Amendments 2026-10-21) and the pipeline leaves them off the element:
// - while disabled, all five, so Blazor has nothing to invoke (the module default-prevents click and pointerdown);
// - always, keydown and keyup, which the part runs itself from its own handler. On WebAssembly the module's activation
//   decision point runs the consumer's key handler synchronously inside the dispatch (ADR 04 §3; a
//   `PreventPartHandler()` there answers `stop`), and Blazor then dispatches the same event; the part handler skips
//   that one dispatch so the consumer's handler runs once. On Server the decision falls back to `run` (ADR 01
//   relaxation table, BTN-KEY-01) and the part handler always runs the consumer's.
// Every other handler is forwarded unchanged (UBT-PTR-04), and a substitute element's own handlers are not gated
// (RND §2.1 row 5).
/// <summary>
/// Represents a button that runs an action: Base UI's <c>Button</c>, rendered as a <c>button</c>. It can be replaced by
/// another element through <see cref="PartBase{TState}.Render"/> while keeping button semantics, and can stay focusable
/// while disabled.
/// </summary>
public sealed class Button : PartBase<ButtonState>
{
    private const string DefaultTag = "button";

    private static readonly ButtonState s_enabled = new(Disabled: false);
    private static readonly ButtonState s_disabled = new(Disabled: true);

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private AttributeValue consumerKeyDown;
    private AttributeValue consumerKeyUp;
    private DecidedKey? decidedKeyDown;
    private DecidedKey? decidedKeyUp;
    private ButtonTwin? twin;
    private TwinHandle<ButtonOptions>? handle;
    private ButtonOptions? sentOptions;
    private string? lateId;
    private TaskCompletionSource? attachedSource;
    private bool attached;

    /// <summary>Initializes a new instance of the <see cref="Button"/> class.</summary>
    public Button()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", HandleKeyUpAsync);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Button> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // BTN-API-01
    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks, presses and keyboard activation, which also withholds the consumer's
    /// <c>onclick</c>, <c>onmousedown</c>, <c>onpointerdown</c>, <c>onkeydown</c> and <c>onkeyup</c> handlers; otherwise,
    /// <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // BTN-API-02
    /// <summary>Gets or sets a value indicating whether the button stays focusable and in the tab order while disabled.</summary>
    /// <value>
    /// <see langword="true"/> to keep a disabled button focusable, announced with <c>aria-disabled</c> instead of the
    /// <c>disabled</c> attribute; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool FocusableWhenDisabled { get; set; }

    // BTN-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ButtonState State => Disabled ? s_disabled : s_enabled;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the twin is attached, so a browser test
    // can wait for the module's listeners before it presses keys.
    /// <summary>Gets a task that completes when the button's behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    internal Task TwinAttached => attached ? Task.CompletedTask : (attachedSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        FindConsumerKeyHandlers();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Enabled)
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
            ButtonOptions? next = NextOptions();
            if (next is not null)
            {
                sentOptions = next;
                await handle.UpdateAsync(next);
            }

            return;
        }

        string? elementId = null;
        if (Element is null)
        {
            elementId = Id ?? lateId;
            if (elementId is null)
            {
                // A substitute whose root is a component leaves no element reference: render an id, attach next time.
                lateId = IdGenerator.Next();
                StateHasChanged();
                return;
            }
        }

        twin ??= CreateTwin();
        ButtonOptions options = Options();
        sentOptions = options;
        handle = elementId is null
            ? await twin.AttachAsync(Element!.Value, options)
            : await twin.AttachAsync(elementId, options);
        attached = true;
        attachedSource?.TrySetResult();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && twin is not null)
        {
            twin.NativeButtonMismatched -= ReportNativeButtonMismatch;
            twin.ActivationDecider = null;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (lateId is not null && Id is null)
        {
            attributes.Set("id", lateId);
        }

        ButtonAttributeWriter.Write(attributes, NativeButton, Disabled, FocusableWhenDisabled, composite: false);

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

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return !(IsKeyDown(eventName) || IsKeyUp(eventName) || (Disabled && IsGated(eventName)));
    }

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
        ButtonTwin created = new(JSRuntime) { ActivationDecider = Decide };
        created.NativeButtonMismatched += ReportNativeButtonMismatch;
        return created;
    }

    private ButtonOptions Options()
    {
        // BTN-API-02: focusableWhenDisabled always explicit; BTN-EDGE-02: composite inference is M2's composite module.
        return new ButtonOptions
        {
            Disabled = Disabled,
            FocusableWhenDisabled = FocusableWhenDisabled,
            Native = NativeButton,
            Composite = false,
        };
    }

    // Compares before constructing, so an unchanged re-render allocates no options record.
    private ButtonOptions? NextOptions()
    {
        if (sentOptions is { } sent && sent.Disabled == Disabled && sent.FocusableWhenDisabled == FocusableWhenDisabled && sent.Native == NativeButton)
        {
            return null;
        }

        return Options();
    }

    private void ReportNativeButtonMismatch(bool expectedNative) => DevelopmentLog.ReportNativeButtonMismatch(Logger, expectedNative);

    // The consumer's key handlers, which the part declines and runs itself (see the comment on the class).
    private void FindConsumerKeyHandlers()
    {
        consumerKeyDown = default;
        consumerKeyUp = default;
        if (AdditionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in AdditionalAttributes)
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
                _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = DispatchExceptionAsync(exception);
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
            await DispatchExceptionAsync(exception);
        }
    }

    private Task HandleKeyDownAsync(KeyboardEventArgs e) => RunConsumerKeyHandlerAsync(e, consumerKeyDown, ref decidedKeyDown);

    private Task HandleKeyUpAsync(KeyboardEventArgs e) => RunConsumerKeyHandlerAsync(e, consumerKeyUp, ref decidedKeyUp);

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
