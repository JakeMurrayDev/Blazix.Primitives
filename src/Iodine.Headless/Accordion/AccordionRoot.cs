using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ACC-STA-01, §3.1 state object; accordion/root/AccordionRoot.tsx:91-98. ADR 08 §3: sealed, partial, [PartState]; ADR 02
// Amendments 2026-10-26: the value is in the state, never projected ([NotProjected]; ACC-DOM-03, ACC-DOM-09).
/// <summary>Represents the state of an accordion root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The values of the open items; never projected as an attribute.</param>
/// <param name="Disabled">A value indicating whether every item ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The root's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record AccordionRootState<TValue>([property: NotProjected] IReadOnlyList<TValue> Value, bool Disabled, Orientation Orientation);

// accordion/root/AccordionRootDataAttributes.ts (ACC-DOM-01/02), in Base UI's order.
/// <summary>Provides the names of the data attributes an accordion root renders.</summary>
public static class AccordionRootDataAttributes
{
    /// <summary>Represents the attribute present while the accordion is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute that carries the orientation, <c>vertical</c> or <c>horizontal</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Accordion.Root (accordion/root/AccordionRoot.tsx); the contract is docs/components/accordion.md.
// - One `div` with no handler, role, aria-* or dir (ACC-STR-01, ACC-DOM-03, ACC-I18N-01); no keyboard navigation between
//   triggers (ACC-KEY-04): LoopFocus is consumed and discarded, Orientation reaches the state only (ACC-API-08/09).
// - The value is AccordionRootContext<TValue>'s Controlled<IReadOnlyList<TValue>> (ADR 08 Amendments 2026-10-26, chosen by the
//   user); TValue reaches the items through [CascadingTypeParameter] (the RadioGroup precedent).
// - ACC-EDGE-04: an explicit KeepMounted="false" with HiddenUntilFound is found with TryGetValue and logged once (settled by the
//   orchestrator 2026-10-25 under the CollapsiblePanel precedent).
// - The context is cascaded around the element, as Base UI's provider wraps it; fixed (ADR 16 rule 9).
// - It is the registration-only composite list of its items (ACC-STR-01's registry, ACC-STA-13; CompositeListBehavior, track 1's
//   E1, ADR 04 Amendments 2026-10-26): no listener, no tabindex, the items' order reported by the module. Its context is
//   cascaded inside the element, around the consumer's content (the RadioGroup pattern), fixed (ADR 16 rule 9).
/// <summary>
/// Represents the root of an accordion, Base UI's <c>Accordion.Root</c>, rendered as a <c>div</c>: a vertically stacked
/// set of <see cref="AccordionItem{TValue}"/> sections whose panels open and close. It owns the values of the open items.
/// </summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <remarks>
/// An <see cref="AccordionItem{TValue}"/> inside the root takes <typeparamref name="TValue"/> from the root when its own
/// <c>Value</c> does not determine it. Items without a <c>Value</c> are identified by a generated id, which needs a
/// <typeparamref name="TValue"/> of <see cref="string"/>: set <c>TValue="string"</c> on the root when no item has a value. An
/// item whose <typeparamref name="TValue"/> differs from the root's throws <see cref="InvalidOperationException"/> when it is
/// initialized.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AccordionRoot<TValue> : PartBase<AccordionRootState<TValue>>
{
    private const string DefaultTag = "div";

    private readonly RenderFragment renderPart;
    private readonly Action committed;
    private AccordionRootContext<TValue>? context;
    private CompositeListBehavior? list;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private AccordionRootState<TValue>? state;
    private bool pendingKeepMountedWarning;

    /// <summary>Initializes a new instance of the <see cref="AccordionRoot{TValue}"/> class.</summary>
    public AccordionRoot()
    {
        renderPart = base.BuildRenderTree;
        committed = StateHasChanged;
    }

    [Inject]
    private ILogger<AccordionRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private CollapsibleDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // ACC-API-01
    /// <summary>Gets or sets the values of the open items, when the consumer owns them.</summary>
    /// <value>
    /// The values. Supplying this parameter on the first render makes the value the consumer's for the lifetime of the
    /// accordion: an item's request then only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>. To let the
    /// accordion own the value, use <see cref="DefaultValue"/> instead. <see langword="null"/> opens no item.
    /// </value>
    [Parameter]
    public IReadOnlyList<TValue>? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a request that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/> with a new list.</value>
    [Parameter]
    public EventCallback<IReadOnlyList<TValue>> ValueChanged { get; set; }

    // ACC-API-02
    /// <summary>Gets or sets the values of the items initially open, when the accordion owns the value.</summary>
    /// <value>The values, read on the first render only; <see langword="null"/>, the default, opens no item.</value>
    [Parameter]
    public IReadOnlyList<TValue>? DefaultValue { get; set; }

    // ACC-API-03, ACC-EVT-03/04
    /// <summary>Gets or sets the callback invoked when an item asks to open or close, before the value changes.</summary>
    /// <value>
    /// A callback that receives the requested value, a new list, and the request's details, the same details the item's
    /// <c>OnOpenChange</c> received: the reason <see cref="EventReason.TriggerPress"/> for a trigger press and
    /// <see cref="EventReason.None"/> for a find-in-page reveal. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the
    /// current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValueChange { get; set; }

    // ACC-API-04
    /// <summary>Gets or sets a value indicating whether several items can be open at once.</summary>
    /// <value>
    /// <see langword="true"/> to let items open and close independently; the default is <see langword="false"/>, under which
    /// opening an item closes the one that was open.
    /// </value>
    [Parameter]
    public bool Multiple { get; set; }

    // ACC-API-05
    /// <summary>Gets or sets a value indicating whether every item ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable every item, whatever its own <c>Disabled</c>; the default is <see langword="false"/>.
    /// The value can still change through <see cref="Value"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // ACC-API-06
    /// <summary>Gets or sets a value indicating whether the browser's find-in-page can find and expand closed panels.</summary>
    /// <value>
    /// <see langword="true"/> to keep every closed panel without its own <c>HiddenUntilFound</c> in the document, hidden with
    /// <c>hidden="until-found"</c>; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool HiddenUntilFound { get; set; }

    // ACC-API-07
    /// <summary>Gets or sets a value indicating whether closed panels stay in the document.</summary>
    /// <value>
    /// <see langword="true"/> to keep every closed panel without its own <c>KeepMounted</c> in the document, hidden with the
    /// <c>hidden</c> attribute; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // ACC-API-08; ADR 08 Amendments 2026-10-26: deprecated in Base UI, declared without [Obsolete].
    /// <summary>Gets or sets the visual orientation of the accordion. Deprecated: it does not change keyboard or focus behavior.</summary>
    /// <value>
    /// <see cref="Orientation.Vertical"/>, the default, or <see cref="Orientation.Horizontal"/>; reflected only in every
    /// part's state and <c>data-orientation</c> attribute. Base UI deprecates it since keyboard navigation between the
    /// triggers was removed.
    /// </value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    // ACC-API-09; ADR 08 Amendments 2026-10-26: accepted and discarded, without [Obsolete].
    /// <summary>Gets or sets a value indicating whether focus loops between the triggers. Deprecated: it has no effect.</summary>
    /// <value>Ignored; the parameter is accepted so that it is not rendered as an attribute. Base UI deprecates it.</value>
    [Parameter]
    public bool LoopFocus { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AccordionRootState<TValue> State
    {
        get
        {
            AccordionRootContext<TValue> shared = context!;
            if (state is null || !ReferenceEquals(state.Value, shared.Value) || state.Disabled != shared.Disabled || state.Orientation != shared.Orientation)
            {
                state = new AccordionRootState<TValue>(shared.Value, shared.Disabled, shared.Orientation);
            }

            return state;
        }
    }

    // ADR 02 decision 1: under a substitute callback the list's twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures: completes once the registration-only list is attached to the root's element.
    /// <summary>Gets a task that completes when the root's list behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached it.</value>
    internal Task TwinAttached => list!.TwinAttached;

    /// <summary>Gets the shared state the root cascades to its items.</summary>
    /// <value>The context; available from the first parameter set.</value>
    internal AccordionRootContext<TValue> Context => context!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        context ??= new AccordionRootContext<TValue>(Logger);
        context.ValueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));

        // ACC-EDGE-04: only the root's own two parameters are inspected.
        if (parameters.TryGetValue(nameof(KeepMounted), out bool keepMounted) && !keepMounted
            && parameters.TryGetValue(nameof(HiddenUntilFound), out bool hiddenUntilFound) && hiddenUntilFound)
        {
            pendingKeepMountedWarning = true;
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        list = new CompositeListBehavior(JSRuntime, IdGenerator, StateHasChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        AccordionRootContext<TValue> shared = context!;
        shared.OnValueChange = OnValueChange;
        shared.ValueChanged = ValueChanged;
        shared.Committed = committed;
        shared.ApplyParameters(Multiple, Disabled, Orientation, KeepMounted, HiddenUntilFound);
        if (pendingKeepMountedWarning)
        {
            pendingKeepMountedWarning = false;
            DevelopmentLog.Report(Logger, CollapsibleDevelopmentLog.AccordionRootKeepMountedIgnoredMessage);
        }

        // The list's registry wraps the consumer's content inside the root's element; rebuilt only for other content.
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = list!.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Base UI's provider wraps the root's element; the cascade is fixed and the items listen (ADR 16 rule 9).
        builder.OpenComponent<CascadingValue<AccordionRootContext<TValue>>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<AccordionRootContext<TValue>>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<AccordionRootContext<TValue>>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<AccordionRootContext<TValue>>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return list!.AfterRenderAsync(Enabled, Element, Id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            list?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        list!.AddAttributes(attributes, Id);
    }

    // ACC-EDGE-01 (the PRG-EDGE-01 precedent: `Blazix: ` for `Base UI: `, the part names for the context names).
    internal static InvalidOperationException MissingRoot() =>
        new("Blazix: The `AccordionRoot` context is missing. Accordion parts must be placed within `<AccordionRoot>`.");

    // Design point R7's pattern (ADR 02 Amendments 2026-10-20, part-misuse errors are always on): Razor infers a part's TValue
    // from its own Value first, so `Value="1"` in an AccordionRoot of int? is an AccordionItem of int.
    internal static InvalidOperationException TypeMismatch(string part) =>
        new($"Blazix: An `{part}` must have the `TValue` of its `AccordionRoot`. Set `TValue` on the part, or give `Value` the root's type, for example `Value=\"(int?)1\"` in an `AccordionRoot` of `int?`.");
}
