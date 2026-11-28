using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless.Tests.Components.Rendering;

/// <summary>
/// ADR 16 rule 7 at L2: the steady-state render of a part (nothing changed, every merge memoised) measured around
/// bUnit's synchronous re-render on the test thread, which includes Blazor's own per-render cost (parameter views,
/// the diff). The budget was taken from the first measurement and only tightens afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class AllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-20 (arm64, .NET 11 RC2, Release): 256 bytes on both paths, all of it the
    // dispatcher's and the renderer's own per-render work; the pipeline and the frame rewrite add nothing in
    // steady state (lists, scratch builder and merges are reused or memoised). Tightened 2026-11-28 (issue 57; ADR 16
    // Amendments 2026-11-28) to 224 bytes on both paths, the least of five in three processes: the 32 bytes were the box of
    // the slot's EventCallback that matching the part's created handler cost per render.
    private const long DefaultElementRenderBudgetBytes = 224;
    private const long SubstituteElementRenderBudgetBytes = 224;

    private readonly BunitContext context = new();

    public AllocationBudgetTests()
    {
        context.Services.AddIodineHeadless();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a steady-state render of the default element stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_steady_state_render_of_the_default_element_stays_within_its_budget()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("class", "consumer")
            .AddUnmatched("style", "margin: 0")
            .AddUnmatched("data-testid", "root")
            .Add(x => x.ClassValue, (Func<SampleRootState, string?>)(state => state.Open ? "open" : null)));

        long allocated = Measure(cut);

        AllocationSampler.AssertWithinBudget(allocated, DefaultElementRenderBudgetBytes, "a render");
    }

    [Fact(DisplayName = "a steady-state render of a substitute element stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_steady_state_render_of_a_substitute_element_stays_within_its_budget()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("class", "consumer")
            .AddUnmatched("data-testid", "root")
            .Add(x => x.Render, (Microsoft.AspNetCore.Components.RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "substitute");
                builder.AddAttribute(2, "data-slot", "render");
                builder.AddContent(3, "text");
                builder.CloseElement();
            })));

        long allocated = Measure(cut);

        AllocationSampler.AssertWithinBudget(allocated, SubstituteElementRenderBudgetBytes, "a render");
    }

    private static long Measure(IRenderedComponent<SampleRoot> cut)
    {
        SampleRoot part = cut.Instance;
        Action refresh = part.Refresh;
        for (int i = 0; i < 20; i++)
        {
            cut.InvokeAsync(refresh);
        }

        return AllocationSampler.LeastOfFive(() => cut.InvokeAsync(refresh));
    }
}
