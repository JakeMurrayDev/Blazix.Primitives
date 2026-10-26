using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TGG-STA-01; toggle-group/ToggleGroup.tsx:83,131-147. ADR 08 §3: the state record is sealed, partial and [PartState]; the
// default projection gives data-disabled, data-multiple and data-orientation, which is always present (TGG-DOM-02).
/// <summary>Represents the state of a toggle group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether every toggle of the group ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Multiple">A value indicating whether any number of toggles may be pressed; projected as <c>data-multiple</c>.</param>
/// <param name="Orientation">The axis of the arrow keys that move focus between the toggles; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ToggleGroupState(bool Disabled, bool Multiple, Orientation Orientation);

// TGG §3.1; toggle-group/ToggleGroupDataAttributes.ts:4,9,13, in its order.
/// <summary>Provides the names of the data attributes a toggle group renders.</summary>
public static class ToggleGroupDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the group is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute that holds the group's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while any number of toggles may be pressed: <c>data-multiple</c>.</summary>
    public const string Multiple = "data-multiple";
}

// Base UI's ToggleGroup (toggle-group/ToggleGroup.tsx); the contract is docs/components/toggle-group.md.
// - One `div` with role="group" and no name, tabindex or aria-orientation of its own (TGG-STR-01, TGG-ARIA-01..03,
//   TGG-DOM-05); the consumer's attributes win (TGG-API-09, TGG-DOM-07).
// - The value is a Controlled<IReadOnlyList<string>> fixed at the first parameter set (ADR 05 Amendments 2026-10-18; ADR 08
//   Amendments 2026-10-22 and 2026-10-26, chosen by the user: the string-array shape, a new list per commit, equality by
//   reference, no ValueExpression). The consumer's list is read as given and never copied (TGG-API-01).
// - The toggles in the subtree read the group through ToggleGroupContext and hand each activation to it with their own
//   details (TGL-EVT-05, TGG-EVT-02): the next list (ToggleGroupValue, with Multiple read at call time, TGG §14), then
//   OnValueChange, then, unless canceled, ValueChanged and the commit (TGG-EVT-01..05).
// - It is the composite root of its toggles (CompositeRootBehavior; TGG §11): the two-valued Orientation, LoopFocus, Home
//   and End on, a handled key stopped, no modifier keys, the toggles' disabled state read from the document (toggle-group §16
//   Q3, pending the user), no highlight mirror and no report; the first toggle holds the initial tab stop (TGG-STA-08,
//   §16 Q2, chosen by the user 2026-10-26). The direction comes from the nearest DirectionProvider only (TGG-EDGE-08). Every
//   toggle's tabindex is the `composite` module's from attach (ADR 04 Amendments 2026-10-22).
// - The consumer's onkeydown is declined and run by the root at `navigate`, under option (i) (TGG-API-09; ADR 04 Amendments
//   2026-10-22, chosen by the user 2026-10-23). Base UI's root onFocus only selects a native text input's value
//   (useCompositeRoot.ts:321-327), which no toggle is, so a consumer onfocus has nothing to veto and is forwarded.
/// <summary>
/// Represents a set of toggles that share one value, the identities of the pressed toggles: Base UI's
/// <c>ToggleGroup</c>, rendered as a <c>div</c> with <c>role="group"</c>. The group is one tab stop, and the arrow keys of
/// its orientation, Home and End move focus between its toggles.
/// </summary>
/// <remarks>
/// By default at most one toggle is pressed and pressing the pressed toggle again leaves none pressed; with
/// <see cref="Multiple"/> each activation adds or removes one identity. A toggle's identity is its <c>Value</c>, or a
/// generated id when it has none.
/// </remarks>
public sealed class ToggleGroup : PartBase<ToggleGroupState>
{
    private const string DefaultTag = "div";

    private static readonly IReadOnlyList<string> s_empty = [];

    private readonly EventCallback onKeyDown;
    private Controlled<IReadOnlyList<string>>? valueState;
    private bool valueInitialized;
    private CompositeRootBehavior? composite;
    private ToggleGroupContext? context;
    private ToggleGroupState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="ToggleGroup"/> class.</summary>
    public ToggleGroup()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ToggleGroup> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // TGG-API-01/02, TGG-STA-02: a null default, or a controlled value removed later, reads as an empty list.
    private IReadOnlyList<string> CurrentValue => valueState!.Value ?? s_empty;

    // TGG-API-04: the toolbar's and the toolbar group's disabled states join this in a toolbar (M3 track 4).
    private bool EffectiveDisabled => Disabled;

    private string? IdParameter => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("id", out object? value)
        && value is string { Length: > 0 } text ? text : null;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // TGG-API-01, TGG-STA-06; ADR 05 §2, ADR 08 Amendments 2026-10-22
    /// <summary>Gets or sets the identities of the pressed toggles, when the consumer owns the value.</summary>
    /// <value>
    /// The value; an array or a list binds. Supplying it at the first render makes the value the consumer's for the
    /// lifetime of the group: an activation then only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>,
    /// each with a new list.
    /// </value>
    [Parameter]
    public IReadOnlyList<string>? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after an activation that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<IReadOnlyList<string>> ValueChanged { get; set; }

    // TGG-API-02
    /// <summary>Gets or sets the identities of the toggles initially pressed, when the group owns the value.</summary>
    /// <value>The value; <see langword="null"/>, the default, is an empty list. Read at the first render only.</value>
    [Parameter]
    public IReadOnlyList<string>? DefaultValue { get; set; }

    // TGG-API-03, TGG-EVT-01..10
    /// <summary>Gets or sets the callback invoked when a toggle of the group is activated, before the value changes.</summary>
    /// <value>
    /// The callback, which receives the proposed value, a new list, and the change details the activated toggle created:
    /// the reason <see cref="EventReason.None"/> and the click. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the
    /// current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<string>>> OnValueChange { get; set; }

    // TGG-API-04
    /// <summary>Gets or sets a value indicating whether every toggle of the group ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable every toggle, whatever its own setting; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // TGG-API-05; ADR 08 Amendments 2026-10-26 (non-nullable, the family default Horizontal)
    /// <summary>Gets or sets the axis of the arrow keys that move focus between the toggles.</summary>
    /// <value>
    /// <see cref="Blazix.Primitives.Orientation.Horizontal"/>, the default, for the left and right arrows (swapped in a
    /// right-to-left <see cref="DirectionProvider"/>); <see cref="Blazix.Primitives.Orientation.Vertical"/> for the up and
    /// down arrows. Reflected in <c>data-orientation</c>.
    /// </value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    // TGG-API-06
    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last toggle to the first and back.</summary>
    /// <value><see langword="true"/>, the default, to wrap; <see langword="false"/> to stop at either end.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    // TGG-API-07
    /// <summary>Gets or sets a value indicating whether any number of toggles may be pressed.</summary>
    /// <value>
    /// <see langword="true"/> to let each activation add or remove one identity; <see langword="false"/>, the default, to
    /// keep at most one toggle pressed. A change keeps the current value as it is.
    /// </value>
    [Parameter]
    public bool Multiple { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToggleGroupState State => state!;

    // TGG-DOM-01 renders no generated id; ADR 02 decision 1: under a substitute callback the composite twin finds the
    // element by id (the RadioGroup and Button precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the composite behavior is attached, so a
    // browser test can wait for its listeners before it presses a key.
    /// <summary>Gets a task that completes when the group's composite behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached it.</value>
    internal Task TwinAttached => composite!.TwinAttached;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // TGG-API-01/02, TGG-STA-02, TGG-EDGE-02/03: ownership is fixed at the first parameter set (ADR 05 Amendments
        // 2026-10-18). TGG-STA-07: the raw parameters of this set, `null` reading as Base UI's `undefined`.
        valueState ??= new Controlled<IReadOnlyList<string>>("ToggleGroup", "value", Logger);
        bool valueSupplied = parameters.TryGetValue(nameof(Value), out IReadOnlyList<string>? value);
        bool defaultValueSupplied = parameters.TryGetValue(nameof(DefaultValue), out IReadOnlyList<string>? defaultValue);
        valueInitialized = value is not null || defaultValue is not null;
        valueState.Update(valueSupplied, value!, defaultValueSupplied, defaultValue!);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
        {
            EnableHomeAndEndKeys = true,
        };
        context = new ToggleGroupContext(CurrentValue, RequestAsync);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        composite!.Orientation = CompositeLiterals.ToLiteral(Orientation);
        composite.LoopFocus = LoopFocus;
        composite.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        UpdateState();
        context!.SetState(CurrentValue, EffectiveDisabled, valueInitialized);

        // The cascade wraps the consumer's content inside the group's element: the group's context, then the composite
        // registry; it is rebuilt only when the consumer supplied other content (the Meter precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(composite.Context.Cascade(cascadedContent));
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return composite!.AfterRenderAsync(Enabled, Element, IdParameter ?? GeneratedId);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            composite?.Dispose();
        }

        base.Dispose(disposing);
    }

    // TGG-ARIA-01..03, TGG-DOM-05: role="group" (the consumer's role wins at the merge), no name, tabindex or aria-orientation.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        composite!.AddAttributes(attributes, IdParameter ?? GeneratedId);
        attributes.Set("role", "group");
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    // TGG §7, TGG-EVT-01..05: the group's half of a toggle's request, with the toggle's details. OnValueChange runs before the
    // commit, and a cancel stops it; the toggles whose state changed render as they are notified, and the group does not.
    private async Task RequestAsync(string identity, bool nextPressed, ChangeEventDetails details)
    {
        IReadOnlyList<string> next = ToggleGroupValue.Next(CurrentValue, identity, nextPressed, Multiple);
        if (!await valueState!.NotifyAsync(next, details, OnValueChange))
        {
            return;
        }

        await valueState.CommitAsync(next, ValueChanged);
        context!.SetState(CurrentValue, EffectiveDisabled, valueInitialized);
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool disabled = EffectiveDisabled;
        if (state is { } current && current.Disabled == disabled && current.Multiple == Multiple && current.Orientation == Orientation)
        {
            return false;
        }

        state = new ToggleGroupState(disabled, Multiple, Orientation);
        return true;
    }
}
