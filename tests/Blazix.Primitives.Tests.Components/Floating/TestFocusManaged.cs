using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only focus-managed popup over <see cref="FocusManagerBehavior"/> (ADR 16 rule 10): a <c>div</c> with the Popup
/// parts' focusable props and <c>data-open</c> while <see cref="Open"/>, the inside guards the behavior renders around it,
/// and the inputs sent from the parameter set.
/// </summary>
internal sealed class TestFocusManaged : PartBase<EmptyState>
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [Parameter]
    public bool Open { get; set; } = true;

    [Parameter]
    public bool Modal { get; set; } = true;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool CloseOnFocusOut { get; set; } = true;

    [Parameter]
    public FocusTargetValue? InitialFocus { get; set; }

    [Parameter]
    public FocusTargetValue? ReturnFocus { get; set; }

    [Parameter]
    public InteractionType? OpenInteractionType { get; set; } = InteractionType.Unknown;

    [Parameter]
    public EventSnapshot? CloseEvent { get; set; }

    [Parameter]
    public string? DomReferenceId { get; set; }

    [Parameter]
    public ElementReference? PreviousFocusableElement { get; set; }

    [Parameter]
    public ElementReference? NextFocusableElement { get; set; }

    [Parameter]
    public IReadOnlyList<ElementReference>? InsideElements { get; set; }

    [Parameter]
    public OpenChangeRelay? Relay { get; set; }

    [Parameter]
    public Func<EventSnapshot, Task>? OnFocusOutClose { get; set; }

    public FocusManagerBehavior Behavior { get; private set; } = null!;

    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;

    protected override bool UsesGeneratedId => true;

    protected override void OnInitialized()
    {
        Behavior = new FocusManagerBehavior(JSRuntime, InvokeAsync, snapshot => OnFocusOutClose?.Invoke(snapshot) ?? Task.CompletedTask);
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        Behavior.Portal = Portal;
        Behavior.SetInputs(Inputs());
    }

    public FocusManagerInputs Inputs() => new()
    {
        Open = Open,
        Modal = Modal,
        Disabled = Disabled,
        CloseOnFocusOut = CloseOnFocusOut,
        InitialFocus = InitialFocus,
        ReturnFocus = ReturnFocus,
        OpenInteractionType = OpenInteractionType,
        CloseEvent = CloseEvent,
        DomReferenceId = DomReferenceId,
        PreviousFocusableElement = PreviousFocusableElement,
        NextFocusableElement = NextFocusableElement,
        InsideElements = InsideElements,
        Relay = Relay,
    };

    protected override Task OnAfterRenderAsync(bool firstRender) => Behavior.AfterRenderAsync(Element, Id);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ = Behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "dialog");
        attributes.Set("tabindex", "-1");
        attributes.Set("data-base-ui-focusable", string.Empty);
        if (Open)
        {
            attributes.Set("data-open", string.Empty);
        }
    }

    protected override void BuildBeforeElement(RenderTreeBuilder builder) => Behavior.BuildBeforeElement(builder);

    protected override void BuildAfterElement(RenderTreeBuilder builder) => Behavior.BuildAfterElement(builder);
}
