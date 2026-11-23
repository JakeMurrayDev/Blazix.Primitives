using System.Collections.Concurrent;

// Family tests use the project's root namespace: a `Toast` namespace would read as a family type.
namespace Iodine.Headless.Tests;

/// <summary>
/// The port's own titles for the toast manager and store at L1: the timeout fallbacks of a <see cref="Duration"/>
/// whose <see langword="null"/> means "not supplied" (ADR 08 Amendments 2026-10-27 and 2026-11-18), the marshalling of a
/// call made off the renderer's context (ADR 05 Amendments 2026-11-18 item 3; the M6 plan's risk 11), several providers
/// over one manager, the subscription's timing, the <see cref="ToastManager{TData}.Changed"/> event, the task returned
/// for a canceled or task-only promise, and the provider's disposal.
/// </summary>
public sealed class ToastManagerPortTests
{
    [Fact(DisplayName = "port: a null provider timeout falls back to 5000 ms, a supplied one is used as given")]
    [Trait("Requirement", "TST-API-01")]
    public void A_null_provider_timeout_falls_back_to_5000_ms_a_supplied_one_is_used_as_given()
    {
        Assert.Equal(5000, ToastStore<NoPayload>.ProviderTimeout(null));
        Assert.Equal(1200, ToastStore<NoPayload>.ProviderTimeout(1200));
        Assert.Equal(0, ToastStore<NoPayload>.ProviderTimeout(0));
        Assert.Equal(2500, ToastStore<NoPayload>.ProviderTimeout(TimeSpan.FromSeconds(2.5)));
    }

    [Fact(DisplayName = "port: a toast without a timeout takes the provider's, and a timeout of 0 or less schedules no timer")]
    [Trait("Requirement", "TST-STA-18")]
    public void A_toast_without_a_timeout_takes_the_providers_and_a_timeout_of_0_or_less_schedules_no_timer()
    {
        using ToastHarness<NoPayload> harness = new(timeout: 700);
        string inherits = harness.Manager.Add(new ToastObject { Title = "inherits" });
        string zero = harness.Manager.Add(new ToastObject { Title = "zero", Timeout = 0 });
        string negative = harness.Manager.Add(new ToastObject { Title = "negative", Timeout = -5 });

        Assert.True(harness.Store.HasTimer(inherits));
        Assert.False(harness.Store.HasTimer(zero));
        Assert.False(harness.Store.HasTimer(negative));

        harness.Advance(700);
        Assert.Equal(TransitionStatus.Ending, harness.Find(inherits)?.TransitionStatus);
        Assert.True(harness.IsOpen(zero));
        Assert.True(harness.IsOpen(negative));
    }

    [Fact(DisplayName = "port: an add, an update and a close from a pool thread are applied on the renderer's context, and the id is returned at once")]
    [Trait("Requirement", "TST-API-06")]
    [Trait("Requirement", "TST-API-08")]
    public async Task An_add_an_update_and_a_close_from_a_pool_thread_are_applied_on_the_renderers_context_and_the_id_is_returned_at_once()
    {
        using RendererThread renderer = new();
        using ToastHarness<NoPayload> harness = await renderer.Run(() => new ToastHarness<NoPayload>(invoke: renderer.Invoke));
        List<int> writeThreads = [];
        IDisposable observation = await renderer.Run(() => harness.Store.Subscribe(_ => writeThreads.Add(Environment.CurrentManagedThreadId)));

        string id = await Task.Run(() => harness.Manager.Add(new ToastObject { Id = "pooled", Title = "title" }), TestContext.Current.CancellationToken);
        Assert.Equal("pooled", id);
        string generated = await Task.Run(() => harness.Manager.Add(new ToastObject { Title = "generated" }), TestContext.Current.CancellationToken);
        Assert.StartsWith("toast-", generated, StringComparison.Ordinal);

        await Task.Run(() => harness.Manager.Update("pooled", toast => toast with { Title = "updated" }), TestContext.Current.CancellationToken);
        await Task.Run(() => harness.Manager.Close(generated), TestContext.Current.CancellationToken);
        await renderer.Run(() => { });

        Assert.Equal("updated", await renderer.Run(() => harness.Find("pooled")?.Title));
        Assert.Equal(TransitionStatus.Ending, await renderer.Run(() => harness.Find(generated)?.TransitionStatus));
        Assert.NotEmpty(writeThreads);
        Assert.All(writeThreads, thread => Assert.Equal(renderer.ThreadId, thread));
        observation.Dispose();
    }

    [Fact(DisplayName = "port: a promise settled on a pool thread applies its success state on the renderer's context")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-EVT-07")]
    public async Task A_promise_settled_on_a_pool_thread_applies_its_success_state_on_the_renderers_context()
    {
        using RendererThread renderer = new();
        using ToastHarness<NoPayload> harness = await renderer.Run(() => new ToastHarness<NoPayload>(invoke: renderer.Invoke));
        TaskCompletionSource<string> source = new();
        int successThread = 0;

        Task<string> handled = await renderer.Run(() => harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = new(value =>
            {
                successThread = Environment.CurrentManagedThreadId;
                return value;
            }),
            Error = "error",
        }));

        await Task.Run(() => source.SetResult("done"), TestContext.Current.CancellationToken);

        Assert.Equal("done", await handled);
        Assert.Equal(renderer.ThreadId, successThread);
        Assert.Equal("done", await renderer.Run(() => harness.Toasts[0].Description));
    }

    [Fact(DisplayName = "port: a promise started from a pool thread returns a task that waits for the provider's handled task")]
    [Trait("Requirement", "TST-API-09")]
    public async Task A_promise_started_from_a_pool_thread_returns_a_task_that_waits_for_the_providers_handled_task()
    {
        using RendererThread renderer = new();
        using ToastHarness<NoPayload> harness = await renderer.Run(() => new ToastHarness<NoPayload>(invoke: renderer.Invoke));
        TaskCompletionSource<string> source = new();

        Task<string> handled = await Task.Run(
            () => Task.FromResult(harness.Manager.Promise(source.Task, new ToastPromiseOptions<string> { Loading = "loading", Success = "success", Error = "error" })),
            TestContext.Current.CancellationToken);
        Assert.NotSame(source.Task, handled);

        source.SetResult("done");
        Assert.Equal("done", await handled);
        Assert.Equal("success", await renderer.Run(() => harness.Toasts[0].Description));
    }

    [Fact(DisplayName = "port: a canceled task takes the error branch with its exception unwrapped, and the returned task faults with it")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-EVT-08")]
    public async Task A_canceled_task_takes_the_error_branch_with_its_exception_unwrapped_and_the_returned_task_faults_with_it()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource<string> source = new();
        Exception? seen = null;
        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = "success",
            Error = new(exception =>
            {
                seen = exception;
                return "canceled";
            }),
        });

        source.SetCanceled(TestContext.Current.CancellationToken);

        TaskCanceledException thrown = await Assert.ThrowsAsync<TaskCanceledException>(() => handled);
        Assert.True(handled.IsFaulted);
        Assert.Same(seen, thrown);
        Assert.IsNotType<AggregateException>(seen);
        Assert.Equal("canceled", harness.Toasts[0].Description);
        Assert.Equal("error", harness.Toasts[0].Type);
    }

    [Fact(DisplayName = "port: a promise over a task with no result follows it like a promise over a value")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task A_promise_over_a_task_with_no_result_follows_it_like_a_promise_over_a_value()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource source = new();
        Task handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<NoPayload> { Loading = "saving", Success = "saved", Error = "failed" });
        Assert.Equal("saving", harness.Toasts[0].Description);

        source.SetResult();
        await handled;

        Assert.Equal("saved", harness.Toasts[0].Description);
        Assert.Equal("success", harness.Toasts[0].Type);
    }

    [Fact(DisplayName = "port: a manager with no provider returns the task itself and discards its calls")]
    [Trait("Requirement", "TST-API-08")]
    [Trait("Requirement", "TST-API-09")]
    public void A_manager_with_no_provider_returns_the_task_itself_and_discards_its_calls()
    {
        ToastManager manager = new();
        TaskCompletionSource<string> source = new();

        Task<string> handled = manager.Promise(source.Task, new ToastPromiseOptions<string> { Loading = "loading", Success = "success", Error = "error" });
        manager.Update("missing", toast => toast);
        manager.Close();

        Assert.Same(source.Task, handled);
        Assert.Empty(manager.Toasts);
    }

    [Fact(DisplayName = "port: several providers over one manager each show every toast in a store of their own")]
    [Trait("Requirement", "TST-EDGE-05")]
    [Trait("Requirement", "TST-STR-01")]
    public void Several_providers_over_one_manager_each_show_every_toast_in_a_store_of_their_own()
    {
        ToastManager manager = new();
        using ToastHarness<NoPayload> first = new(manager: manager);
        using ToastHarness<NoPayload> second = new(manager: manager);

        string id = manager.Add(new ToastObject { Title = "shared" });
        first.Store.CloseToast(id);

        Assert.Equal(TransitionStatus.Ending, first.Find(id)?.TransitionStatus);
        Assert.Equal(TransitionStatus.Starting, second.Find(id)?.TransitionStatus);
        Assert.NotSame(first.Store, second.Store);

        // The manager's list is the first subscribed provider's.
        Assert.Same(first.Toasts, manager.Toasts);
    }

    [Fact(DisplayName = "port: a provider that subscribes later does not receive earlier calls")]
    [Trait("Requirement", "TST-EVT-06")]
    public void A_provider_that_subscribes_later_does_not_receive_earlier_calls()
    {
        ToastManager manager = new();
        using ToastHarness<NoPayload> early = new(manager: manager);
        manager.Add(new ToastObject { Id = "before", Title = "before" });

        using ToastHarness<NoPayload> late = new(manager: manager);
        manager.Add(new ToastObject { Id = "after", Title = "after" });

        Assert.Equal(["after", "before"], early.Toasts.Select(toast => toast.Id));
        Assert.Equal(["after"], late.Toasts.Select(toast => toast.Id));
    }

    [Fact(DisplayName = "port: a disposed provider link unsubscribes, so later calls no longer reach its store")]
    [Trait("Requirement", "TST-API-03")]
    public void A_disposed_provider_link_unsubscribes_so_later_calls_no_longer_reach_its_store()
    {
        ToastManager manager = new();
        ToastHarness<NoPayload> harness = new(manager: manager);
        manager.Add(new ToastObject { Id = "a" });

        harness.Dispose();
        manager.Add(new ToastObject { Id = "b" });

        Assert.Equal(["a"], harness.Toasts.Select(toast => toast.Id));
        Assert.Empty(manager.Toasts);
    }

    [Fact(DisplayName = "port: the manager raises Changed after each change of a subscribed provider's toasts")]
    [Trait("Requirement", "TST-API-05")]
    [Trait("Requirement", "TST-EVT-11")]
    public void The_manager_raises_Changed_after_each_change_of_a_subscribed_providers_toasts()
    {
        ToastManager manager = new();
        using ToastHarness<NoPayload> harness = new(manager: manager);
        List<IReadOnlyList<ToastObject<NoPayload>>> seen = [];
        manager.Changed += (sender, _) => seen.Add(((ToastManager)sender!).Toasts);

        string id = manager.Add(new ToastObject { Title = "title" });
        manager.Update(id, toast => toast with { Title = "updated" });
        harness.Store.SetHovering(true);
        manager.Close(id);

        // The hover changed no toast, so it raised nothing.
        Assert.Equal(3, seen.Count);
        Assert.Equal("title", seen[0][0].Title);
        Assert.Equal("updated", seen[1][0].Title);
        Assert.Equal(TransitionStatus.Ending, seen[2][0].TransitionStatus);
    }

    [Fact(DisplayName = "port: disposing the provider's store clears every pending timer")]
    [Trait("Requirement", "TST-EDGE-06")]
    [Trait("Requirement", "TST-STA-25")]
    public void Disposing_the_providers_store_clears_every_pending_timer()
    {
        ToastHarness<NoPayload> harness = new();
        int closed = 0;
        harness.Manager.Add(new ToastObject { Id = "a", Timeout = 100, OnClose = () => closed++ });
        harness.Manager.Add(new ToastObject { Id = "b", Timeout = 200, OnClose = () => closed++ });

        harness.Dispose();
        harness.Advance(1000);

        Assert.Equal(0, closed);
        Assert.False(harness.Store.HasTimer("a"));
        Assert.False(harness.Store.HasTimer("b"));
    }

    [Fact(DisplayName = "port: an update assigns only what its with expression assigns, and the add input only what its initializer sets")]
    [Trait("Requirement", "TST-STA-05")]
    [Trait("Requirement", "TST-STA-09")]
    public void An_update_assigns_only_what_its_with_expression_assigns_and_the_add_input_only_what_its_initializer_sets()
    {
        using ToastHarness<Draft> harness = new();
        Draft data = new("Draft", 1);
        harness.Manager.Add(new ToastObject<Draft> { Id = "a", Title = "title", Description = "description", Data = data, Priority = ToastPriority.High });

        // An updater that returns a fresh toast changes only what that toast's initializer set.
        harness.Manager.Update("a", _ => new ToastObject<Draft> { Description = null });
        ToastObject<Draft>? toast = harness.Find("a");
        Assert.Equal("title", toast?.Title);
        Assert.Null(toast?.Description);
        Assert.Same(data, toast?.Data);
        Assert.Equal(ToastPriority.High, toast?.Priority);

        // The presence of a key is invisible to equality.
        Assert.Equal(new ToastObject<Draft> { Title = "x" }, new ToastObject<Draft> { Title = "x", Description = null });
        Assert.Equal(new ToastObject<Draft> { Title = "x" }.GetHashCode(), new ToastObject<Draft> { Title = "x", Description = null }.GetHashCode());
    }

    // A dedicated thread standing in for the renderer's synchronization context: work posted from another thread runs
    // there in order, and work invoked on it runs inline, as Blazor's dispatcher does.
    private sealed class RendererThread : IDisposable
    {
        private readonly BlockingCollection<Action> queue = [];
        private readonly Thread thread;

        public RendererThread()
        {
            thread = new Thread(() =>
            {
                foreach (Action action in queue.GetConsumingEnumerable())
                {
                    action();
                }
            })
            {
                IsBackground = true,
            };
            thread.Start();
        }

        public int ThreadId => thread.ManagedThreadId;

        public void Dispose() => queue.CompleteAdding();

        public Task Invoke(Action action)
        {
            if (Environment.CurrentManagedThreadId == thread.ManagedThreadId)
            {
                action();
                return Task.CompletedTask;
            }

            return Run(action);
        }

        public Task Run(Action action) => Run(() =>
        {
            action();
            return true;
        });

        public Task<T> Run<T>(Func<T> work)
        {
            TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Add(() =>
            {
                try
                {
                    completion.SetResult(work());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            });
            return completion.Task;
        }
    }
}
