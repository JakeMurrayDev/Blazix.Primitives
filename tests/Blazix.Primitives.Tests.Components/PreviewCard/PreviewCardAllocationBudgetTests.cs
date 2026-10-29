using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the preview card root (issue 122): the least of five re-renders of a closed
/// <see cref="PreviewCardRoot{TPayload}"/> and its trigger with unchanged parameters, measured around bUnit's synchronous
/// render on the test thread, as Tooltip's budget is. It includes Blazor's own per-render cost; the root's own share is its
/// cascades, and the trigger's the interactions options it builds after a render, whose comparison sends nothing.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class PreviewCardAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-29 (arm64, .NET 11 RC2, Release): 2008 bytes, the test alone and in 8 full runs of the project
    // alike. The root and its fixed cascades render, the harness rebuilds the one parameter expression, and the session's
    // and the trigger's comparisons send nothing. The budget adds the 112 bytes by which Tooltip's floor moved in full runs
    // (TooltipAllocationBudgetTests), the state earlier tests leave in the process.
    private const long RerenderBudgetBytes = 2120;

    private readonly PreviewCardTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a re-render of a preview card root and its trigger with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<PreviewCardTrigger<NoPayload>>(0);
            builder.AddAttribute(1, "id", "trigger");
            builder.CloseComponent();
        };
        Action<ComponentParameterCollectionBuilder<PreviewCardRoot<NoPayload>>> parameters = p => p.Add(x => x.ChildContent, content);
        IRenderedComponent<PreviewCardRoot<NoPayload>> cut = host.Context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        int updates = host.InteractionsUpdates.Count;
        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(parameters));

        Assert.Equal(updates, host.InteractionsUpdates.Count);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }
}
