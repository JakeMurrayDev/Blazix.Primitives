using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

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
// - Not here yet (open until track 1's E2 is on `main`): the Tab registry's order (TAB-STA-04/05), the automatic selection
//   and its notification (TAB-STA-05..12, TAB-EVT-04).
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
    private TabsRootContext<TValue>? context;
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

    // TAB-API-01/02: a controlled root has a value only while Value is supplied; an uncontrolled one from its default, or
    // from its first commit when it started with the implicit default.
    private bool HasSelection => valueState!.IsControlled ? valueSupplied : !implicitDefault || selectedOnce;

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
            implicitDefault = !parameters.TryGetValue<TValue>(nameof(Value), out _) && !parameters.TryGetValue<TValue>(nameof(DefaultValue), out _);
        }

        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        valueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        context = new TabsRootContext<TValue>(valueState!.IsControlled, RequestAsync);
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
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveListener(contextChanged);
        }

        base.Dispose(disposing);
    }

    // ADR 04 Amendments 2026-10-18 and 2026-10-26: the handover. The direction the element is created with is rendered on
    // every render, so Blazor never writes the attribute the module owns from attach.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
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
        }

        PublishValue();
        return true;
    }

    private void PublishValue() => context!.SetValue(valueState!.Value, HasSelection, implicitDefault && !selectedOnce);

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
