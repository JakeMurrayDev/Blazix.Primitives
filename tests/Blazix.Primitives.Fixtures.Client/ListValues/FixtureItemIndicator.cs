using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.ListValuesFixtures;

/// <summary>
/// The fixtures' copy of the test-only item indicator (tests/Blazix.Primitives.Tests.Components/ListValues/TestItemIndicator.cs; RND-STR-10, API-12): a <c>span</c> over <see cref="ItemIndicatorBehavior"/>, as the sealed
/// <c>Select.ItemIndicator</c> and <c>Combobox.ItemIndicator</c> shells compose it in wave 2. <see cref="Selected"/> stands
/// in for the family item's selection, which a shell reads from its item context.
/// </summary>
internal sealed class FixtureItemIndicator : PartBase<ItemIndicatorState>
{
    private ItemIndicatorBehavior? behavior;
    private ItemIndicatorState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool Selected { get; set; }

    [Parameter]
    public bool KeepMounted { get; set; }

    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override ItemIndicatorState State => state ??= new ItemIndicatorState(behavior!.Selected, behavior.TransitionStatus);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ItemIndicatorBehavior(JSRuntime, action => InvokeAsync(action), () =>
        {
            state = null;
            StateHasChanged();
        });
        behavior.Initialize(Selected, KeepMounted);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Update(Selected, KeepMounted);
        state = null;
        ChildContent ??= ItemIndicatorBehavior.DefaultContent;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!behavior!.Shown)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(behavior.Shown ? Element : null);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }
}
