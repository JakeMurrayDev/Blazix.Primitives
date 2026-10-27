using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// A test-only composite item for the <c>composite-*</c> fixtures, the counterpart of <c>TestCompositeItem</c> in the L2
/// project: an element of <see cref="Tag"/> composing <see cref="CompositeItemBehavior"/>.
/// </summary>
public sealed class FixtureCompositeItem : PartBase<EmptyState>
{
    private readonly EventCallback onKeyDown;
    private CompositeItemBehavior? behavior;

    public FixtureCompositeItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [CascadingParameter]
    private CompositeRootContext? RootContext { get; set; }

    [Parameter]
    public string Tag { get; set; } = "div";

    /// <summary>The item's <c>disabled</c> flag, sent in its registration.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The item's <c>focusableWhenDisabled</c> flag, sent in its registration.</summary>
    [Parameter]
    public bool FocusableWhenDisabled { get; set; }

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
        behavior = new CompositeItemBehavior(RootContext, nameof(FixtureCompositeItem), onKeyDown);
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
        behavior!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }
}
