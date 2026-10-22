using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// FLD-STA-06; field/error/FieldError.tsx:113-116,139-144. ADR 04 §2: the transition status is a mirrored field (JS writes
// data-starting-style / data-ending-style); 'idle' is never produced (no idle state).
/// <summary>Represents the state of a field error, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the field is disabled; projected as <c>data-disabled</c>. An enclosing <see cref="FieldItem"/>'s disabled state is not reflected.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the field's value differs from its initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether the field's control has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the field's control has focus; projected as <c>data-focused</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the message's enter or exit transition, or <see langword="null"/> when it is not transitioning; never
/// <see cref="Primitives.TransitionStatus.Idle"/>.
/// </param>
[PartState]
public sealed partial record FieldErrorState(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// FLD-DOM-04, FLD-DOM-18; field/error/FieldErrorDataAttributes.ts.
/// <summary>Provides the names of the data attributes a field error renders.</summary>
public static class FieldErrorDataAttributes
{
    /// <summary>Represents the attribute present while the field is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present once the field has been touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the field's value differs from its initial value: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while the field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the field's control has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";

    /// <summary>Represents the attribute present, with an empty value, while the message plays its enter transition: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present, with an empty value, while the message plays its exit transition: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// Base UI's Field.Error (field/error/FieldError.tsx); the contract is docs/components/field.md §3.6, §5.6 and §9.
//
// Rendered (FLD-API-30, FFI-ALG-15): Match true always; else not while the field is disabled (the field's, FLD-EDGE-04,
// FLD §16 item 5 followed); a flag while the field's published validity has it set (Valid included, FLD-EDGE-07, item 3
// followed); the default slot while the field has an error from outside its own validation or its verdict is invalid.
// The port's outside errors are the form's errors for the field's name and, after them, the messages of the field's
// external-error slot (a cascaded EditContext's, ADR 05 Amendments 2026-10-22).
//
// The message (FLD-DOM-15..17): the outside errors unless a flag is matched, else the field's errors when there are
// several, else its first error; a list of more than one is a <ul> (entries unkeyed: Blazor rejects duplicate keys, React
// only warns), one entry is its text. While the exit plays the part shows the last message it showed while rendered.
// ChildContent replaces the message (FLD-API-32): the part keeps the consumer's fragment and hands the pipeline its own,
// as MeterValue does.
//
// The id (FLD-API-31, FLD-ARIA-05, FFI-ARIA-02): published to the nearest labelable scope while rendered, in the render
// batch that renders it (ADR 01 Amendments 2026-10-22: met on both modes), never when empty (FLD-EDGE-03).
//
// The transition (FLD §9, TRN §6.1: no idle state, no deferred ending, no initial animation): one TransitionsTwin on the
// element while it is mounted. The element C# creates carries data-starting-style (the handover, ADR 04 Amendments
// 2026-10-18; the value stays frozen in the render output after attach, as Avatar.Image's). No attribute C# renders carries
// the rendered state, so the close reaches the module through `update` (design point E1 of PR 1c, pending the
// orchestrator: the Avatar.Image row of ADR 01). The module's completion message unmounts the part; a completion that
// crossed a reopen (Server) is ignored, and the module's later report settles the status. Without an element to watch (a
// substitute callback) the exit ends at once.
/// <summary>
/// Represents the message shown when a field fails validation: Base UI's <c>Field.Error</c>, rendered as a <c>div</c> only
/// while the message is shown or plays its exit transition. It belongs inside a <see cref="FieldRoot"/>; the control
/// lists its id in <c>aria-describedby</c> while the message is shown.
/// </summary>
/// <remarks>
/// Without <see cref="PartBase{TState}.ChildContent"/> the part shows the field's message: the form's error for the field
/// when there is one, else the field's own errors, as a list when there are several.
/// </remarks>
public sealed class FieldError : PartBase<FieldErrorState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<FieldErrorState> s_mapping = FieldValidityMapping.Create<FieldErrorState>();

    private readonly Action fieldChanged;
    private readonly RenderFragment content;
    private readonly Action<TransitionStatusChange> onTransitionStatus;
    private readonly Action<bool> onTransitionCompleted;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FieldRootContext? fieldScope;
    private LabelableScope? scope;
    private RenderFragment? consumerContent;
    private string? publishedId;
    private bool evaluated;
    private bool rendered;
    private bool mounted;
    private TransitionStatus? transitionStatus;
    private TransitionStatus? renderedTransition;
    private ErrorMessage message;
    private ErrorMessage lastMessage;
    private FieldErrorState? state;
    private FieldStateSnapshot renderedSnapshot;
    private TransitionsTwin? transitionsTwin;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentOptions;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="FieldError"/> class.</summary>
    public FieldError()
    {
        fieldChanged = OnFieldChanged;
        content = RenderContent;
        onTransitionStatus = change => _ = InvokeAsync(() => MirrorTransition(change.Status));
        onTransitionCompleted = open => _ = InvokeAsync(() => CompleteTransition(open));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    // FLD-API-30
    /// <summary>Gets or sets when the message is shown.</summary>
    /// <value>
    /// <see langword="true"/> to show it always, even while the field is valid or disabled; a <see cref="ValidityFlag"/>
    /// to show it while the field's validity has that flag set; <see langword="false"/> or <see langword="null"/>, the
    /// default, to show it while the field is invalid for any reason, including an error the form has for the field.
    /// </value>
    [Parameter]
    public MatchValue? Match { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldErrorState State
    {
        get
        {
            FieldStateSnapshot snapshot = fieldScope!.State;
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new FieldErrorState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused, transitionStatus);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<FieldErrorState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes once the transitions module has been attached to the element for the first time.</summary>
    /// <value>The task; it never completes while the message has not been mounted.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <remarks>The content of the previous parameter set is not carried over: the part shows its message unless this set supplies content.</remarks>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The error is not inside a <see cref="FieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        fieldScope = FieldScopes.RequireField(FieldContext);
        scope = FieldItemContext.LabelableOf(fieldScope, ItemContext);
        base.OnInitialized();
        fieldScope.AddListener(fieldChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerContent = ChildContent;
        ChildContent = content;
        Evaluate();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // FLD-DOM-14, FLD-ANI-01: the element exists only while mounted.
        if (!mounted)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!disposed)
        {
            await SyncTransitionAsync();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            if (fieldScope is not null)
            {
                fieldScope.RemoveListener(fieldChanged);
                Publish(null);
            }

            if (transitionsTwin is not null)
            {
                transitionsTwin.StatusChanged -= onTransitionStatus;
                transitionsTwin.TransitionCompleted -= onTransitionCompleted;
                _ = transitionsTwin.DisposeAsync().AsTask();
                transitionsTwin = null;
            }

            transitionsHandle = null;
        }

        base.Dispose(disposing);
    }

    // The C#-rendered transition attribute is the handover value of the element C# created.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (renderedTransition == TransitionStatus.Starting)
        {
            attributes.Set(FieldErrorDataAttributes.StartingStyle, string.Empty);
        }
    }

    private static bool HasFlag(FieldValidityFlags flags, ValidityFlag flag) => flag switch
    {
        ValidityFlag.BadInput => flags.BadInput,
        ValidityFlag.CustomError => flags.CustomError,
        ValidityFlag.PatternMismatch => flags.PatternMismatch,
        ValidityFlag.RangeOverflow => flags.RangeOverflow,
        ValidityFlag.RangeUnderflow => flags.RangeUnderflow,
        ValidityFlag.StepMismatch => flags.StepMismatch,
        ValidityFlag.TooLong => flags.TooLong,
        ValidityFlag.TooShort => flags.TooShort,
        ValidityFlag.TypeMismatch => flags.TypeMismatch,
        ValidityFlag.Valid => flags.Valid == true,
        ValidityFlag.ValueMissing => flags.ValueMissing,
        _ => false,
    };

    private void OnFieldChanged()
    {
        if (disposed)
        {
            return;
        }

        if (!Evaluate() && state is not null && fieldScope!.State == renderedSnapshot)
        {
            return;
        }

        StateHasChanged();
    }

    // Recomputes the rendered predicate, the message and the published id; returns whether what the part shows changed.
    private bool Evaluate()
    {
        FieldRootContext context = fieldScope!;
        FieldValidityData data = context.ValidityData;
        bool always = false;
        bool flagged = false;
        ValidityFlag flag = default;
        if (Match is { } match)
        {
            if (match.TryGetValue(out bool value))
            {
                always = value;
            }
            else
            {
                flagged = match.TryGetValue(out flag);
            }
        }

        IReadOnlyList<string> outside = context.FormErrors;
        if (outside.Count == 0)
        {
            outside = context.ExternalErrors;
        }

        bool nextRendered = always || (!context.Disabled && (flagged ? HasFlag(data.State, flag) : outside.Count > 0 || data.State.Valid == false));
        ErrorMessage nextMessage = !flagged && outside.Count > 0 ? new ErrorMessage(null, outside)
            : data.Errors.Count > 1 ? new ErrorMessage(null, data.Errors)
            : new ErrorMessage(data.Error, null);

        bool changed = !nextMessage.Equals(message);
        message = nextMessage;
        if (!evaluated)
        {
            // FLD-ANI-05: a message rendered in the part's first render mounts without 'starting'.
            evaluated = true;
            rendered = nextRendered;
            (mounted, transitionStatus) = TransitionStatusRules.Initial(rendered, enableIdleState: false, animateInitialOpen: false);
            changed = true;
        }
        else if (nextRendered != rendered)
        {
            rendered = nextRendered;
            ApplyOpenState();
            changed = true;
        }

        if (rendered)
        {
            lastMessage = message;
        }

        Publish(rendered && Enabled ? Id : null);
        return changed;
    }

    // The C# half of the transition machine for a change of the rendered state (TransitionStatusRules): the element
    // created for a message that was not mounted starts in 'starting'; an element with nothing to play its exit leaves at once.
    private void ApplyOpenState()
    {
        (bool nextMounted, TransitionStatus? nextStatus) = TransitionStatusRules.Evaluate(rendered, mounted, transitionStatus, deferEndingState: false);
        if (rendered && !mounted)
        {
            renderedTransition = nextStatus;
        }
        else if (!rendered && mounted && transitionsHandle is null && Element is null)
        {
            nextMounted = false;
            nextStatus = null;
            renderedTransition = null;
        }

        mounted = nextMounted;
        transitionStatus = nextStatus;
        state = null;
    }

    private void Publish(string? id)
    {
        if (id is { Length: 0 })
        {
            id = null;
        }

        if (string.Equals(id, publishedId, StringComparison.Ordinal))
        {
            return;
        }

        using FieldRootContext.BatchScope batch = fieldScope!.Batch();
        if (publishedId is not null)
        {
            scope!.RemoveMessageId(publishedId);
        }

        if (id is not null)
        {
            scope!.AddMessageId(id);
        }

        publishedId = id;
    }

    private void MirrorTransition(TransitionStatus? next)
    {
        if (disposed || next == transitionStatus)
        {
            return;
        }

        transitionStatus = next;
        state = null;
        StateHasChanged();
    }

    private void CompleteTransition(bool open)
    {
        if (disposed || open || rendered)
        {
            return;
        }

        // The exit finished: the element is removed and the status unset.
        mounted = false;
        transitionStatus = null;
        renderedTransition = null;
        state = null;
        StateHasChanged();
    }

    private async Task SyncTransitionAsync()
    {
        ElementReference? element = mounted ? Element : null;
        if (element is null || (transitionsHandle is not null && !string.Equals(transitionsHandle.Key, element.Value.Id, StringComparison.Ordinal)))
        {
            if (transitionsHandle is { } released)
            {
                transitionsHandle = null;
                sentOptions = null;
                await released.DisposeAsync();
            }

            if (element is null)
            {
                return;
            }
        }

        if (transitionsHandle is not null)
        {
            if (sentOptions!.Open != rendered)
            {
                sentOptions = NewOptions();
                await transitionsHandle.UpdateAsync(sentOptions);
            }

            return;
        }

        if (transitionsTwin is null)
        {
            transitionsTwin = new TransitionsTwin(JSRuntime);
            transitionsTwin.StatusChanged += onTransitionStatus;
            transitionsTwin.TransitionCompleted += onTransitionCompleted;
        }

        sentOptions = NewOptions();
        transitionsHandle = await transitionsTwin.AttachAsync(element.Value, sentOptions);
        attachedSource.TrySetResult();
    }

    // TRN §6.1: no idle state, no deferred ending, no initial animation; Mounted and Status are what the element was
    // rendered with, read on attach only.
    private TransitionsOptions NewOptions() => new()
    {
        Open = rendered,
        Mounted = mounted,
        Status = renderedTransition,
    };

    private void RenderContent(RenderTreeBuilder builder)
    {
        if (consumerContent is not null)
        {
            builder.AddContent(0, consumerContent);
            return;
        }

        ErrorMessage shown = rendered ? message : lastMessage;
        if (shown.Entries is not { } list)
        {
            builder.AddContent(1, shown.Text);
            return;
        }

        if (list.Count == 1)
        {
            builder.AddContent(2, list[0]);
            return;
        }

        if (list.Count > 1)
        {
            builder.OpenElement(3, "ul");
            for (int i = 0; i < list.Count; i++)
            {
                builder.OpenElement(4, "li");
                builder.AddContent(5, list[i]);
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }

    // FLD-DOM-15: a message is text or a list; equal when the text or the entries are.
    private readonly record struct ErrorMessage(string? Text, IReadOnlyList<string>? Entries)
    {
        /// <inheritdoc />
        public bool Equals(ErrorMessage other)
        {
            if (Entries is null || other.Entries is null)
            {
                return Entries is null && other.Entries is null && string.Equals(Text, other.Text, StringComparison.Ordinal);
            }

            if (ReferenceEquals(Entries, other.Entries))
            {
                return true;
            }

            if (Entries.Count != other.Entries.Count)
            {
                return false;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (!string.Equals(Entries[i], other.Entries[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override int GetHashCode() => Entries is null ? StringComparer.Ordinal.GetHashCode(Text ?? string.Empty) : Entries.Count;
    }
}
