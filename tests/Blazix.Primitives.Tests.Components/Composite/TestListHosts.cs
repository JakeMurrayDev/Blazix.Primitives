using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// The test-only list hosts at L2 (M5 track 1, issue 159, Deliverable 12): a store-agnostic root that composes the list
// behaviour over a recorded request delegate, a part that is a composite root in list mode in one role, and a list item
// whose state carries the Highlighted mirror, so render counts are read through ClassValue (the lesson of issue 110).

/// <summary>A test-only list root: no element; it owns a <see cref="ListNavigationBehavior"/> and cascades its context.</summary>
internal sealed class TestListRoot : ComponentBase, IDisposable
{
    private ListNavigationBehavior? list;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Completes when the session's handle is attached; complete by default.</summary>
    [Parameter]
    public Task? SessionReady { get; set; }

    /// <summary>The family's open-change sequence; records only by default.</summary>
    [Parameter]
    public Func<OpenChangeRequest, Task>? OnRequest { get; set; }

    [Parameter]
    public bool LoopFocus { get; set; }

    [Parameter]
    public string Orientation { get; set; } = "vertical";

    [Parameter]
    public bool Virtual { get; set; }

    [Parameter]
    public int? SelectedIndex { get; set; }

    [Parameter]
    public IReadOnlyList<string?>? TypeaheadLabels { get; set; }

    public ListNavigationBehavior List => list!;

    public List<OpenChangeRequest> Requests { get; } = [];

    public void Dispose() => list?.Dispose();

    protected override void OnInitialized()
    {
        list = new ListNavigationBehavior(
            "test-session",
            SessionReady ?? Task.CompletedTask,
            request =>
            {
                Requests.Add(request);
                return OnRequest?.Invoke(request) ?? Task.CompletedTask;
            },
            exception => throw exception);
    }

    protected override void OnParametersSet()
    {
        list!.LoopFocus = LoopFocus;
        list.Orientation = Orientation;
        list.Virtual = Virtual;
        list.SelectedIndex = SelectedIndex;
        list.SetTypeahead(TypeaheadLabels is not null, 750, null, TypeaheadLabels);
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = list.Context.Cascade(cascadedContent);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, cascade);

    protected override Task OnAfterRenderAsync(bool firstRender) => list!.FlushAsync();
}

/// <summary>A test-only list handle: a <c>div</c> that composes a composite root in list mode in a role.</summary>
internal sealed class TestListHandle : PartBase<EmptyState>
{
    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? behavior;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    public TestListHandle()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ListNavigationContext? ListContext { get; set; }

    [Parameter]
    public CompositeListRole Role { get; set; } = CompositeListRole.Floating;

    [Parameter]
    public bool ReportItems { get; set; }

    public CompositeRootBehavior Behavior => behavior!;

    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized()
    {
        behavior = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown);
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        behavior!.SetList(ListContext, Role);
        behavior.ReportItems = ReportItems;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        if (Role == CompositeListRole.Floating && (cascade is null || !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = behavior.Context.Cascade(cascadedContent);
        }

        if (Role == CompositeListRole.Floating)
        {
            ChildContent = cascade;
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Enabled, Element, Id);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, Id);

    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}

/// <summary>The state of <see cref="TestListItem"/>: the Highlighted mirror, whose attribute the module writes.</summary>
/// <param name="Highlighted">Whether the module's highlight is on the item.</param>
[PartState]
internal sealed partial record TestListItemState([property: MirroredField] bool Highlighted);

/// <summary>A test-only list item: a <c>div</c> that composes <see cref="CompositeItemBehavior"/> with the Highlighted mirror.</summary>
internal sealed class TestListItem : PartBase<TestListItemState>
{
    private static readonly TestListItemState s_highlighted = new(true);
    private static readonly TestListItemState s_plain = new(false);
    private readonly EventCallback onKeyDown;
    private CompositeItemBehavior? behavior;

    public TestListItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [CascadingParameter]
    private CompositeRootContext? RootContext { get; set; }

    [Parameter]
    public string? Label { get; set; }

    public bool Highlighted => behavior!.Highlighted;

    protected override string DefaultTagName => "div";

    protected override TestListItemState State => behavior!.Highlighted ? s_highlighted : s_plain;

    protected override bool UsesGeneratedId => true;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        behavior = new CompositeItemBehavior(RootContext, nameof(TestListItem), onKeyDown, StateHasChanged);
    }

    protected override void OnParametersSet()
    {
        behavior!.SetConsumerAttributes(AdditionalAttributes);
        behavior.SetRegistration(Id, AdditionalAttributes, label: Label);
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
