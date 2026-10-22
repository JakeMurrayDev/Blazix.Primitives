using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// FLD-STA-07, FLD §3.7; field/validity/FieldValidity.tsx:41-50. A class with reference identity: FLD-STA-08 makes the
// instance observable (a consumer can hand it to a child that renders only when it changes).
/// <summary>Represents the validity a <see cref="FieldValidity"/> passes to its content.</summary>
/// <param name="Validity">
/// The field's validity flags, with <see cref="FieldValidityFlags.Valid"/> <see langword="false"/> also while the field is
/// invalid for a reason outside its own validation, such as its <c>Invalid</c> parameter or an error the form has for it.
/// </param>
/// <param name="Error">The field's first error message, or the empty string.</param>
/// <param name="Errors">Every error message of the field, empty unless the field is invalid.</param>
/// <param name="Value">The value the field last validated, or <see langword="null"/> before its first validation.</param>
/// <param name="InitialValue">The value captured when the field's first control registered, or <see langword="null"/> before.</param>
/// <param name="TransitionStatus">
/// The phase of the invalid state's enter or exit transition: <see cref="Primitives.TransitionStatus.Starting"/> for one
/// frame after the field becomes invalid, <see cref="Primitives.TransitionStatus.Ending"/> from the moment it stops being
/// invalid until it is invalid again, and <see langword="null"/> otherwise.
/// </param>
public sealed record FieldValidityState(
    FieldValidityFlags Validity,
    string Error,
    IReadOnlyList<string> Errors,
    object? Value,
    object? InitialValue,
    TransitionStatus? TransitionStatus);

// Base UI's Field.Validity (field/validity/FieldValidity.tsx); the contract is docs/components/field.md §3.7 and §9.
//
// No element (FLD-STR-05) and no PartBase: the content runs on every render (FLD-API-35). The argument is replaced when
// the field publishes validity, when its external invalid state changes or when the transition status changes, and kept
// otherwise (FLD-STA-08, FFI-EDGE-12).
//
// The transition (FLD-ANI-06; FLD §16 item 1 followed, and design point E2 of PR 1c, pending the orchestrator): Base UI's
// machine over "the combined validity is invalid", whose mounted flag nothing clears, so 'ending' lasts until the field is
// invalid again. With no element and no frames in C#, the next-frame clear of 'starting' (and of a stale 'ending') runs
// one frame's time later on the field's TimeProvider (ADR 01 decision 4: timers on TimeProvider).
/// <summary>
/// Represents a part that renders content from a field's validity: Base UI's <c>Field.Validity</c>. It renders no element
/// of its own and belongs inside a <see cref="FieldRoot"/>.
/// </summary>
public sealed class FieldValidity : ComponentBase, IDisposable
{
    private static readonly TimeSpan s_frame = TimeSpan.FromMilliseconds(16);

    private readonly Action fieldChanged;
    private readonly TimerCallback clearElapsed;
    private FieldRootContext? field;
    private FieldValidityState? state;
    private FieldValidityData? stateData;
    private bool stateInvalid;
    private TransitionStatus? stateTransition;
    private bool evaluated;
    private bool invalid;
    private bool mounted;
    private TransitionStatus? transitionStatus;
    private ITimer? clearTimer;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="FieldValidity"/> class.</summary>
    public FieldValidity()
    {
        fieldChanged = OnFieldChanged;
        clearElapsed = timerState => _ = InvokeAsync(ClearStatus);
    }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    // FLD-API-35
    /// <summary>Gets or sets the content rendered from the field's validity.</summary>
    /// <value>A template that receives the field's <see cref="FieldValidityState"/>, such as <c>&lt;FieldValidity Context="validity"&gt;@validity.Error&lt;/FieldValidity&gt;</c>; it runs on every render of the part.</value>
    [Parameter]
    [EditorRequired]
    public RenderFragment<FieldValidityState> ChildContent { get; set; } = null!;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The part is not inside a <see cref="FieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        field = FieldScopes.RequireField(FieldContext);
        field.AddListener(fieldChanged);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"><see cref="ChildContent"/> is <see langword="null"/>.</exception>
    protected override void OnParametersSet()
    {
        if (ChildContent is null)
        {
            throw new InvalidOperationException("Blazix: `FieldValidity` requires `ChildContent`, a template that receives the field's validity.");
        }

        Evaluate();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent(State()));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        field?.RemoveListener(fieldChanged);
        clearTimer?.Dispose();
        clearTimer = null;
    }

    private void OnFieldChanged()
    {
        if (!disposed && Evaluate())
        {
            StateHasChanged();
        }
    }

    // Runs the transition machine on the combined invalid state; returns whether the argument would change.
    private bool Evaluate()
    {
        FieldRootContext context = field!;
        bool nextInvalid = context.ValidityData.Combined(context.Invalid).State.Valid == false;
        if (!evaluated)
        {
            // FLD-ANI-06: a field invalid on the first render starts with the status unset.
            evaluated = true;
            invalid = nextInvalid;
            (mounted, transitionStatus) = TransitionStatusRules.Initial(invalid, enableIdleState: false, animateInitialOpen: false);
        }
        else if (nextInvalid != invalid)
        {
            invalid = nextInvalid;
            (mounted, transitionStatus) = TransitionStatusRules.Evaluate(invalid, mounted, transitionStatus, deferEndingState: false);
            ScheduleClear();
        }

        return state is null
            || !ReferenceEquals(stateData, context.ValidityData)
            || stateInvalid != context.Invalid
            || stateTransition != transitionStatus;
    }

    // The machine's default settle: while invalid, a status other than unset is cleared one frame later.
    private void ScheduleClear()
    {
        if (invalid && transitionStatus is not null)
        {
            clearTimer ??= field!.TimeProvider.CreateTimer(clearElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            clearTimer.Change(s_frame, Timeout.InfiniteTimeSpan);
        }
        else
        {
            clearTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void ClearStatus()
    {
        if (disposed || !invalid || transitionStatus is null)
        {
            return;
        }

        transitionStatus = null;
        StateHasChanged();
    }

    private FieldValidityState State()
    {
        FieldRootContext context = field!;
        FieldValidityData data = context.ValidityData;
        bool fieldInvalid = context.Invalid;
        if (state is null || !ReferenceEquals(stateData, data) || stateInvalid != fieldInvalid || stateTransition != transitionStatus)
        {
            stateData = data;
            stateInvalid = fieldInvalid;
            stateTransition = transitionStatus;
            FieldValidityData combined = data.Combined(fieldInvalid);
            state = new FieldValidityState(combined.State, combined.Error, combined.Errors, combined.Value, combined.InitialValue, transitionStatus);
        }

        return state;
    }
}
