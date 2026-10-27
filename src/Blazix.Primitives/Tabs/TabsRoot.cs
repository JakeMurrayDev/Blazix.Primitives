using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TAB-STA-20; tabs/root/TabsRoot.tsx:332-335,411-420. ADR 04 Amendments 2026-10-26 (chosen by the user 2026-10-26): the
// `tabs` module writes data-activation-direction from attach, so the direction is a mirrored field (RND-STA-01 row of
// ADR 01); C# renders its initial value once per element under the handover (TabsRoot.AddPartAttributes).
/// <summary>Represents the state of a tabs root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The axis along which the tabs are laid out; projected as <c>data-orientation</c>.</param>
/// <param name="TabActivationDirection">
/// The direction from the previously active tab to the active one, rendered as <c>data-activation-direction</c> by the
/// browser-side behavior; <see cref="TabsActivationDirection.None"/> initially and after an automatic change.
/// </param>
[PartState]
public sealed partial record TabsRootState(Orientation Orientation, [property: MirroredField] TabsActivationDirection TabActivationDirection);

// TAB §3.1; tabs/root/TabsRootDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tabs root renders.</summary>
public static class TabsRootDataAttributes
{
    /// <summary>Represents the attribute whose value is the activation direction, <c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = "data-activation-direction";

    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Tabs.Root (tabs/root/TabsRoot.tsx); the contract is docs/components/tabs.md.
// - One `div` with no handler and no id of its own (TAB-STR-01, TAB-DOM-01/02).
// - The value is a Controlled<TValue> fixed at the first parameter set (ADR 05 Amendments 2026-10-18; TAB §16 Q4). ADR 08
//   Amendments 2026-10-26 (chosen by the user 2026-10-26): TValue, inferred from Value or DefaultValue; while neither is
//   supplied the Root holds the implicit default (Base UI's number 0, TabsImplicitDefault) until the first commit; `null`
//   selects nothing; OnValueChange's details are a TabsChangeEventDetails at run time.
// - The parts read the Root through TabsRootContext<TValue> (the value, IsActive, the mounted-panel registry of TAB-STA-21,
//   the request), cascaded fixed; a selection renders the two Tabs and the two Panels whose state flipped, never the
//   Root's subtree (ADR 16 rules 8 and 9).
// - The Tab registry is the List's Tabs in the order the `composite` module reports (track 1's E2; TAB-STA-04). On each
//   report, and after a registered Tab's value or `disabled` changed, an uncontrolled Root re-validates its value as Base
//   UI's layout effect does (TAB-STA-06..12; tabs/root/TabsRoot.tsx:240-330): post-commit C# state work, after paint plus
//   a round trip on Server (ADR 01's "EVD-STA-07, CMP-STA-10, FFI registration" row). An automatic change commits before
//   it notifies, so it is not cancelable (TAB-EVT-04); a change to nothing is "nothing selected" (ADR 08 Amendments
//   2026-10-26), `default(TValue)` in the callback.
// - The Root is also the registration-only list of its Panels (track 1's E1, CompositeListBehavior; TabsRoot.tsx:343-346),
//   whose report gives each rendered Panel its data-index (TAB-DOM-17). It renders no id of its own: a late id only under a
//   substitute callback, for the module to find the element.
/// <summary>
/// Represents a set of tabs and the panels they show: Base UI's <c>Tabs.Root</c>, rendered as a <c>div</c>. The root
/// owns the active value, and every <see cref="TabsTab{TValue}"/> and <see cref="TabsPanel{TValue}"/> inside it compares
/// its own value with it.
/// </summary>
/// <typeparam name="TValue">The type of the value of the active tab.</typeparam>
/// <remarks>
/// A tab or panel takes <typeparamref name="TValue"/> from the root when its own <c>Value</c> does not determine it. For a
/// nullable value type such as <c>int?</c>, set <c>TValue</c> on each tab and panel: <c>Value="1"</c> alone is an
/// <see cref="int"/>. A tab or panel whose <typeparamref name="TValue"/> differs from the root's throws
/// <see cref="InvalidOperationException"/> when it is initialized.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class TabsRoot<TValue> : PartBase<TabsRootState>
{
    private const string DefaultTag = "div";

    private readonly Action contextChanged;
    private Controlled<TValue>? valueState;
    private bool valueSupplied;
    private bool implicitDefault;
    private bool selectedOnce;
    private bool cleared;
    private bool explicitDefault;
    private TValue initialDefault = default!;
    private bool notifyInitialPending;
    private bool honorDisabledDefault;
    private bool tabsRegistered;
    private bool validating;
    private bool validateAgain;
    private TabsRootContext<TValue>? context;
    private CompositeListBehavior? panelList;
    private TabsRootState? state;
    private string? renderedDirection;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TabsRoot{TValue}"/> class.</summary>
    public TabsRoot()
    {
        contextChanged = OnContextChanged;
    }

    [Inject]
    private ILogger<TabsRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // TAB-API-01/02: a controlled root has a value only while Value is supplied; an uncontrolled one from its default, or
    // from its first commit when it started with the implicit default.
    private bool HasSelection => valueState!.IsControlled ? valueSupplied : (!implicitDefault || selectedOnce) && !cleared;

    // TAB-API-01, TAB-STA-01; ADR 05 §2, ADR 08 Amendments 2026-10-26
    /// <summary>Gets or sets the value of the active tab, when the consumer owns the value.</summary>
    /// <value>
    /// The value. Supplying it at the first render makes the value the consumer's for the lifetime of the root: a user
    /// activation then only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>. <see langword="null"/>
    /// activates no tab.
    /// </value>
    [Parameter]
    public TValue Value { get; set; } = default!;

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a user activation that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    // TAB-API-02; ADR 08 Amendments 2026-10-26
    /// <summary>Gets or sets the value of the tab initially active, when the root owns the value.</summary>
    /// <value>
    /// The value. Read at the first render only; <see langword="null"/> activates no tab. Without it, a tab whose value is
    /// the number zero is active (for a numeric <typeparamref name="TValue"/>), as Base UI's default value <c>0</c> is.
    /// </value>
    [Parameter]
    public TValue DefaultValue { get; set; } = default!;

    // TAB-API-03, TAB-EVT-01..05; ADR 05 §3 (the details are a subclass).
    /// <summary>Gets or sets the callback invoked when the value is about to change.</summary>
    /// <value>
    /// The callback, which receives the new value and the change details. The details are a
    /// <see cref="TabsChangeEventDetails"/>: cast <c>args.Details</c> to read its
    /// <see cref="TabsChangeEventDetails.ActivationDirection"/>, as in
    /// <c>((TabsChangeEventDetails)args.Details).ActivationDirection</c>. A user activation has the reason
    /// <see cref="EventReason.None"/> and the click or focus event, and calling <see cref="ChangeEventDetails.Cancel"/>
    /// keeps the current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    // TAB-API-04; ADR 08 Amendments 2026-10-26 (non-nullable, default Horizontal).
    /// <summary>Gets or sets the axis along which the tabs are laid out.</summary>
    /// <value>
    /// <see cref="Primitives.Orientation.Horizontal"/>, the default, for the left and right arrow keys; or
    /// <see cref="Primitives.Orientation.Vertical"/> for the up and down arrow keys.
    /// </value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    // ADR 02 decision 1: under a substitute callback the panel list's twin finds the element by id (the TabsList precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    protected override TabsRootState State => state!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // TAB-API-01/02, TAB-STA-01: ownership is fixed at the first parameter set (ADR 05 Amendments 2026-10-18); the
        // default is explicit when supplied, `null` included (R1's TryGetValue).
        if (valueState is null)
        {
            valueState = new Controlled<TValue>("Tabs", "value", Logger);
            explicitDefault = parameters.TryGetValue(nameof(DefaultValue), out initialDefault!);
            implicitDefault = !parameters.TryGetValue<TValue>(nameof(Value), out _) && !explicitDefault;

            // TAB-STA-07/08: only an implicit default notifies `initial`, and only an explicit one may keep a disabled Tab.
            notifyInitialPending = !explicitDefault;
            honorDisabledDefault = explicitDefault;
        }

        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        valueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        panelList = new CompositeListBehavior(JSRuntime, IdGenerator, StateHasChanged);
        context = new TabsRootContext<TValue>(valueState!.IsControlled, RequestAsync, ValidateAsync) { PanelList = panelList.Context };
        context.AddListener(contextChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        context!.SetOrientation(Orientation);
        PublishValue();
        UpdateState();

        // The cascade wraps the consumer's content inside the root's element; it is rebuilt only when the consumer supplied
        // other content (the Meter precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await panelList!.AfterRenderAsync(Enabled, Element, Id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveListener(contextChanged);
            panelList?.Dispose();
        }

        base.Dispose(disposing);
    }

    // ADR 04 Amendments 2026-10-18 and 2026-10-26: the handover. The direction the element is created with is rendered on
    // every render, so Blazor never writes the attribute the module owns from attach.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        panelList!.AddAttributes(attributes, Id);
        renderedDirection ??= TabsLiterals.ToLiteral(context!.Direction);
        attributes.Set(TabsRootDataAttributes.ActivationDirection, renderedDirection);
    }

    // TAB-STA-03, TAB-EVT-02/03: the Root's half of a user activation. OnValueChange runs before any commit, and a cancel
    // stops it; a controlled Root commits nothing itself.
    private async Task<bool> RequestAsync(TValue next, TabsChangeEventDetails details)
    {
        if (!await valueState!.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        await valueState.CommitAsync(next, ValueChanged);
        if (!valueState.IsControlled)
        {
            // ADR 08 Amendments 2026-10-26: the first commit clears the implicit default.
            selectedOnce = true;
            implicitDefault = false;
            cleared = false;
        }

        PublishValue();
        return true;
    }

    private void PublishValue() => context!.SetValue(valueState!.Value, HasSelection, implicitDefault && !selectedOnce);

    // TAB-STA-06..12. A report or a Tab change that arrives while a consumer callback of this pass runs is not lost: the
    // pass runs again on the registry it left.
    private async Task ValidateAsync()
    {
        if (disposed || valueState!.IsControlled)
        {
            return;
        }

        if (validating)
        {
            validateAgain = true;
            return;
        }

        validating = true;
        try
        {
            do
            {
                validateAgain = false;
                await ValidateOnceAsync();
            }
            while (validateAgain && !disposed);
        }
        finally
        {
            validating = false;
        }
    }

    // tabs/root/TabsRoot.tsx:240-330, step for step.
    private async Task ValidateOnceAsync()
    {
        IReadOnlyList<TabsTabEntry<TValue>> tabs = context!.Tabs;
        if (tabs.Count == 0)
        {
            // TAB-STA-11, TAB-EDGE-10: the port has no suspension, so an empty registry after registration is a removal.
            if (tabsRegistered && context.HasValue)
            {
                await CommitAutomaticAsync(null, EventReason.Missing);
            }

            return;
        }

        tabsRegistered = true;

        // TAB-STA-05 (a): the first Tab with the value; (b): the first enabled Tab.
        TabsTabEntry<TValue>? selected = null;
        TabsTabEntry<TValue>? firstEnabled = null;
        for (int i = 0; i < tabs.Count; i++)
        {
            selected ??= context.IsActive(tabs[i].Value) ? tabs[i] : null;
            firstEnabled ??= tabs[i].Disabled ? null : tabs[i];
        }

        bool selectionIsDisabled = selected is { Disabled: true };
        bool selectionIsMissing = selected is null && context.HasValue;
        bool atInitialDefault = AtInitialDefault();

        // TAB-STA-07: an explicit default that points at a disabled Tab is kept until the selection is first valid.
        if (!selectionIsDisabled && atInitialDefault)
        {
            honorDisabledDefault = false;
        }

        if (honorDisabledDefault && selectionIsDisabled && atInitialDefault)
        {
            return;
        }

        bool notifyInitial = notifyInitialPending;
        if (selectionIsDisabled || selectionIsMissing)
        {
            // TAB-STA-10: a fallback to the current value changes nothing, but the initial transition has resolved.
            if (firstEnabled is null ? !context.HasValue : context.IsActive(firstEnabled.Value))
            {
                notifyInitialPending = false;
                return;
            }

            // TAB-STA-09.
            EventReason reason = notifyInitial ? EventReason.Initial : selectionIsDisabled ? EventReason.Disabled : EventReason.Missing;
            await CommitAutomaticAsync(firstEnabled, reason);
            return;
        }

        // TAB-STA-08: the implicit default matches an enabled Tab, which is reported without a commit.
        if (notifyInitial && selected is not null)
        {
            await NotifyAutomaticAsync(selected.Value, EventReason.Initial);
            notifyInitialPending = false;
        }
    }

    // Base UI's `value === initialDefaultValueRef.current`, with the implicit default standing for its number 0.
    private bool AtInitialDefault()
    {
        if (context!.ImplicitDefault)
        {
            return true;
        }

        if (!context.HasSelection || RadioValues.IsNull(context.Value))
        {
            return explicitDefault && RadioValues.IsNull(initialDefault);
        }

        return explicitDefault ? Equality.StrictEquals(context.Value, initialDefault) : TabsImplicitDefault.Matches(context.Value);
    }

    // TAB-STA-06, TAB-EVT-04: the fallback is committed first, so the callback cannot cancel it, and the parts render it
    // after the callback returns, as Base UI's batched commit, even when the callback throws.
    private async Task CommitAutomaticAsync(TabsTabEntry<TValue>? fallback, EventReason reason)
    {
        TValue value = fallback is null ? default! : fallback.Value;
        selectedOnce = true;
        implicitDefault = false;
        cleared = fallback is null;
        valueState!.SetValueIfUncontrolled(value);
        try
        {
            await NotifyAutomaticAsync(value, reason);
        }
        finally
        {
            PublishValue();
        }

        notifyInitialPending = false;

        // Base UI's effect runs again on the committed value; a duplicate disabled Tab then resolves to it (TAB-STA-10).
        validateAgain = true;
    }

    // TAB-EVT-04: a fresh placeholder event, no trigger, direction none.
    private Task NotifyAutomaticAsync(TValue value, EventReason reason) =>
        OnValueChange.InvokeAsync(new ValueChangeEventArgs<TValue>(value, new TabsChangeEventDetails(reason, TabsActivationDirection.None, EventSnapshot.CreatePlaceholder())));

    // A change of the mirrored direction or of the orientation renders the root; a change of the value does not.
    private void OnContextChanged()
    {
        if (!disposed && UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        Orientation orientation = context!.Orientation;
        TabsActivationDirection direction = context.Direction;
        if (state is { } current && current.Orientation == orientation && current.TabActivationDirection == direction)
        {
            return false;
        }

        state = new TabsRootState(orientation, direction);
        return true;
    }
}
