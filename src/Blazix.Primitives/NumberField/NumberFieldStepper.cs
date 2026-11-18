using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NUM §3.5/§3.7, NUM-STA-20, NUM-DOM-10/17, NUM-ARIA-05..08; number-field/root/useNumberFieldStepperButton.ts. The one
// stepper behaviour NumberFieldIncrement and NumberFieldDecrement compose (ADR 16 rule 10), over ButtonBehavior with
// UBT-ALG-08's NumberField row: Native = NativeButton, Disabled = effective disabled || read-only, FocusableWhenDisabled.
/// <summary>The behavior of a number field's stepper button: its effective disabled state, its attributes and its button mechanism.</summary>
internal sealed class NumberFieldStepper : IDisposable
{
    private const string UserSelectNone = "-webkit-user-select: none; user-select: none";

    private readonly bool increment;
    private readonly ButtonBehavior behavior;
    private readonly Func<Func<Task>, Task> invoke;
    private readonly Func<Exception, Task> dispatchException;
    private NumberFieldRootContext? context;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldStepper"/> class.</summary>
    /// <param name="increment"><see langword="true"/> for the increment button; <see langword="false"/> for the decrement button.</param>
    /// <param name="jsRuntime">The runtime the button twin imports its module through.</param>
    /// <param name="logger">The logger of the part.</param>
    /// <param name="developmentLog">The once-per-session set of the declared-mode messages.</param>
    /// <param name="idGenerator">The source of the late id of a component root.</param>
    /// <param name="stateChanged">Renders the part again.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    /// <param name="invoke">Runs a function on the renderer's dispatcher.</param>
    public NumberFieldStepper(bool increment, IJSRuntime jsRuntime, ILogger logger, ButtonDevelopmentLog developmentLog, PartIdGenerator idGenerator, Action stateChanged, Func<Exception, Task> dispatchException, EventCallback onKeyDown, EventCallback onKeyUp, Func<Func<Task>, Task> invoke)
    {
        this.increment = increment;
        this.invoke = invoke;
        this.dispatchException = dispatchException;

        // UBT-ALG-08's NumberField row; the repeater with the input focus of UBT-PTR-20 (decision 16 (a), chosen by the
        // user 2026-11-18).
        behavior = new ButtonBehavior(jsRuntime, logger, developmentLog, idGenerator, stateChanged, dispatchException, onKeyDown, onKeyUp)
        {
            FocusableWhenDisabled = true,
            PressAndHold = true,
        };
        behavior.HoldStarted += snapshot => Run(() => StartAsync(snapshot));
        behavior.HoldTicked += snapshot => Run(() => TickAsync(snapshot));
        behavior.HoldStopped += snapshot => Run(() => ReleaseAsync(snapshot));
        behavior.HoldClicked += snapshot => Run(() => ClickAsync(snapshot));
    }

    /// <summary>Gets the button mechanism.</summary>
    public ButtonBehavior Behavior => behavior;

    /// <summary>Gets a value indicating whether the button is disabled: its own parameter, the field's, or the value at the bound it steps toward.</summary>
    public bool EffectiveDisabled { get; private set; }

    /// <summary>Gets the reason of this button's steps.</summary>
    public EventReason Reason => increment ? EventReason.IncrementPress : EventReason.DecrementPress;

    /// <inheritdoc />
    public void Dispose() => behavior.Dispose();

    /// <summary>Takes the cascaded root context; called once, when the part initializes.</summary>
    /// <param name="rootContext">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException">The part is not placed within a <see cref="NumberFieldRoot"/>.</exception>
    public NumberFieldRootContext Initialize(NumberFieldRootContext? rootContext)
    {
        context = NumberFieldRootContext.Require(rootContext);
        return context;
    }

    // NUM-STA-20, NUM-API-23: disabled || rootDisabled || atBoundary; the mechanism adds read-only.
    /// <summary>Applies the part's parameters and the root's state; called on every parameter set and root notification.</summary>
    /// <param name="disabled">The part's own <c>Disabled</c>.</param>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void Update(bool disabled, bool nativeButton, IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        NumberFieldRoot root = context!.Root;
        NumberFieldRootState state = context.State;
        bool atBoundary = state.Value is double v && (increment ? v >= root.MaxWithDefault : v <= root.MinWithDefault);
        EffectiveDisabled = disabled || state.Disabled || atBoundary;
        behavior.Native = nativeButton;
        behavior.Disabled = EffectiveDisabled || state.ReadOnly;
        behavior.HoldFocusTarget = root.InputId;
        behavior.SetConsumerAttributes(additionalAttributes);
    }

    // NUM-DOM-10, NUM-ARIA-05..07.
    /// <summary>Adds the button's attributes to a render.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    /// <param name="consumerId">The part's id when the consumer supplied one.</param>
    public void AddAttributes(AttributeList attributes, string? consumerId)
    {
        behavior.AddAttributes(attributes, consumerId, composite: false);

        // The stepper's own props, which Base UI passes beside the mechanism's: out of the tab order, and the `disabled`
        // attribute of the effective disabled state even though the mechanism keeps it focusable (UBT-DOM-09).
        attributes.Set("tabindex", "-1");
        if (behavior.Native && EffectiveDisabled)
        {
            attributes.Set("disabled", true);
        }

        attributes.Set("aria-label", increment ? "Increase" : "Decrease");
        attributes.Set("aria-controls", context!.Root.InputId);
        attributes.Set("style", UserSelectNone);
    }

    /// <summary>Attaches the button mechanism after a render.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/>.</param>
    /// <param name="id">The part's id, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public Task AfterRenderAsync(bool enabled, ElementReference? element, string? id) => behavior.AfterRenderAsync(enabled, element, id, composite: false);

    // NUM-PTR-01..05; useNumberFieldStepperButton.ts:70-171. The `button` module runs the repeater (UBT-ALG-07, ADR 01
    // decision 4): it reports a sequence's start and its ticks, the release and the single-action clicks, and focuses the
    // input on a mouse press; C# applies each and stops the repeat when a tick applied nothing.

    // The pointerdown of a sequence (useNumberFieldStepperButton.ts:158-170): the dirty text is synced first, and the last
    // changed value becomes the hold's own result slot.
    private async Task StartAsync(EventSnapshot snapshot)
    {
        if (behavior.Disabled || !string.Equals(snapshot.Type, "pointerdown", StringComparison.Ordinal))
        {
            return;
        }

        await CommitInputValueAsync(snapshot).ConfigureAwait(true);
        context!.Root.LastChangedValue = null;
    }

    private async Task TickAsync(EventSnapshot snapshot)
    {
        // A report the module sent before it learned the button is disabled or read-only (Server) applies nothing.
        if (behavior.Disabled)
        {
            await behavior.StopHoldAsync().ConfigureAwait(true);
            return;
        }

        NumberFieldRoot root = context!.Root;
        double amount = root.GetStepAmount(snapshot);
        if (!await root.IncrementValueAsync(amount, increment ? 1 : -1, null, snapshot, Reason).ConfigureAwait(true))
        {
            await behavior.StopHoldAsync().ConfigureAwait(true);
        }
    }

    // NUM-PTR-04: the release commits once, the hold's result or the current value.
    private Task ReleaseAsync(EventSnapshot snapshot)
    {
        NumberFieldRoot root = context!.Root;
        return root.CommitAsync(root.LastChangedValue ?? root.ValueRef, Reason, snapshot);
    }

    // NUM-PTR-01 (useNumberFieldStepperButton.ts:130-151): a keyboard click or a tap steps once and commits a change.
    private async Task ClickAsync(EventSnapshot snapshot)
    {
        if (behavior.Disabled)
        {
            return;
        }

        NumberFieldRoot root = context!.Root;
        await CommitInputValueAsync(snapshot).ConfigureAwait(true);
        double amount = root.GetStepAmount(snapshot);
        double? previous = root.ValueRef;
        await root.IncrementValueAsync(amount, increment ? 1 : -1, null, snapshot, Reason).ConfigureAwait(true);
        double? committed = root.LastChangedValue ?? root.ValueRef;
        if (!Store.Equality.StrictEquals(committed, previous))
        {
            await root.CommitAsync(committed, Reason, snapshot).ConfigureAwait(true);
        }
    }

    // NUM-PTR-05 (commitValue, useNumberFieldStepperButton.ts:70-95): dirty text is parsed and applied without a
    // direction, so it is not snapped, before the step; synced text steps from the numeric value.
    private async Task CommitInputValueAsync(EventSnapshot snapshot)
    {
        NumberFieldRoot root = context!.Root;
        bool shouldCommitInputValue = !root.AllowInputSync;
        root.AllowInputSync = true;
        if (!shouldCommitInputValue)
        {
            root.LastChangedValue = root.ValueRef;
            return;
        }

        double? parsed = Utilities.NumberParser.Parse(root.InputValue, root.Culture, root.Format);
        if (parsed is not null)
        {
            NumberFieldChangeEventDetails details = new(Reason, null, snapshot);
            await root.SetValueAsync(parsed, details).ConfigureAwait(true);
            if (!details.IsCanceled)
            {
                root.ValueRef = parsed;
            }
        }
    }

    private void Run(Func<Task> work)
    {
        _ = RunAsync(work);
    }

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await invoke(work).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await dispatchException(exception).ConfigureAwait(true);
        }
    }
}
