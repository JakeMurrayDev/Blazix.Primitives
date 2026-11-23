using Blazix.Primitives.Store;

// Family tests use the project's root namespace: a `Toast` namespace would read as a family type.
namespace Blazix.Primitives.Tests;

/// <summary>
/// ADR 16 rule 7 for the toast store (the M6 plan's decision 26 (a); issue 280, Deliverable 11): an add's whole path
/// through the manager, and a store notification seen by every root's selectors, at 1 and 10 toasts. Each figure is the
/// least of five runs after a warm-up, measured with the class alone in its process
/// (<c>.github/scripts/allocation-budgets.mjs</c>). The root's render budget is L2's.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ToastAllocationBudgetTests
{
    private const int Runs = 5;

    // First measurements 2026-11-18 (arm64, .NET 11, Release, the class alone, five processes alike). An add: the stored
    // toast, the new array and list with its metadata dictionary, the timer record, its run and its ITimer.
    private const long AddBudget1 = 856;
    private const long AddBudget10 = 1288;

    // A hover: the new state record only; no selection but the expanded flag moved.
    private const long HoverBudget1 = 40;
    private const long HoverBudget10 = 40;

    // An update: the updater's `with`, the applied toast, the new array and list with its metadata dictionary.
    private const long UpdateBudget1 = 808;
    private const long UpdateBudget10 = 1168;

    [Theory(DisplayName = "port: an add's whole path allocates the toast, the list and its timer")]
    [InlineData(1, AddBudget1)]
    [InlineData(10, AddBudget10)]
    [Trait("Requirement", "ADR16-7")]
    public void An_adds_whole_path_allocates_the_toast_the_list_and_its_timer(int toasts, long budgetBytes)
    {
        using ToastHarness<NoPayload> harness = new(limit: 20);
        ToastObject[] inputs = new ToastObject[20 + Runs];
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new ToastObject { Id = $"toast-{i}", Title = "title" };
        }

        // The list holds `toasts - 1` toasts before each measured add, which makes the `toasts`-th.
        for (int i = 0; i < toasts - 1; i++)
        {
            harness.Manager.Add(inputs[i]);
        }

        // Warm-up: an add and its close-and-remove, so the measured runs reuse the dictionary's and timer's code paths.
        harness.Manager.Add(new ToastObject { Id = "warm", Title = "title" });
        harness.Store.CloseToast("warm");
        harness.Store.RemoveToast("warm", skipOnRemove: true);

        long least = long.MaxValue;
        for (int run = 0; run < Runs; run++)
        {
            ToastObject input = inputs[toasts - 1 + run];
            long before = GC.GetAllocatedBytesForCurrentThread();
            harness.Manager.Add(input);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            harness.Store.CloseToast(input.Id);
            harness.Store.RemoveToast(input.Id, skipOnRemove: true);
        }

        AllocationSampler.AssertWithinBudget(least, budgetBytes, $"an add at {toasts} toasts");
    }

    [Theory(DisplayName = "port: a hover notification seen by every root's selectors allocates only the new record")]
    [InlineData(1, HoverBudget1)]
    [InlineData(10, HoverBudget10)]
    [Trait("Requirement", "ADR16-7")]
    public void A_hover_notification_seen_by_every_roots_selectors_allocates_only_the_new_record(int toasts, long budgetBytes)
    {
        using ToastHarness<NoPayload> harness = new(limit: 20);
        List<SelectorSubscription<ToastStoreState<NoPayload>>> roots = Subscribe(harness, toasts, out int[] renders);
        bool hovering = false;
        for (int i = 0; i < 4; i++)
        {
            harness.Store.SetHovering(hovering = !hovering);
        }

        long least = long.MaxValue;
        for (int run = 0; run < Runs; run++)
        {
            hovering = !hovering;
            long before = GC.GetAllocatedBytesForCurrentThread();
            harness.Store.SetHovering(hovering);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Every root re-renders for the expanded flag, once per write.
        Assert.All(renders, count => Assert.Equal(4 + Runs, count));
        AllocationSampler.AssertWithinBudget(least, budgetBytes, $"a hover at {toasts} toasts");
        GC.KeepAlive(roots);
    }

    [Theory(DisplayName = "port: an update of one toast allocates the toast and the list, and re-renders only its root")]
    [InlineData(1, UpdateBudget1)]
    [InlineData(10, UpdateBudget10)]
    [Trait("Requirement", "ADR16-7")]
    public void An_update_of_one_toast_allocates_the_toast_and_the_list_and_re_renders_only_its_root(int toasts, long budgetBytes)
    {
        using ToastHarness<NoPayload> harness = new(limit: 20);
        List<SelectorSubscription<ToastStoreState<NoPayload>>> roots = Subscribe(harness, toasts, out int[] renders);
        string[] titles = ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"];
        Func<ToastObject<NoPayload>, ToastObject<NoPayload>>[] updates = new Func<ToastObject<NoPayload>, ToastObject<NoPayload>>[titles.Length];
        for (int i = 0; i < titles.Length; i++)
        {
            string title = titles[i];
            updates[i] = toast => toast with { Title = title };
        }

        for (int i = 0; i < 4; i++)
        {
            harness.Manager.Update("toast-0", updates[i]);
        }

        long least = long.MaxValue;
        for (int run = 0; run < Runs; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            harness.Manager.Update("toast-0", updates[4 + run]);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Only the updated toast's root saw a selection change.
        Assert.Equal(4 + Runs, renders[0]);
        Assert.All(renders[1..], count => Assert.Equal(0, count));
        AllocationSampler.AssertWithinBudget(least, budgetBytes, $"an update at {toasts} toasts");
        GC.KeepAlive(roots);
    }

    // The selections a root makes: its toast, its indexes, its offset and the stack's expanded flag.
    private static List<SelectorSubscription<ToastStoreState<NoPayload>>> Subscribe(ToastHarness<NoPayload> harness, int toasts, out int[] renders)
    {
        int[] counts = new int[toasts];
        List<SelectorSubscription<ToastStoreState<NoPayload>>> roots = [];
        for (int i = toasts - 1; i >= 0; i--)
        {
            harness.Manager.Add(new ToastObject { Id = $"toast-{i}", Title = "title" });
        }

        for (int i = 0; i < toasts; i++)
        {
            int index = i;
            string id = $"toast-{i}";
            SelectorSubscription<ToastStoreState<NoPayload>> root = new(harness.Store, () => counts[index]++);
            root.Select(ToastSelectors<NoPayload>.Toast, id);
            root.Select(ToastSelectors<NoPayload>.ToastIndex, id);
            root.Select(ToastSelectors<NoPayload>.ToastVisibleIndex, id);
            root.Select(ToastSelectors<NoPayload>.ToastOffsetY, id);
            root.Select(ToastSelectors<NoPayload>.Expanded);
            roots.Add(root);
        }

        renders = counts;
        return roots;
    }
}
