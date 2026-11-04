using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Menu: a re-render with unchanged parameters (the parent passing the same values again) of a closed
/// root with its trigger, portal, positioner, popup and items, measured around bUnit's synchronous render on the test thread.
/// It includes Blazor's own per-render cost of the root and its fixed cascades.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class MenuAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-05 (arm64, .NET 11 RC2, Release; M5 track 6): 1360 bytes in 3 of 3 runs, the least of five
    // (AllocationSampler.LeastOfFive), for the parent passing the same child content again to a closed root with a trigger, a
    // portal and two items. The root and its fixed cascades render; the trigger and the portal, whose parameters are the same
    // fragments, are skipped by Blazor's change detection. The budget is the envelope plus 112 B (the brief's), and only
    // tightens afterwards.
    private const long RerenderBudgetBytes = 1472;

    private readonly BunitContext context = new();

    public MenuAllocationBudgetTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a re-render of a closed root with its parts and unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        RenderFragment content = Content;
        Action<ComponentParameterCollectionBuilder<MenuRoot<NoPayload>>> parameters = p => p.AddChildContent(content);
        IRenderedComponent<MenuRoot<NoPayload>> cut = context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(parameters));

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    private static void Content(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuTrigger<NoPayload>>(0);
        builder.CloseComponent();
        builder.OpenComponent<MenuPortal>(1);
        builder.AddComponentParameter(2, nameof(MenuPortal.ChildContent), (RenderFragment)Portal);
        builder.CloseComponent();
    }

    private static void Portal(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuPositioner>(0);
        builder.AddComponentParameter(1, nameof(MenuPositioner.ChildContent), (RenderFragment)Positioner);
        builder.CloseComponent();
    }

    private static void Positioner(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuPopup>(0);
        builder.AddComponentParameter(1, nameof(MenuPopup.ChildContent), (RenderFragment)Popup);
        builder.CloseComponent();
    }

    private static void Popup(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuItem>(0);
        builder.CloseComponent();
        builder.OpenComponent<MenuItem>(1);
        builder.CloseComponent();
    }
}
