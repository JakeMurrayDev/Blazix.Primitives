using Blazix.Primitives.Interop;

namespace Blazix.Primitives;

// FFI §2 (validation service), FFI-ALG-01 (commit), FFI-ALG-02 (native state read; the browser's half is the form module's
// and arrives in each input's State), FFI-ALG-03 (change), FFI-ALG-04 (representative input), FFI-ALG-14 (publish),
// FFI-FRM-01..16; field/root/useFieldValidation.ts. The commit runs synchronously up to the first await of a validator
// that did not complete synchronously, so a submission reads every field's verdict right after calling it (FFI-FRM-04);
// a ValueTask that completed synchronously is Base UI's synchronous result, one that did not is the thenable
// (FFI-FRM-11). The debounce is a TimeProvider timer (ADR 01 decision 4), cancelled on unregistration (FFI-FRM-15).
/// <summary>The validation service of one field: commits, the change path with its debounce, and the field's own custom message.</summary>
internal sealed class FieldValidation : IDisposable
{
    private static readonly string[] s_none = [];

    private readonly FieldRootContext field;
    private readonly List<FieldValidationInput> registeredInputs = [];
    private int commitSequence;
    private ITimer? debounce;
    private FieldValidationInput? ownedInput;

    /// <summary>Initializes a new instance of the <see cref="FieldValidation"/> class.</summary>
    /// <param name="field">The field the service validates.</param>
    public FieldValidation(FieldRootContext field)
    {
        this.field = field;
    }

    /// <summary>Gets or sets the validation input of a control with one native input.</summary>
    /// <value>The input, or <see langword="null"/>; used when no input is registered.</value>
    public FieldValidationInput? InputRef { get; set; }

    /// <summary>Gets the validation inputs a group registered, in registration order.</summary>
    /// <value>The inputs.</value>
    public IReadOnlyList<FieldValidationInput> RegisteredInputs => registeredInputs;

    /// <inheritdoc />
    /// <remarks>Cancels a pending debounced commit.</remarks>
    public void Dispose() => CancelDebounce();

    // FFI §6.1 `registerInput`: a group's members (tracks 6, 8).
    /// <summary>Registers a validation input of a group.</summary>
    /// <param name="input">The input.</param>
    public void RegisterInput(FieldValidationInput input)
    {
        if (!registeredInputs.Contains(input))
        {
            registeredInputs.Add(input);
        }
    }

    /// <summary>Removes a validation input added with <see cref="RegisterInput"/>.</summary>
    /// <param name="input">The input.</param>
    public void UnregisterInput(FieldValidationInput input) => registeredInputs.Remove(input);

    // FFI-ALG-03; useFieldValidation.ts:349-365.
    /// <summary>Reacts to a change of the control's value: commits, schedules a debounced commit, or runs the revalidate pass.</summary>
    /// <param name="value">The new value.</param>
    /// <param name="cancelPending"><see langword="true"/> to only cancel pending work, as unregistration and replacement do.</param>
    public void Change(object? value, bool cancelPending = false)
    {
        CancelDebounce();
        commitSequence++;
        if (cancelPending)
        {
            return;
        }

        bool onChange = field.ShouldValidateOnChange();
        double delay = field.DebounceMilliseconds;
        if (onChange && !IsEmptyString(value) && delay > 0)
        {
            DebouncedCommit scheduled = new(this, value);
            debounce = field.TimeProvider.CreateTimer(
                static state => ((DebouncedCommit)state!).Fire(),
                scheduled,
                TimeSpan.FromMilliseconds(delay),
                Timeout.InfiniteTimeSpan);
            scheduled.Timer = debounce;
            return;
        }

        field.Observe(CommitAsync(value, revalidate: !onChange));
    }

    // FFI-ALG-01; useFieldValidation.ts:123-347.
    /// <summary>Commits a value: reads the native state, runs the validator when it should, and publishes the result.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="revalidate"><see langword="true"/> for the revalidate pass of a change outside change validation.</param>
    /// <returns>
    /// A task that completes when the commit finished. Everything up to an asynchronous validator's first await has run
    /// when the method returns. The task faults with the validator's exception, in which case the published state is kept.
    /// </returns>
    public Task CommitAsync(object? value, bool revalidate = false)
    {
        try
        {
            return Commit(value, revalidate);
        }
        catch (Exception exception)
        {
            // A validator that throws keeps the published state; the field's own message is already withdrawn.
            FlushInputs();
            return Task.FromException(exception);
        }
    }

    // FFI-ALG-04 `representative()`, with the field's own message counted as an error when picking.
    /// <summary>Picks the input whose native state represents the field.</summary>
    /// <returns>The first eligible invalid registered input, else the first eligible one; <see cref="InputRef"/> without registered inputs.</returns>
    public FieldValidationInput? Representative()
    {
        if (registeredInputs.Count == 0)
        {
            return InputRef;
        }

        FieldValidationInput? fallback = null;
        foreach (FieldValidationInput input in registeredInputs)
        {
            if (!input.State.Eligible)
            {
                continue;
            }

            if (!input.IsCurrentlyValid)
            {
                return input;
            }

            fallback ??= input;
        }

        return fallback;
    }

    private static bool IsEmptyString(object? value) => value is string { Length: 0 };

    private static FieldValidityFlags MakeState(bool customError) => new(false, customError, false, false, false, false, false, false, false, !customError, false);

    private static FieldValidityFlags Flags(NativeValidity state) => new(
        state.BadInput,
        state.CustomError,
        state.PatternMismatch,
        state.RangeOverflow,
        state.RangeUnderflow,
        state.StepMismatch,
        state.TooLong,
        state.TooShort,
        state.TypeMismatch,
        state.Valid,
        state.ValueMissing);

    private static bool HasOtherNativeError(NativeValidity state) =>
        state.BadInput || state.PatternMismatch || state.RangeOverflow || state.RangeUnderflow || state.StepMismatch || state.TooLong || state.TooShort || state.TypeMismatch;

    private static string[] NativeMessages(FieldValidationInput? element) =>
        element is not null && element.State.ValidationMessage.Length > 0 ? [element.State.ValidationMessage] : s_none;

    private Task Commit(object? value, bool revalidate)
    {
        int sequence = ++commitSequence;
        FieldValidationInput? element = Representative();

        if (revalidate)
        {
            // FFI-FRM-05; useFieldValidation.ts:244-276.
            if (field.State.Valid != false || element is null)
            {
                return Task.CompletedTask;
            }

            if (!element.State.ValueMissing)
            {
                ClearOwnCustomValidity();
                FieldValidationInput? current = Representative();
                string[] foreign = current is not null && current.State.CustomError ? NativeMessages(current) : s_none;
                Publish(value, MakeState(foreign.Length > 0), foreign, externalInvalid: false);
                return Task.CompletedTask;
            }

            if (HasOtherNativeError(element.State))
            {
                return Task.CompletedTask;
            }
        }

        CancelDebounce();
        ClearOwnCustomValidity();
        FieldValidityFlags next = RefreshState(ref element);
        string[] messages = NativeMessages(element);
        if (messages.Length > 0 && !field.ShouldValidateOnChange())
        {
            // FFI-FRM-07: a native failure short-circuits the validator outside change validation.
            Publish(value, next, messages);
            return Task.CompletedTask;
        }

        FormValues values = field.Form.Registry.BuildValues();
        ValueTask<FieldValidationResult?> pending = field.Validator(new FieldValidationArguments(value, values));
        if (!pending.IsCompleted)
        {
            // FFI-FRM-11: neutral while the validator runs, keeping what must block a submission synchronously.
            if (next.Valid == false)
            {
                Publish(value, next, messages);
            }
            else if (field.ValidationMode == ValidationMode.OnSubmit || !field.ValidityData.State.CustomError)
            {
                Publish(value, next with { Valid = null }, messages);
            }
            else
            {
                FlushInputs();
            }

            return AwaitValidatorAsync(value, sequence, pending);
        }

        Finish(value, element, next, pending.Result);
        return Task.CompletedTask;
    }

    // FFI-FRM-12/13: a superseded result is dropped; a rejection keeps the published state and propagates.
    private async Task AwaitValidatorAsync(object? value, int sequence, ValueTask<FieldValidationResult?> pending)
    {
        FieldValidationResult? result = await pending.ConfigureAwait(true);
        if (sequence != commitSequence)
        {
            return;
        }

        FieldValidationInput? element = Representative();
        FieldValidityFlags next = RefreshState(ref element);
        Finish(value, element, next, result);
    }

    private void Finish(object? value, FieldValidationInput? element, FieldValidityFlags next, FieldValidationResult? result)
    {
        IReadOnlyList<string> errors = result?.Errors ?? s_none;
        IReadOnlyList<string> messages;
        if (errors.Count > 0)
        {
            next = next with { Valid = false, CustomError = true };
            if (element is not null && element.State.WillValidate)
            {
                // FFI-FRM-08: a barred input never receives the field's message.
                InstallOwnCustomValidity(element, string.Join('\n', errors));
            }

            messages = errors;
        }
        else
        {
            messages = NativeMessages(element);
        }

        Publish(value, next, messages);
    }

    // FFI-ALG-02 `refreshState`: a barred input exposes no native constraint.
    private FieldValidityFlags RefreshState(ref FieldValidationInput? element)
    {
        element = Representative();
        if (element is null || !element.State.WillValidate)
        {
            return MakeState(customError: false);
        }

        FieldValidityFlags state = Flags(element.State);
        if (state.ValueMissing && !HasOtherNativeError(element.State) && !element.State.CustomError && !field.MarkedDirty)
        {
            // FFI-FRM-06: a pristine required field is not reported invalid.
            state = state with { Valid = true, ValueMissing = false };
        }

        return state;
    }

    // FFI-ALG-14.
    private void Publish(object? value, FieldValidityFlags state, IReadOnlyList<string> messages, bool? externalInvalid = null)
    {
        IReadOnlyList<string> errors = state.Valid == false ? messages : s_none;
        FieldValidityData data = new(state, errors.Count > 0 ? errors[0] : string.Empty, errors, value, field.ValidityData.InitialValue);
        field.PublishValidity(data, externalInvalid ?? field.Invalid);
        FlushInputs();
    }

    // FFI-FRM-09: the field withdraws only the message it installed; the module restores what it displaced.
    private void ClearOwnCustomValidity()
    {
        ownedInput?.SetOwnMessage(null);
        ownedInput = null;
    }

    private void InstallOwnCustomValidity(FieldValidationInput element, string message)
    {
        element.SetOwnMessage(message);
        ownedInput = element;
    }

    private void FlushInputs()
    {
        InputRef?.Flush();
        foreach (FieldValidationInput input in registeredInputs)
        {
            input.Flush();
        }
    }

    private void CancelDebounce()
    {
        debounce?.Dispose();
        debounce = null;
    }

    // The debounced commit captures the value it was scheduled with (FLD-FRM-04) and runs on the renderer's dispatcher.
    // A timer that a later change replaced or cancelled does nothing when its callback still arrives.
    private sealed class DebouncedCommit(FieldValidation owner, object? value)
    {
        public ITimer? Timer { get; set; }

        public void Fire() => owner.field.Invoke(() =>
        {
            if (Timer is null || !ReferenceEquals(owner.debounce, Timer))
            {
                return;
            }

            owner.CancelDebounce();
            owner.field.Observe(owner.CommitAsync(value));
        });
    }
}
