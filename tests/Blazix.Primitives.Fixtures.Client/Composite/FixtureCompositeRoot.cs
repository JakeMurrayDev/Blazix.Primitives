using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// A test-only composite root for the <c>composite-*</c> fixtures (tests/browser/composite.spec.ts), the counterpart of
/// <c>TestCompositeRoot</c> in the L2 project: a <c>div</c> composing <see cref="CompositeRootBehavior"/> with every option
/// as a parameter, because Base UI's <c>CompositeRoot</c> is internal and no public root exists before RadioGroup.
/// </summary>
public sealed class FixtureCompositeRoot : PartBase<EmptyState>
{
    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? behavior;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    public FixtureCompositeRoot()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    [Parameter]
    public Orientation? Orientation { get; set; }

    [Parameter]
    public bool LoopFocus { get; set; } = true;

    [Parameter]
    public bool EnableHomeAndEndKeys { get; set; }

    [Parameter]
    public bool StopEventPropagation { get; set; } = true;

    [Parameter]
    public IReadOnlyList<string>? ModifierKeys { get; set; }

    [Parameter]
    public bool SelectOnNavigation { get; set; }

    [Parameter]
    public bool ReportFocusLeave { get; set; }

    [Parameter]
    public bool ReportHighlight { get; set; }

    [Parameter]
    public EventCallback<int> OnHighlightChange { get; set; }

    [Parameter]
    public EventCallback<EventSnapshot> OnFocusLeave { get; set; }

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
        behavior.ModifierKeys = ModifierKeys ?? [];
        behavior.SelectOnNavigation = SelectOnNavigation;
        behavior.ReportFocusLeave = ReportFocusLeave;
        behavior.ReportHighlight = ReportHighlight;
        behavior.SetDirectionContext(DirectionContext);
        behavior.SetConsumerAttributes(AdditionalAttributes);

        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = behavior.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

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
