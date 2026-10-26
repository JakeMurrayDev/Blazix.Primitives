using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only composite root (the <see cref="SampleRoot"/> precedent): a <c>div</c> that composes
/// <see cref="CompositeRootBehavior"/> as a public root part will (RadioGroup in M2, ToggleGroup, Toolbar and Tabs in M3),
/// with every option of the behaviour as a parameter. Base UI's <c>CompositeRoot</c> is internal (CMP §3), so the CMP
/// rows no public part can host yet run on this part.
/// </summary>
internal sealed class TestCompositeRoot : PartBase<EmptyState>
{
    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? behavior;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    public TestCompositeRoot()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <summary>The arrow keys that move the highlight; <see langword="null"/> is both axes.</summary>
    [Parameter]
    public Blazix.Primitives.Orientation? Orientation { get; set; }

    [Parameter]
    public bool LoopFocus { get; set; } = true;

    [Parameter]
    public bool EnableHomeAndEndKeys { get; set; }

    [Parameter]
    public bool StopEventPropagation { get; set; } = true;

    [Parameter]
    public bool HighlightItemOnHover { get; set; }

    [Parameter]
    public IReadOnlyList<string>? ModifierKeys { get; set; }

    [Parameter]
    public IReadOnlyList<int>? DisabledIndices { get; set; }

    [Parameter]
    public bool DeriveDisabledIndices { get; set; }

    [Parameter]
    public bool SelectOnNavigation { get; set; }

    [Parameter]
    public bool ReportFocusLeave { get; set; }

    [Parameter]
    public bool ReportHighlight { get; set; }

    /// <summary>The part's own hook on the focus-driven selection, as RadioRoot's consumer focus handler will be.</summary>
    [Parameter]
    public Func<EventSnapshot, DecisionPolicy>? SelectionDecider { get; set; }

    [Parameter]
    public EventCallback<int> OnHighlightChange { get; set; }

    [Parameter]
    public EventCallback<EventSnapshot> OnFocusLeave { get; set; }

    public CompositeRootBehavior Behavior => behavior!;

    public int HighlightedIndex => behavior!.HighlightedIndex;

    public Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown);
        behavior.HighlightChanged += index => _ = OnHighlightChange.InvokeAsync(index);
        behavior.FocusLeft += snapshot => _ = OnFocusLeave.InvokeAsync(snapshot);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Orientation = CompositeLiterals.ToLiteral(Orientation);
        behavior.LoopFocus = LoopFocus;
        behavior.EnableHomeAndEndKeys = EnableHomeAndEndKeys;
        behavior.StopEventPropagation = StopEventPropagation;
        behavior.HighlightItemOnHover = HighlightItemOnHover;
        behavior.ModifierKeys = ModifierKeys ?? [];
        behavior.DisabledIndices = DisabledIndices;
        behavior.DeriveDisabledIndices = DeriveDisabledIndices;
        behavior.SelectOnNavigation = SelectOnNavigation;
        behavior.ReportFocusLeave = ReportFocusLeave;
        behavior.ReportHighlight = ReportHighlight;
        behavior.SelectionDecider = SelectionDecider;
        behavior.SetDirectionContext(DirectionContext);
        behavior.SetConsumerAttributes(AdditionalAttributes);

        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = behavior.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <summary>Requests a render with the current parameters, for the update and allocation tests.</summary>
    public void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(Enabled, Element, Id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, Id);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }
}
