using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the toast parts (the M6 plan's decision 26 (a); issue 280, Deliverable 11): the least of five hover
/// writes, each rendering the viewport and every root with its content, title, description, action and close once (the
/// render counts of <see cref="ToastRenderCountTests"/>), at 1 and 10 toasts, measured with the class alone in its process.
/// It renders through a <see cref="Renderer"/> of its own with a display that does nothing (the
/// <c>RenderPipelineSequenceTests</c> precedent): bUnit's markup upkeep after each batch grows with the square of the tree
/// (65 KB at 1 toast, 2.7 MB at 10, measured 2026-11-19), which would hide the parts' own cost; the figure here is the
/// library's and Blazor's.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ToastAllocationBudgetTests
{
    // First measurement 2026-11-19 (arm64, .NET 11, Release, the class alone, three processes alike), with the root's swipe
    // fields in its state record (8 bytes a root over the 672 and 5208 measured before them, in this pull request, before
    // the budget first lands on main): 680 bytes at 1 toast and 5288 at 10, linear (about 512 bytes a toast): each root's
    // style string, the viewport template's per-toast fragments, the new state records of the parts whose expanded flag
    // moved, and Blazor's diff.
    private const long HoverBudget1 = 680;
    private const long HoverBudget10 = 5288;
    private const int Runs = 5;

    [Theory(DisplayName = "port: a hover that renders the viewport and every toast once stays within its budget")]
    [InlineData(1, HoverBudget1)]
    [InlineData(10, HoverBudget10)]
    [Trait("Requirement", "ADR16-7")]
    public void A_hover_that_renders_the_viewport_and_every_toast_once_stays_within_its_budget(int toasts, long budgetBytes)
    {
        ServiceCollection services = new();
        services.AddBlazixPrimitives();
        services.AddSingleton<IJSRuntime>(new SilentJSRuntime());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        using SilentRenderer renderer = new(scope.ServiceProvider);
        ToastManager manager = scope.ServiceProvider.GetRequiredService<ToastManager>();
        ToastBudgetHost host = renderer.RenderRoot<ToastBudgetHost>();
        for (int i = 0; i < toasts; i++)
        {
            int index = i;
            renderer.Run(() => manager.Add(new ToastObject { Id = $"toast-{index}", Title = "title", Description = "description", ActionContent = b => b.AddContent(0, "go") }));
        }

        ToastStore<NoPayload> store = host.Store;
        bool hovering = false;
        for (int i = 0; i < 20; i++)
        {
            hovering = !hovering;
            bool value = hovering;
            renderer.Run(() => store.SetHovering(value));
        }

        long least = long.MaxValue;
        for (int run = 0; run < Runs; run++)
        {
            hovering = !hovering;
            bool value = hovering;
            Action write = () => store.SetHovering(value);
            long before = GC.GetAllocatedBytesForCurrentThread();
            renderer.Run(write);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        AllocationSampler.AssertWithinBudget(least, budgetBytes, $"a hover at {toasts} toasts");
    }

    // The toast list of ToastHost, with a handle on its provider's store.
    private sealed class ToastBudgetHost : ComponentBase
    {
        private ToastProvider<NoPayload>? provider;

        public ToastStore<NoPayload> Store => provider!.Store;

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ToastProvider<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(ToastProvider<NoPayload>.Limit), 20);
            builder.AddComponentParameter(2, nameof(ToastProvider<NoPayload>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<ToastViewport<NoPayload>>(0);
                inner.AddComponentParameter(1, nameof(ToastViewport<NoPayload>.ToastsContent), (RenderFragment<IReadOnlyList<ToastObject<NoPayload>>>)(list => items =>
                {
                    foreach (ToastObject<NoPayload> toast in list)
                    {
                        items.OpenComponent<ToastRoot<NoPayload>>(0);
                        items.SetKey(toast.Id);
                        items.AddComponentParameter(1, nameof(ToastRoot<NoPayload>.Toast), toast);
                        items.AddComponentParameter(2, nameof(ToastRoot<NoPayload>.ChildContent), (RenderFragment)(parts =>
                        {
                            parts.OpenComponent<ToastContent>(0);
                            parts.AddComponentParameter(1, nameof(ToastContent.ChildContent), (RenderFragment)(content =>
                            {
                                content.OpenComponent<ToastTitle>(0);
                                content.CloseComponent();
                                content.OpenComponent<ToastDescription>(1);
                                content.CloseComponent();
                                content.OpenComponent<ToastAction>(2);
                                content.CloseComponent();
                                content.OpenComponent<ToastClose>(3);
                                content.CloseComponent();
                            }));
                            parts.CloseComponent();
                        }));
                        items.CloseComponent();
                    }
                }));
                inner.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(3, component => provider = (ToastProvider<NoPayload>)component);
            builder.CloseComponent();
        }
    }

    // A renderer whose display does nothing, interactive so the provider subscribes.
    private sealed class SilentRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        /// <inheritdoc />
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        /// <inheritdoc />
        protected override RendererInfo RendererInfo { get; } = new("Server", isInteractive: true);

        public TComponent RenderRoot<TComponent>()
            where TComponent : IComponent
        {
            IComponent component = InstantiateComponent(typeof(TComponent));
            int id = AssignRootComponentId(component);
            Run(() => _ = RenderRootComponentAsync(id, ParameterView.Empty));
            return (TComponent)component;
        }

        public void Run(Action action) => Dispatcher.InvokeAsync(action).GetAwaiter().GetResult();

        /// <inheritdoc />
        protected override void HandleException(Exception exception) => throw exception;

        /// <inheritdoc />
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }

    // Every module import and call completes at once with no value. BL0016 asks callers to guard interop calls; this is the
    // runtime itself, which throws nothing.
#pragma warning disable BL0016
    private sealed class SilentJSRuntime : IJSRuntime, IJSObjectReference
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        /// <inheritdoc />
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            new(this is TValue reference ? reference : default!);
    }
#pragma warning restore BL0016
}
