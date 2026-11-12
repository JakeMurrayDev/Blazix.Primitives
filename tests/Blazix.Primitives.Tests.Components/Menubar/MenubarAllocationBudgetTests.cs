using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Menubar (M5 track 8's brief, Tests first, L2): a flag flip, a bar menu reported open and then closed
/// by another reason, which re-renders the bar and its two triggers twice, measured around the renderer's dispatch.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class MenubarAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-11 (arm64, .NET 11 RC2, Release; M5 track 8): 101776 bytes in 3 of 3 runs, the least of five
    // (AllocationSampler.LeastOfFive), for one open and one close reported to the bar. It includes the bar's and both
    // triggers' renders, each trigger's interactions `update` with its serialized hover and focus options, and bUnit's
    // loose-mode record of those invocations. The budget is the envelope plus 112 B (the brief's), and only tightens
    // afterwards.
    private const long FlipBudgetBytes = 101888;

    private readonly BunitContext context = new();

    public MenubarAllocationBudgetTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a flag flip of a bar with two menus stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_flag_flip_stays_within_its_budget()
    {
        IRenderedComponent<MenubarRenderHost> cut = context.Render<MenubarRenderHost>();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        await cut.InvokeAsync(() => cut.Instance.File!.CloseAsync());
        MenuRootContext menu = cut.Instance.File!.Behavior.Context;
        IMenuParentContext bar = menu.Parent.Context!;
        // bUnit's dispatcher runs the flip inline on the test thread, renders included; a flip that did not finish there
        // would measure nothing, so it fails instead.
        void Flip()
        {
            Task flip = cut.InvokeAsync(() =>
            {
                bar.OnMenuOpenChange(menu, true, EventReason.TriggerPress);
                bar.OnMenuOpenChange(menu, false, EventReason.ItemPress);
            });
            Assert.True(flip.IsCompletedSuccessfully);
        }

        for (int i = 0; i < 20; i++)
        {
            Flip();
        }

        long allocated = AllocationSampler.LeastOfFive(Flip);

        Assert.True(allocated <= FlipBudgetBytes, $"a flag flip allocated {allocated} bytes; the budget is {FlipBudgetBytes}");
    }
}
