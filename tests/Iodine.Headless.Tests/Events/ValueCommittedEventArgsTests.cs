using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests.Events;

/// <summary>
/// <see cref="ValueCommittedEventArgs{T}"/>, the argument of <c>OnValueCommitted</c> (the M6 plan's decision 13 (a); ADR 08
/// Amendments 2026-11-18, the commit-callback line, its <c>Details</c> confirmed as <see cref="GenericEventDetails"/>: ADR 05
/// §3, EVD-API-02, SLD-API-04, NUM-API-04), and its delivery budget under ADR 16 rule 7, measured both ways Blazor delivers,
/// as <see cref="ValueChangeEventArgsAllocationTests"/> measures <see cref="ValueChangeEventArgs{T}"/>.
/// </summary>
public sealed class ValueCommittedEventArgsTests
{
    private const int Deliveries = 1000;
    private const long ReceiverlessBudgetBytes = 0;

    // Least of five runs (ADR 16 rule 7), 2026-11-18, SDK 11.0.100, Release: 0 B without a receiver; 65,776 B over 1,000
    // deliveries through one (all five runs), 32 B the box of the struct (a double and a reference) and the rest Blazor's
    // dispatch, the same figure ValueChangeEventArgs<bool> measures on this SDK. Its 68 B envelope is kept.
    private const long ComponentReceiverBudgetBytesPerDelivery = 68;

    [Fact(DisplayName = "port: ValueCommittedEventArgs carries the value and the details object itself")]
    public void Carries_the_value_and_the_details_object_itself()
    {
        GenericEventDetails details = new(EventReason.Drag);

        ValueCommittedEventArgs<double> args = new(42.5, details);

        Assert.Equal(42.5, args.Value);
        Assert.Same(details, args.Details);
    }

    [Fact(DisplayName = "a delivery with no receiver allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_delivery_with_no_receiver_allocates_nothing()
    {
        double received = 0;
        EventCallback<ValueCommittedEventArgs<double>> callback = new(null, (Action<ValueCommittedEventArgs<double>>)(args => received += args.Value));
        ValueCommittedEventArgs<double> args = new(1, new GenericEventDetails(EventReason.None));
        await callback.InvokeAsync(args);

        long allocated = AllocationSampler.LeastOfFive(() =>
        {
            for (int i = 0; i < Deliveries; i++)
            {
                Task task = callback.InvokeAsync(args);
                Assert.True(task.IsCompletedSuccessfully);
            }
        });

        Assert.Equal((AllocationSampler.Runs * Deliveries) + 1, received);
        AllocationSampler.AssertWithinBudget(allocated, ReceiverlessBudgetBytes, $"{Deliveries} deliveries (the least of {AllocationSampler.Runs} runs)");
    }

    [Fact(DisplayName = "a delivery through a component receiver costs one box plus Blazor's dispatch")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_delivery_through_a_component_receiver_costs_one_box_plus_Blazor_s_dispatch()
    {
        Receiver receiver = new();
        EventCallback<ValueCommittedEventArgs<double>> callback = new(receiver, (Action<ValueCommittedEventArgs<double>>)(args => receiver.Received += args.Value));
        ValueCommittedEventArgs<double> args = new(1, new GenericEventDetails(EventReason.None));
        await callback.InvokeAsync(args);

        long allocated = AllocationSampler.LeastOfFive(() =>
        {
            for (int i = 0; i < Deliveries; i++)
            {
                Task task = callback.InvokeAsync(args);
                Assert.True(task.IsCompletedSuccessfully);
            }
        });
        long budget = ComponentReceiverBudgetBytesPerDelivery * Deliveries;

        Assert.Equal((AllocationSampler.Runs * Deliveries) + 1, receiver.Received);
        AllocationSampler.AssertWithinBudget(allocated, budget, $"{Deliveries} deliveries (the least of {AllocationSampler.Runs} runs)");
    }

    /// <summary>A component's event handling without a renderer: run the work item, no re-render.</summary>
    private sealed class Receiver : IHandleEvent
    {
        public double Received { get; set; }

        /// <inheritdoc />
        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
    }
}
