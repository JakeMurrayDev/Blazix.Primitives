using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rules 2 and 7 for NavigationMenu: the parts a hover switch renders, counted through their class functions, and a
/// re-render with unchanged parameters of a Root with its List, two Items with Triggers, Icons and Contents, a Portal, a
/// Positioner, a Popup and a Viewport, which sends nothing to the modules. Measured around bUnit's synchronous work on the
/// test thread; the budget is the least of five runs plus the envelope dynamic PGO adds in some processes (144 bytes on
/// CI, ADR 16 Amendments 2026-11-10).
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class NavigationMenuAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-04 (arm64, .NET 11, Release), the least of five, the same in six runs: 11848 bytes. The Root
    // and every mounted part render again (the Root, the List, the Portal, the Positioner, the Popup, the Viewport, both
    // Triggers and Icons, the open Content), since each one's parameters hold a fragment or a delegate, which Blazor
    // counts as changed; the twins compare their options and send nothing. The budget adds the 112 bytes of the full-run
    // envelope (TabsAllocationBudgetTests' note of 2026-10-28).
    // Re-pinned 2026-11-10 (issue-112 rule; ADR 16 Amendments 2026-11-10, chosen by the user 2026-11-10): CI run
    // 42769822221 measured 11992. The floor varies per process with dynamic PGO, not with the code: locally, Release,
    // 11848 in 9 of 10 isolated runs and 11960 in 1 (all five samples equal in each); with DOTNET_TieredPGO=0, 11848 in 8 of
    // 8; with tiering off, 11584 in 8 of 8; inside three full L2 runs, 11848. The envelope is CI's observed +144 (the same
    // +144 as Combobox's budget in CI run 42769069830).
    private const long RerenderBudgetBytes = 11992;

    private readonly NavigationMenuTestContext test = new();
    private readonly Dictionary<string, int> renders = [];
    private RenderFragment? menu;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // NavigationMenuRoot.test.tsx has no render-count title; the Items notify only their own parts (NAV-STA-08). A switch
    // renders the two Triggers and their Contents once; their Icons twice, for their Item's change and as the Trigger's
    // children; the Positioner and the Viewport once for the new active trigger they follow; the Popup once and the
    // Viewport once more as the Positioner's children (Blazor renders a child whose parameters include a fragment). The
    // Root, the List, the Items and the Portal do not render.
    [Fact(DisplayName = "port: a hover switch renders the Triggers, Icons and Contents of the two Items and the Positioner's subtree, and not the Root, the List, the Items or the Portal")]
    [Trait("Requirement", "ADR16-2")]
    [Trait("Requirement", "NAV-STA-08")]
    public async Task A_hover_switch_renders_only_the_parts_whose_state_changed()
    {
        IRenderedComponent<NavigationMenuRoot<string>> cut = RenderMenu();
        NavigationMenuTrigger first = TriggerOf(cut, "trigger-item-1");
        NavigationMenuTrigger second = TriggerOf(cut, "trigger-item-2");
        string json = $$"""{"reason":"trigger-hover","direction":null,"event":{{NavigationMenuTestContext.MouseEnter}}}""";

        // A switch there and back first, so the first render's attaches have landed.
        await cut.InvokeAsync(() => second.ModuleTwin.OnActivate(json));
        await cut.InvokeAsync(() => first.ModuleTwin.OnActivate(json));
        renders.Clear();

        await cut.InvokeAsync(() => second.ModuleTwin.OnActivate(json));

        Assert.Equal("true", cut.Find("[data-testid=\"trigger-item-2\"]").GetAttribute("aria-expanded"));
        string rendered = string.Join(' ', renders.Keys.Order(StringComparer.Ordinal).Select(key => renders[key] == 1 ? key : key + "×" + renders[key]));
        Assert.Equal("content-item-1 content-item-2 icon-item-1×2 icon-item-2×2 popup positioner trigger-item-1 trigger-item-2 viewport×2", rendered);
    }

    [Fact(DisplayName = "a re-render of an open menu with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        IRenderedComponent<NavigationMenuRoot<string>> cut = RenderMenu();
        for (int i = 0; i < 20; i++)
        {
            Rerender(cut);
        }

        int updates = test.InteractionsHandle.Invocations["update"].Count + test.NavigationMenuHandle.Invocations["update"].Count;
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Rerender(cut);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(updates, test.InteractionsHandle.Invocations["update"].Count + test.NavigationMenuHandle.Invocations["update"].Count);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    private static NavigationMenuTrigger TriggerOf(IRenderedComponent<NavigationMenuRoot<string>> cut, string testId) =>
        cut.FindComponents<NavigationMenuTrigger>()
            .Single(trigger => (string?)trigger.Instance.AdditionalAttributes?["data-testid"] == testId)
            .Instance;

    private void Rerender(IRenderedComponent<NavigationMenuRoot<string>> cut) =>
        cut.Render(p => p.Add(x => x.DefaultValue, "item-1").Add(x => x.ChildContent, menu));

    private IRenderedComponent<NavigationMenuRoot<string>> RenderMenu()
    {
        menu ??= BuildMenu;
        return test.Context.Render<NavigationMenuRoot<string>>(p => p
            .Add(x => x.DefaultValue, "item-1")
            .Add(x => x.ClassValue, Count<NavigationMenuRootState>("root"))
            .Add(x => x.ChildContent, menu));
    }

    private Func<TState, string?> Count<TState>(string key) => _ =>
    {
        renders[key] = renders.GetValueOrDefault(key) + 1;
        return null;
    };

    private void BuildMenu(RenderTreeBuilder builder)
    {
        builder.OpenComponent<NavigationMenuList>(0);
        builder.AddComponentParameter(1, nameof(NavigationMenuList.ClassValue), Count<NavigationMenuListState>("list"));
        builder.AddComponentParameter(2, nameof(NavigationMenuList.ChildContent), (RenderFragment)(list =>
        {
            foreach (string value in (string[])["item-1", "item-2"])
            {
                list.OpenComponent<NavigationMenuItem<string>>(0);
                list.SetKey(value);
                list.AddComponentParameter(1, nameof(NavigationMenuItem<string>.Value), value);
                list.AddComponentParameter(2, nameof(NavigationMenuItem<string>.ChildContent), (RenderFragment)(item =>
                {
                    item.OpenComponent<NavigationMenuTrigger>(0);
                    item.AddAttribute(1, "data-testid", "trigger-" + value);
                    item.AddComponentParameter(2, nameof(NavigationMenuTrigger.ClassValue), Count<NavigationMenuTriggerState>("trigger-" + value));
                    item.AddComponentParameter(3, nameof(NavigationMenuTrigger.ChildContent), (RenderFragment)(trigger =>
                    {
                        trigger.OpenComponent<NavigationMenuIcon>(0);
                        trigger.AddComponentParameter(1, nameof(NavigationMenuIcon.ClassValue), Count<NavigationMenuIconState>("icon-" + value));
                        trigger.CloseComponent();
                    }));
                    item.CloseComponent();
                    item.OpenComponent<NavigationMenuContent>(4);
                    item.AddAttribute(5, "data-testid", "content-" + value);
                    item.AddComponentParameter(6, nameof(NavigationMenuContent.ClassValue), Count<NavigationMenuContentState>("content-" + value));
                    item.CloseComponent();
                }));
                list.CloseComponent();
            }
        }));
        builder.CloseComponent();
        builder.OpenComponent<NavigationMenuPortal>(3);
        builder.AddComponentParameter(4, nameof(NavigationMenuPortal.ClassValue), Count<EmptyState>("portal"));
        builder.AddComponentParameter(5, nameof(NavigationMenuPortal.ChildContent), (RenderFragment)(portal =>
        {
            portal.OpenComponent<NavigationMenuPositioner>(0);
            portal.AddComponentParameter(1, nameof(NavigationMenuPositioner.ClassValue), Count<NavigationMenuPositionerState>("positioner"));
            portal.AddComponentParameter(2, nameof(NavigationMenuPositioner.ChildContent), (RenderFragment)(positioner =>
            {
                positioner.OpenComponent<NavigationMenuPopup>(0);
                positioner.AddComponentParameter(1, nameof(NavigationMenuPopup.ClassValue), Count<NavigationMenuPopupState>("popup"));
                positioner.AddComponentParameter(2, nameof(NavigationMenuPopup.ChildContent), (RenderFragment)(popup =>
                {
                    popup.OpenComponent<NavigationMenuViewport>(0);
                    popup.AddComponentParameter(1, nameof(NavigationMenuViewport.ClassValue), Count<EmptyState>("viewport"));
                    popup.CloseComponent();
                }));
                positioner.CloseComponent();
            }));
            portal.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
