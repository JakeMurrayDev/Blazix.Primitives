using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Menu's filter mode (M5 track 12, the final review's fix 6): a re-render with unchanged parameters of a
/// <see cref="MenuFilterProvider"/> (the parent passing the same child content again) around a closed filterable root with
/// its trigger, portal, positioner, popup, input, list and two items, measured around bUnit's synchronous render on the test
/// thread. It includes Blazor's own per-render cost of the provider and its fixed cascade.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class MenuFilterAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-12 (arm64, .NET 11, Release; M5 track 12): 3464 bytes in 5 of 5 isolated runs, the least
    // of five (AllocationSampler.LeastOfFive), for the parent passing the same child content again. The budget is the
    // envelope plus 112 B (the brief's), and only tightens afterwards.
    private const long RerenderBudgetBytes = 3576;

    private readonly BunitContext context = new();

    public MenuFilterAllocationBudgetTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a re-render of a MenuFilterProvider with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        RenderFragment content = Content;
        Action<ComponentParameterCollectionBuilder<MenuFilterProvider>> parameters = p => p.Add(x => x.ChildContent, content);
        IRenderedComponent<MenuFilterProvider> cut = context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(parameters));

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    private static void Content(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuRoot<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(MenuRoot<NoPayload>.ChildContent), (RenderFragment)Root);
        builder.CloseComponent();
    }

    private static void Root(RenderTreeBuilder builder)
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
        builder.OpenComponent<MenuInput>(0);
        builder.CloseComponent();
        builder.OpenComponent<MenuList>(1);
        builder.AddComponentParameter(2, nameof(MenuList.ChildContent), (RenderFragment)Items);
        builder.CloseComponent();
    }

    private static void Items(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuItem>(0);
        builder.CloseComponent();
        builder.OpenComponent<MenuItem>(1);
        builder.CloseComponent();
    }
}
