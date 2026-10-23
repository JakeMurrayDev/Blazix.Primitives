using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// RAD-STA-04; radio/indicator/RadioIndicator.tsx:24-33,73-78 (the root's state and the transition status). ADR 04 §2: the
// transition status is a mirrored field (JavaScript writes data-starting-style and data-ending-style).
/// <summary>Represents the state of a radio indicator: its root's state and the phase of its transition.</summary>
/// <param name="Checked">A value indicating whether the radio button is selected; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
/// <param name="Disabled">A value indicating whether the radio button ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot select the radio button; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether a radio button of the group must be selected to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Touched">A value indicating whether a radio button of the field was selected or focus left the group; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the group's value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the field has a selected value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether a radio button of the field has focus; projected as <c>data-focused</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the indicator's enter or exit transition, or <see langword="null"/> when it is not transitioning; never
/// <see cref="Primitives.TransitionStatus.Idle"/>.
/// </param>
[PartState]
public sealed partial record RadioIndicatorState(
    bool Checked,
    bool Disabled,
    bool ReadOnly,
    bool Required,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// RAD §3.3; radio/indicator/RadioIndicatorDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a radio indicator renders.</summary>
public static class RadioIndicatorDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the radio button is selected: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present, with an empty value, while the radio button is not selected: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present, with an empty value, while the radio button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the radio button is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while a selection is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the indicator plays its enter transition: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present, with an empty value, while the indicator plays its exit transition: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present, with an empty value, while the radio button's field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the radio button's field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once the radio button's field was touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the field's initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while the radio button's field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while a radio button of the field has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Radio.Indicator (radio/indicator/RadioIndicator.tsx); the contract is docs/components/radio.md §9.
// - Open is the root's `checked` (RAD-ANI-01); the element is rendered while KeepMounted || mounted.
// - The default machine (TRN §6.1; RAD §11 useTransitionStatus(checked)): no idle state, no deferred ending, no
//   initial-open animation, so an indicator rendered checked at mount has no 'starting' phase (TRN-STA-13); not hidden
//   while unmounted (RAD-DOM-15, TRN-DOM-07).
// - The handover (ADR 04 Amendments 2026-10-18, the Avatar precedent): an element C# creates open is rendered with
//   data-starting-style and attached with that status; the value stays frozen in the render output after attach. From
//   attach on the transitions module writes both transition attributes, reads the open state from the applied batch
//   through OpenSelector (ADR 04 Amendments 2026-10-21) and reports its status, which C# mirrors (RAD-STA-04's lag on
//   Server); its completion message removes the element (RAD-ANI-03; ADR 01's TRN-ANI-03 and TRN-ANI-10 rows, with the
//   convergent `hidden` meanwhile). Indicators unchecked together finish in one frame, so no batch flag is needed
//   (Checkbox's design point C1, settled by the orchestrator 2026-10-23; RAD-ANI-03's MAY).
/// <summary>
/// Represents the mark inside a radio button: Base UI's <c>Radio.Indicator</c>, rendered as a <c>span</c> while the radio
/// button is selected and through its exit transition, or always with <see cref="KeepMounted"/>.
/// </summary>
/// <remarks>The part must be placed within a <see cref="RadioRoot{TValue}"/>.</remarks>
public sealed class RadioIndicator : PartBase<RadioIndicatorState>
{
    private const string DefaultTag = "span";

    // The attribute the indicator renders while its radio button is selected; data-unchecked otherwise.
    private const string RenderedSelector = "[data-checked]";

    private static readonly StateAttributesMapping<RadioIndicatorState> s_mapping = RadioStateMapping.Create<RadioIndicatorState>();

    private readonly Action rootChanged;
    private readonly Action<TransitionStatusChange> onTransitionStatus;
    private readonly Action<bool> onTransitionCompleted;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RadioRootContext? context;
    private RadioIndicatorState? state;
    private bool mounted;
    private TransitionStatus? transitionStatus;
    private TransitionStatus? renderedTransition;
    private TransitionsTwin? transitionsTwin;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentOptions;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="RadioIndicator"/> class.</summary>
    public RadioIndicator()
    {
        rootChanged = OnRootChanged;
        onTransitionStatus = change => _ = InvokeAsync(() => MirrorTransition(change.Status, change.Mounted));
        onTransitionCompleted = open => _ = InvokeAsync(() => CompleteTransition(open));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private bool Rendered => context!.State.Checked;

    private bool Shown => KeepMounted || mounted;

    [CascadingParameter]
    private RadioRootContext? RootContext { get; set; }

    // RAD-API-10, RAD-ANI-04
    /// <summary>Gets or sets a value indicating whether the indicator stays in the document while the radio button is not selected.</summary>
    /// <value>
    /// <see langword="true"/> to keep the element, which still plays its exit transition and is never hidden;
    /// <see langword="false"/>, the default, to remove it once its exit transition has finished.
    /// </value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override RadioIndicatorState State => state ??= CreateState();

    /// <inheritdoc />
    protected override StateAttributesMapping<RadioIndicatorState>? StateAttributesMapping => s_mapping;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): a browser test waits for the module before it selects.
    /// <summary>Gets a task that completes when the indicator's transitions behavior is first attached to its element.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The indicator is not placed within a <see cref="RadioRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        context = RadioRootContext.Require(RootContext);
        context.AddListener(rootChanged);
        base.OnInitialized();

        // TRN-STA-13: checked at mount means mounted, without a 'starting' phase.
        (mounted, transitionStatus) = TransitionStatusRules.Initial(Rendered, enableIdleState: false, animateInitialOpen: false);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        state = null;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!Shown)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await SyncTransitionAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveListener(rootChanged);
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

    // The C#-rendered transition attribute is the handover value of an element created open (RAD-ANI-02).
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (renderedTransition == TransitionStatus.Starting)
        {
            attributes.Set(RadioIndicatorDataAttributes.StartingStyle, string.Empty);
        }
    }

    private RadioIndicatorState CreateState()
    {
        RadioRootState root = context!.State;
        return new RadioIndicatorState(
            root.Checked,
            root.Disabled,
            root.ReadOnly,
            root.Required,
            root.Touched,
            root.Dirty,
            root.Valid,
            root.Filled,
            root.Focused,
            transitionStatus);
    }

    private void OnRootChanged()
    {
        ApplyOpenState();
        state = null;
        StateHasChanged();
    }

    // The C# half of the transition machine for this render (TransitionStatusRules): an element created open starts in
    // 'starting'; an element with no transitions twin to animate it leaves at once.
    private void ApplyOpenState()
    {
        bool open = Rendered;
        bool creating = open && !mounted && (!KeepMounted || transitionsHandle is null);
        (bool nextMounted, TransitionStatus? nextStatus) = TransitionStatusRules.Evaluate(open, mounted, transitionStatus, deferEndingState: false);
        if (creating)
        {
            renderedTransition = nextStatus;
        }
        else if (!open && mounted && transitionsHandle is null && Element is null)
        {
            // Nothing can play the exit: a substitute callback leaves no element to watch.
            nextMounted = false;
            nextStatus = null;
            renderedTransition = null;
        }

        mounted = nextMounted;
        transitionStatus = nextStatus;
    }

    private void MirrorTransition(TransitionStatus? next, bool nextMounted)
    {
        if (disposed || (next == transitionStatus && nextMounted == mounted))
        {
            return;
        }

        transitionStatus = next;
        mounted = nextMounted;
        state = null;
        StateHasChanged();
    }

    private void CompleteTransition(bool open)
    {
        if (disposed || open || Rendered)
        {
            return;
        }

        // RAD-ANI-03: the exit finished: the element is unmounted (removed unless kept) and its status unset.
        mounted = false;
        transitionStatus = null;
        renderedTransition = null;
        state = null;
        StateHasChanged();
    }

    private async Task SyncTransitionAsync()
    {
        ElementReference? element = Shown ? Element : null;
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
            // With OpenSelector the module reads the open state from the applied batch; only KeepMounted is sent again.
            if (sentOptions!.KeepMounted != KeepMounted)
            {
                sentOptions = sentOptions with { KeepMounted = KeepMounted };
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

        // TRN §6.1, RAD §11: the default machine; Mounted and Status are what the element was rendered with, read on
        // attach only.
        sentOptions = new TransitionsOptions
        {
            Open = Rendered,
            Mounted = mounted,
            Status = renderedTransition,
            KeepMounted = KeepMounted,
            OpenSelector = RenderedSelector,
        };
        transitionsHandle = await transitionsTwin.AttachAsync(element.Value, sentOptions);
        attachedSource.TrySetResult();
    }
}
