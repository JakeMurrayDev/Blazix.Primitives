using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only composite item: a <c>div</c> that composes <see cref="CompositeItemBehavior"/> as RadioRoot (M2) and the
/// M3 items will. It registers its id, generated or the consumer's, with the nearest <see cref="TestCompositeRoot"/>.
/// </summary>
internal sealed class TestCompositeItem : PartBase<EmptyState>
{
    private readonly EventCallback onKeyDown;
    private CompositeItemBehavior? behavior;

    public TestCompositeItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [CascadingParameter]
    private CompositeRootContext? RootContext { get; set; }

    /// <summary>The tag of the element; <c>div</c> by default.</summary>
    [Parameter]
    public string Tag { get; set; } = "div";

    /// <summary>The item's <c>disabled</c> flag, sent in its registration.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The item's <c>focusableWhenDisabled</c> flag, sent in its registration.</summary>
    [Parameter]
    public bool FocusableWhenDisabled { get; set; }

    public string? ResolvedId => Id;

    public int Renders { get; private set; }

    /// <summary>Requests a render with the current parameters.</summary>
    public void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override string DefaultTagName => Tag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        behavior = new CompositeItemBehavior(RootContext, nameof(TestCompositeItem), onKeyDown);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetConsumerAttributes(AdditionalAttributes);
        behavior.SetRegistration(Id, AdditionalAttributes, Disabled, FocusableWhenDisabled);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(Element);
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
        Renders++;
        behavior!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }
}
