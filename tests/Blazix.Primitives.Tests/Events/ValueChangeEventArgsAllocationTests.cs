using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Events;

/// <summary>
/// The delivery budget of ADR 16 rule 7 for <see cref="ValueChangeEventArgs{T}"/> (ADR 05 §3: "<c>EventCallback&lt;ValueChangeEventArgs&lt;T&gt;&gt;</c>
/// delivers it without boxing"). Measured both ways Blazor delivers: with no receiver, and through a receiver that
/// implements <see cref="IHandleEvent"/> as every component does, whose <c>HandleEventAsync(item, object? arg)</c> takes
/// the argument as <see cref="object"/>. Measured 2026-10-20 on SDK 11.0.100-rc.2: no allocation without a receiver; about
/// 66 bytes per delivery through a receiver, of which 32 are the box of the struct and about 32 are Blazor's own dispatch
/// (a <see cref="string"/> argument costs about 34 bytes the same way). ADR 05 §3's "without boxing" therefore holds only
/// for receiverless callbacks; a consumer's callback always has its component as receiver.
/// </summary>
public sealed class ValueChangeEventArgsAllocationTests
{
    private const int Deliveries = 1000;
    private const long ReceiverlessBudgetBytes = 0;

    // One box of ValueChangeEventArgs<bool> (32 bytes on the heap) plus Blazor's receiver dispatch (about 32 bytes), with
    // headroom for the allocation context's granularity.
    private const long ComponentReceiverBudgetBytesPerDelivery = 68;

    [Fact(DisplayName = "a delivery with no receiver allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_delivery_with_no_receiver_allocates_nothing()
    {
        int received = 0;
        EventCallback<ValueChangeEventArgs<bool>> callback = new(null, (Action<ValueChangeEventArgs<bool>>)(args => received += args.Value ? 1 : 0));
        ValueChangeEventArgs<bool> args = new(true, new ChangeEventDetails(EventReason.None));
        await callback.InvokeAsync(args);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Deliveries; i++)
        {
            Task task = callback.InvokeAsync(args);
            Assert.True(task.IsCompletedSuccessfully);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(Deliveries + 1, received);
        Assert.True(allocated <= ReceiverlessBudgetBytes, $"{allocated} bytes over {Deliveries} deliveries; budget {ReceiverlessBudgetBytes}.");
    }

    [Fact(DisplayName = "a delivery through a component receiver costs one box plus Blazor's dispatch")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_delivery_through_a_component_receiver_costs_one_box_plus_Blazor_s_dispatch()
    {
        Receiver receiver = new();
        EventCallback<ValueChangeEventArgs<bool>> callback = new(receiver, (Action<ValueChangeEventArgs<bool>>)(args => receiver.Received += args.Value ? 1 : 0));
        ValueChangeEventArgs<bool> args = new(true, new ChangeEventDetails(EventReason.None));
        await callback.InvokeAsync(args);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Deliveries; i++)
        {
            Task task = callback.InvokeAsync(args);
            Assert.True(task.IsCompletedSuccessfully);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long budget = ComponentReceiverBudgetBytesPerDelivery * Deliveries;

        Assert.Equal(Deliveries + 1, receiver.Received);
        Assert.True(allocated <= budget, $"{allocated} bytes over {Deliveries} deliveries; budget {budget}.");
    }

    /// <summary>A component's event handling without a renderer: run the work item, no re-render.</summary>
    private sealed class Receiver : IHandleEvent
    {
        public int Received { get; set; }

        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
    }
}
