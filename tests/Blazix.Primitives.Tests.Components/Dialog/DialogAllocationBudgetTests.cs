using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rules 7 and 9 for Dialog (issue 123's tests-first list): a re-render with unchanged parameters sends nothing to
/// any of the six modules and stays within its budget, a value-type payload does not box, and an open and a close render
/// neither the root nor an inactive trigger (counted, the issue 110 rule: the root through a counter in its content, a
/// trigger through its class function). Budgets are the least of five measurements, in the non-parallel collection.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class DialogAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-28 (arm64, .NET 11 RC2, Release), the least of five after twenty renders. 7032 bytes for
    // the re-render of the harness with a trigger, a portal, a popup, a title and a close: the harness, the root and each
    // part with a content fragment render again (a fragment parameter is always changed), the others do not, and none of
    // the six modules is sent an update. 25 full runs of the project measure 7032 (24 runs) and 7144, the spread the
    // Positioner budget records for the state earlier tests leave in the process, so the budget is their envelope. 584
    // bytes for a payload write with an int payload and with a string payload alike in every run: the two records of the
    // shared write (ADR 16 Amendments 2026-10-27) and the root's render of its payload content; a box would add 24.
    private const long RerenderBudgetBytes = 7144;
    private const long PayloadWriteBudgetBytes = 584;

    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a re-render of an open dialog with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_an_open_dialog_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        Action<ComponentParameterCollectionBuilder<DialogHarness>> parameters = p => p.Add(x => x.DefaultOpen, true).Add(x => x.WithClose, true).Add(x => x.WithTitle, true);
        IRenderedComponent<DialogHarness> cut = test.Context.Render(parameters);
        Action render = () => cut.Render(parameters);
        WarmUp(render);
        long allocated = AllocationSampler.LeastOfFive(render);
        int sends = Sends();

        cut.Render(parameters);

        Assert.Equal(sends, Sends());
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    [Fact(DisplayName = "port: a payload write of an int allocates no more than a reference payload's, so the payload does not box")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_value_type_payload_does_not_box()
    {
        long intBytes = await PayloadWriteBytesAsync(7, 8);
        long stringBytes = await PayloadWriteBytesAsync("seven", "eight");

        Assert.True(intBytes <= stringBytes, $"an int payload's write allocated {intBytes} bytes; a string payload's {stringBytes}");
        Assert.True(intBytes <= PayloadWriteBudgetBytes, $"a payload write allocated {intBytes} bytes (a string payload's {stringBytes}); the budget is {PayloadWriteBudgetBytes}");
    }

    [Fact(DisplayName = "port: opening, moving and closing a handle-backed dialog renders neither the root nor an inactive detached trigger")]
    [Trait("Requirement", "DLG-EDGE-16")]
    [Trait("Requirement", "ADR16-9")]
    public async Task Opening_moving_and_closing_renders_neither_the_root_nor_an_inactive_trigger()
    {
        DialogHandle<NoPayload> handle = new();
        int[] triggerRenders = new int[3];
        int rootRenders = 0;
        RenderFragment tree = builder =>
        {
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                builder.OpenComponent<DialogTrigger<NoPayload>>(0);
                builder.SetKey(index);
                builder.AddComponentParameter(1, nameof(DialogTrigger<NoPayload>.Handle), handle);
                builder.AddAttribute(2, "id", $"trigger-{index + 1}");
                builder.AddComponentParameter(3, nameof(DialogTrigger<NoPayload>.ClassValue), (Func<DialogTriggerState, string?>)(_ =>
                {
                    triggerRenders[index]++;
                    return null;
                }));
                builder.CloseComponent();
            }

            builder.OpenComponent<DialogRoot<NoPayload>>(4);
            builder.AddComponentParameter(5, nameof(DialogRoot<NoPayload>.Handle), handle);
            builder.AddComponentParameter(6, nameof(DialogRoot<NoPayload>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<RenderCounter>(0);
                inner.AddComponentParameter(1, nameof(RenderCounter.Rendered), (Action)(() => rootRenders++));
                inner.AddComponentParameter(2, nameof(RenderCounter.ChildContent), (RenderFragment)(content =>
                {
                    content.OpenComponent<DialogPortal>(0);
                    content.AddComponentParameter(1, nameof(DialogPortal.ChildContent), (RenderFragment)(portal =>
                    {
                        portal.OpenComponent<DialogPopup>(0);
                        portal.CloseComponent();
                    }));
                    content.CloseComponent();
                }));
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };
        IRenderedComponent<IComponent> cut = test.Context.Render(tree);
        int rootBefore = rootRenders;
        int[] before = [.. triggerRenders];
        Assert.True(rootBefore > 0 && before.All(count => count > 0));

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Equal("trigger-1", ActiveTrigger(cut));
        await test.PressAsync(cut, "trigger-2", open: true);
        Assert.Equal("trigger-2", ActiveTrigger(cut));
        await test.PressAsync(cut, "trigger-2", open: false);
        Assert.NotNull(Popup(cut).GetAttribute("data-closed"));

        // Measured 2026-10-28: trigger 1 renders twice (opened, then moved away from), trigger 2 twice (moved to, then
        // closed), trigger 3 and the root never.
        Assert.Equal([2, 2, 0], triggerRenders.Select((count, i) => count - before[i]));
        Assert.Equal(0, rootRenders - rootBefore);
    }

    // Two payloads written alternately through the store of an open root whose payload content renders a constant; the
    // notification compares the record's version counter, and the store compares the payloads with Object.is.
    private async Task<long> PayloadWriteBytesAsync<TPayload>(TPayload first, TPayload second)
    {
        DialogHandle<TPayload> handle = new();
        RenderFragment constant = builder => builder.AddContent(0, "payload");
        RenderFragment<TPayload?> payloadContent = _ => constant;
        IRenderedComponent<DialogRoot<TPayload>> cut = test.Context.Render<DialogRoot<TPayload>>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Handle, handle)
            .Add(x => x.PayloadContent, payloadContent));
        IPopupStore<TPayload> store = handle.Store;
        int writes = 0;
        await cut.InvokeAsync(() => store.SetPayload(second));
        Action write = () => cut.InvokeAsync(() => store.SetPayload(writes++ % 2 == 0 ? first : second)).GetAwaiter().GetResult();
        WarmUp(write);
        return AllocationSampler.LeastOfFive(write);
    }

    // Twenty runs before the shared sampler's five, as the budgets were first measured.
    private static void WarmUp(Action operation)
    {
        for (int i = 0; i < 20; i++)
        {
            operation();
        }
    }

    private static AngleSharp.Dom.IElement Popup(IRenderedComponent<IComponent> cut) => cut.Find("[role=dialog]");

    private static string? ActiveTrigger(IRenderedComponent<IComponent> cut) => cut.Find("[data-popup-open]").Id;

    private int Sends() =>
        new[] { test.InteractionsHandle, test.FocusManagerHandle, test.TransitionsHandle, test.PortalHandle, test.ButtonHandle, test.ScrollLockHandle }
            .Sum(handle => DialogTestContext.UpdateOptions(handle).Count);
}
