using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for list navigation: a re-render of <see cref="TestListRoot"/> with unchanged parameters and 100 items,
/// measured around bUnit's synchronous render on the test thread, as the composite root's budget is. It includes Blazor's
/// own per-render cost: the root, its fixed cascade, the reference and floating handles, the floating handle's fixed
/// cascade and the 100 items all render, since each item's child content is a new fragment on every render of the content,
/// as a consumer's markup is. The list's comparison of its options, the handles' and the items' registrations send nothing.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ListNavigationAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-04 (arm64, .NET 11 RC2, Release; M5 track 1, issue 159, PR 170's review): 41440 bytes,
    // least of five, for the parent passing the same values again to a root with its two handles and 100 items, about 410
    // bytes an item, which is Blazor's render of a part and its new child content. The figure is the envelope of 25 full
    // Release runs of this project in the non-parallel AllocationBudgets collection (ADR 16 Amendments 2026-10-27): 41440
    // in 22, 41520, 41552 and 41584 once each. The budget is that envelope with the CI runner's +112 bytes floor shift that
    // InteractionsAllocationBudgetTests records (2026-10-29), as the lifecycle root's budget took it, so 41696. It only
    // tightens.
    private const long RerenderBudgetBytes = 41696;

    private readonly CompositeTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "port: a re-render of a 100-item list with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "CMP-STA-10")]
    public void A_rerender_of_a_100_item_list_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        RenderFragment content = Handles(100);
        Action<ComponentParameterCollectionBuilder<TestListRoot>> parameters = p => p
            .Add(x => x.LoopFocus, true)
            .Add(x => x.Orientation, "vertical")
            .AddChildContent(content);
        IRenderedComponent<TestListRoot> cut = host.Context.Render(parameters);
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(parameters));

        Assert.Empty(host.UpdateOptions);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    // The labels are the consumer's data, made once; each item's child content is new on every render of the content.
    private static RenderFragment Handles(int count)
    {
        string[] labels = [.. Enumerable.Range(0, count).Select(i => "Item " + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        return builder =>
        {
            builder.OpenComponent<TestListHandle>(0);
            builder.AddAttribute(1, nameof(TestListHandle.Role), CompositeListRole.Reference);
            builder.CloseComponent();

            builder.OpenComponent<TestListHandle>(2);
            builder.AddAttribute(3, nameof(TestListHandle.Role), CompositeListRole.Floating);
            builder.AddAttribute(4, nameof(TestListHandle.ChildContent), (RenderFragment)(items =>
            {
                foreach (string label in labels)
                {
                    items.OpenComponent<TestListItem>(5);
                    items.SetKey(label);
                    items.AddAttribute(6, "data-item", label);
                    items.AddAttribute(7, nameof(TestListItem.Label), label);
                    items.AddAttribute(8, nameof(TestListItem.ChildContent), (RenderFragment)(text => text.AddContent(0, label)));
                    items.CloseComponent();
                }
            }));
            builder.CloseComponent();
        };
    }
}
