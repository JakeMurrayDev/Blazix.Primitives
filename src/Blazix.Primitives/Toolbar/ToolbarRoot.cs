using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TLB-STA-01; toolbar/root/ToolbarRoot.tsx:56,88-91. ADR 08 §3: the state record is sealed, partial and [PartState]; the
// default projection gives data-disabled and data-orientation, which is always present (TLB-DOM-01).
/// <summary>Represents the state of a toolbar, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the toolbar's buttons and inputs ignore user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The axis of the arrow keys that move focus between the toolbar's items; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ToolbarRootState(bool Disabled, Orientation Orientation);

// TLB §3.1; toolbar/root/ToolbarRootDataAttributes.ts:4,9, in its order.
/// <summary>Provides the names of the data attributes a toolbar renders.</summary>
public static class ToolbarRootDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the toolbar is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute that holds the toolbar's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Toolbar.Root (toolbar/root/ToolbarRoot.tsx); the contract is docs/components/toolbar.md.
// - One `div` with role="toolbar" and aria-orientation always (TLB-ARIA-01/02), no tabindex (TLB-DOM-02); the consumer's
//   attributes win (TLB-API-05, TLB-DOM-11). `dir` is forwarded and not read: the direction comes from the nearest
//   DirectionProvider only (TLB-EDGE-06).
// - It is the composite root of every item in its subtree, buttons, links, inputs and the toggles of a toggle group, in
//   document order (TLB-STR-01; CompositeRootBehavior, TLB §11): the two-valued Orientation, LoopFocus, Home and End off
//   (TLB-KEY-04, §16 Q2, chosen by the user 2026-10-26), a handled key stopped, no modifier keys, no highlight mirror. The
//   items' tabindex is the `composite` module's from attach (ADR 04 Amendments 2026-10-22).
// - It cascades ToolbarRootContext (design point 14.4 (a), pending the user), then the composite registry, both fixed.
// - The consumer's onkeydown is declined and run by the root at `navigate`, under option (i) (TLB-API-05; ADR 04
//   Amendments 2026-10-22, chosen by the user 2026-10-23). Base UI's root onFocus only selects a native text input's value
//   (useCompositeRoot.ts:321-327), which the module does in its focusin listener with no decision point, so a consumer
//   onfocus is forwarded and cannot veto it (design point 14.6 (a), pending the user).
/// <summary>
/// Represents a container for a set of controls: Base UI's <c>Toolbar.Root</c>, rendered as a <c>div</c> with
/// <c>role="toolbar"</c>. The toolbar is one tab stop, and the arrow keys of its orientation move focus between its
/// buttons, links, inputs and the toggles of a toggle group.
/// </summary>
/// <remarks>
/// <see cref="Disabled"/> disables every <c>ToolbarButton</c> and <c>ToolbarInput</c> in the toolbar, and
/// every toggle of a <see cref="ToggleGroup"/> in it; a <see cref="ToolbarLink"/> is never disabled. Give the toolbar an
/// accessible name with an <c>aria-label</c> or <c>aria-labelledby</c> attribute.
/// </remarks>
public sealed class ToolbarRoot : PartBase<ToolbarRootState>
{
    private const string DefaultTag = "div";

    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? composite;
    private ToolbarRootContext? context;
    private ToolbarRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="ToolbarRoot"/> class.</summary>
    public ToolbarRoot()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private string? IdParameter => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("id", out object? value)
        && value is string { Length: > 0 } text ? text : null;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // TLB-API-01, TLB-STA-05
    /// <summary>Gets or sets a value indicating whether the toolbar's buttons and inputs ignore user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable every button, input and toggle group in the toolbar, whatever their own setting;
    /// links are unaffected. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // TLB-API-02; ADR 08 Amendments 2026-10-26 (non-nullable, the family default Horizontal, chosen by the user)
    /// <summary>Gets or sets the axis of the arrow keys that move focus between the toolbar's items.</summary>
    /// <value>
    /// <see cref="Blazix.Primitives.Orientation.Horizontal"/>, the default, for the left and right arrows (swapped in a
    /// right-to-left <see cref="DirectionProvider"/>); <see cref="Blazix.Primitives.Orientation.Vertical"/> for the up and
    /// down arrows. Reflected in <c>aria-orientation</c> and <c>data-orientation</c>, and in every part's state.
    /// </value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    // TLB-API-03
    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last item to the first and back.</summary>
    /// <value><see langword="true"/>, the default, to wrap; <see langword="false"/> to stop at either end.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToolbarRootState State => state!;

    // TLB-DOM-01 renders no generated id; ADR 02 decision 1: under a substitute callback the composite twin finds the
    // element by id (the ToggleGroup and RadioGroup precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the composite behavior is attached, so a
    // browser test can wait for its listeners before it presses a key.
    /// <summary>Gets a task that completes when the toolbar's composite behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached it.</value>
    internal Task TwinAttached => composite!.TwinAttached;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // TLB-KEY-04: Home and End are not the toolbar's (enableHomeAndEndKeys is false, useCompositeRoot.ts:79).
        composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
        {
            EnableHomeAndEndKeys = false,
        };
        context = new ToolbarRootContext(Disabled, Orientation);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        composite!.Orientation = CompositeLiterals.ToLiteral(Orientation);
        composite.LoopFocus = LoopFocus;
        composite.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        UpdateState();
        context!.SetState(Disabled, Orientation);

        // The cascade wraps the consumer's content inside the toolbar's element: the toolbar's context, then the composite
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

    // TLB-ARIA-01/02, TLB-DOM-02: role="toolbar" and aria-orientation (the consumer's win at the merge), no tabindex.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        composite!.AddAttributes(attributes, IdParameter ?? GeneratedId);
        attributes.Set("role", "toolbar");
        attributes.Set("aria-orientation", CompositeLiterals.ToLiteral(Orientation));
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    // A new record only when the state changed (ADR 16 rule 7).
    private void UpdateState()
    {
        if (state is { } current && current.Disabled == Disabled && current.Orientation == Orientation)
        {
            return;
        }

        state = new ToolbarRootState(Disabled, Orientation);
    }
}
