using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace: a `Toast` namespace would read as a family type.
namespace Iodine.Headless.Tests;

/// <summary>
/// The toast store at L1 (TST §4; ADR 06 §1). Base UI's <c>toast/store.test.ts</c>, every title verbatim, over the port's
/// store with a <see cref="FakeTimeProvider"/> in place of <c>vi.useFakeTimers</c>. As in Base UI's <c>createStore</c>, the
/// provider's timeout is 0, so only a toast with a timeout of its own has a timer. Base UI's object and function data are
/// a record and a delegate here, one store per data type.
/// </summary>
public sealed class ToastStoreTests
{
    [Fact(DisplayName = "keeps toast metadata synchronized after mutations")]
    [Trait("Requirement", "TST-STA-02")]
    public void Keeps_toast_metadata_synchronized_after_mutations()
    {
        using ToastHarness<NoPayload> harness = Create([
            new ToastObject { Id = "newest", Height = 30 },
            new ToastObject { Id = "middle", Height = 40 },
            new ToastObject { Id = "oldest", Height = 50 },
        ]);
        ToastStore<NoPayload> store = harness.Store;

        ExpectToastMetadataToMatchToasts(store);

        store.SetMeasuredHeight("middle", 45);
        ExpectToastMetadataToMatchToasts(store);

        store.CloseToast("middle");
        ExpectToastMetadataToMatchToasts(store);
        Assert.Equal(TransitionStatus.Ending, harness.Find("middle")?.TransitionStatus);

        store.RemoveToast("middle", skipOnRemove: true);
        ExpectToastMetadataToMatchToasts(store);

        store.AddToast(new ToastObject { Id = "front", Title = "Front", Timeout = 0 });
        ExpectToastMetadataToMatchToasts(store);
        Assert.Equal(["front", "newest", "oldest"], store.State.Toasts.Select(toast => toast.Id));
    }

    [Fact(DisplayName = "ignores height recalculations while a toast is transitioning out")]
    [Trait("Requirement", "TST-ANI-10")]
    public void Ignores_height_recalculations_while_a_toast_is_transitioning_out()
    {
        using ToastHarness<NoPayload> harness = Create([new ToastObject { Id = "a", Height = 40 }]);

        harness.Store.CloseToast("a");
        Assert.Equal(TransitionStatus.Ending, harness.Find("a")?.TransitionStatus);

        // The measurement write always clears the transition status; the ending toast must stay ending.
        harness.Store.SetMeasuredHeight("a", 80);

        ToastObject<NoPayload>? toast = harness.Find("a");
        Assert.Equal(TransitionStatus.Ending, toast?.TransitionStatus);
        Assert.Equal(0, toast?.Height);

        harness.Store.RemoveToast("a", skipOnRemove: true);
        Assert.Null(harness.Find("a"));
    }

    [Fact(DisplayName = "ignores mutations that target an unknown toast")]
    [Trait("Requirement", "TST-EDGE-04")]
    [Trait("Requirement", "TST-STA-08")]
    [Trait("Requirement", "TST-STA-11")]
    public void Ignores_mutations_that_target_an_unknown_toast()
    {
        using ToastHarness<NoPayload> harness = Create([new ToastObject { Id = "a" }]);
        ToastStoreState<NoPayload> before = harness.Store.State;

        harness.Store.RemoveToast("missing");
        harness.Store.CloseToast("missing");
        harness.Store.UpdateToast("missing", toast => toast with { Title = "nope" });

        Assert.Same(before, harness.Store.State);
        Assert.Same(before.Toasts, harness.Store.State.Toasts);
        Assert.Null(harness.Find("a")?.TransitionStatus);
    }

    [Fact(DisplayName = "replaces custom data wholesale when updating a toast")]
    [Trait("Requirement", "TST-STA-09")]
    public void Replaces_custom_data_wholesale_when_updating_a_toast()
    {
        using ToastHarness<Draft> harness = Create<Draft>([new ToastObject<Draft> { Id = "a", Data = new Draft("Draft", 1) }]);

        Draft nextData = new(null, 2);
        harness.Store.UpdateToast("a", toast => toast with { Data = nextData });
        Assert.Same(nextData, harness.Find("a")?.Data);

        harness.Store.UpdateToast("a", toast => toast with { Title = "Saved" });
        Assert.Same(nextData, harness.Find("a")?.Data);

        harness.Store.UpdateToast("a", toast => toast with { Data = null });
        Assert.Null(harness.Find("a")?.Data);
    }

    [Fact(DisplayName = "derives the update from the current toast when given a function")]
    [Trait("Requirement", "TST-STA-09")]
    [Trait("Requirement", "TST-STA-10")]
    public void Derives_the_update_from_the_current_toast_when_given_a_function()
    {
        using ToastHarness<Draft> harness = Create<Draft>([new ToastObject<Draft> { Id = "a", Title = "Draft", Data = new Draft("Draft", 1) }]);

        harness.Store.UpdateToast("a", previous => previous with
        {
            Title = $"{previous.Title} (saved)",
            Data = previous.Data! with { Count = 2 },
        });

        ToastObject<Draft>? toast = harness.Find("a");
        Assert.Equal("Draft (saved)", toast?.Title);
        Assert.Equal(new Draft("Draft", 2), toast?.Data);
        Assert.Equal(1, toast?.UpdateKey);
    }

    [Fact(DisplayName = "passes a toast without custom data to the updater")]
    [Trait("Requirement", "TST-STA-10")]
    public void Passes_a_toast_without_custom_data_to_the_updater()
    {
        using ToastHarness<Draft> harness = Create<Draft>([new ToastObject<Draft> { Id = "a" }]);
        List<ToastObject<Draft>> calls = [];

        harness.Store.UpdateToast("a", previous =>
        {
            calls.Add(previous);
            return previous with { Data = new Draft("Draft", 0) };
        });

        ToastObject<Draft> received = Assert.Single(calls);
        Assert.Equal("a", received.Id);
        Assert.Null(received.Data);
        Assert.Equal(new Draft("Draft", 0), harness.Find("a")?.Data);
    }

    [Fact(DisplayName = "stores a function passed as custom data instead of calling it")]
    [Trait("Requirement", "TST-STA-27")]
    public void Stores_a_function_passed_as_custom_data_instead_of_calling_it()
    {
        int calls = 0;
        using ToastHarness<Func<string>> harness = Create<Func<string>>([new ToastObject<Func<string>> { Id = "a", Data = () => Count("first") }]);
        Func<string> nextValue = () => Count("second");

        harness.Store.UpdateToast("a", toast => toast with { Data = nextValue });

        Assert.Same(nextValue, harness.Find("a")?.Data);
        Assert.Equal(0, calls);

        string Count(string value)
        {
            calls++;
            return value;
        }
    }

    [Fact(DisplayName = "does not call the updater for a missing or ending toast")]
    [Trait("Requirement", "TST-STA-08")]
    public void Does_not_call_the_updater_for_a_missing_or_ending_toast()
    {
        Draft data = new("Draft", 0);
        using ToastHarness<Draft> harness = Create<Draft>([new ToastObject<Draft> { Id = "a", Data = data }]);
        harness.Store.CloseToast("a");
        Assert.Equal(TransitionStatus.Ending, harness.Find("a")?.TransitionStatus);

        int calls = 0;
        ToastObject<Draft> Updater(ToastObject<Draft> toast)
        {
            calls++;
            return toast;
        }

        harness.Store.UpdateToast("a", Updater);
        harness.Store.UpdateToast("missing", Updater);

        Assert.Equal(0, calls);
        Assert.Same(data, harness.Find("a")?.Data);
    }

    [Fact(DisplayName = "keeps a toast added from inside an updater")]
    [Trait("Requirement", "TST-STA-01")]
    [Trait("Requirement", "TST-STA-10")]
    public void Keeps_a_toast_added_from_inside_an_updater()
    {
        using ToastHarness<Draft> harness = Create<Draft>([new ToastObject<Draft> { Id = "a", Data = new Draft(null, 1) }]);

        harness.Store.UpdateToast("a", toast =>
        {
            harness.Store.AddToast(new ToastObject<Draft> { Id = "b" });
            return toast with { Data = new Draft(null, 2) };
        });

        Assert.Equal(["b", "a"], harness.Toasts.Select(toast => toast.Id));
        Assert.Equal(new Draft(null, 2), harness.Find("a")?.Data);
        ExpectToastMetadataToMatchToasts(harness.Store);
    }

    [Fact(DisplayName = "keeps a toast closed from inside its updater closed")]
    [Trait("Requirement", "TST-STA-10")]
    public void Keeps_a_toast_closed_from_inside_its_updater_closed()
    {
        using ToastHarness<Draft> harness = Create<Draft>([new ToastObject<Draft> { Id = "a", Data = new Draft(null, 1) }]);

        harness.Store.UpdateToast("a", toast =>
        {
            harness.Store.CloseToast("a");
            return toast with { Data = new Draft(null, 2) };
        });

        Assert.Equal(TransitionStatus.Ending, harness.Find("a")?.TransitionStatus);
        Assert.Equal(new Draft(null, 1), harness.Find("a")?.Data);
    }

    [Fact(DisplayName = "stores a function as the value when re-adding a toast under an existing id")]
    [Trait("Requirement", "TST-STA-05")]
    [Trait("Requirement", "TST-STA-27")]
    public void Stores_a_function_as_the_value_when_re_adding_a_toast_under_an_existing_id()
    {
        using ToastHarness<Func<string>> harness = Create<Func<string>>([]);
        Func<string> value = () => "first";
        Func<string> nextValue = () => "second";

        harness.Store.AddToast(new ToastObject<Func<string>> { Id = "a", Data = value });
        harness.Store.AddToast(new ToastObject<Func<string>> { Id = "a", Data = nextValue });

        Assert.Same(nextValue, harness.Find("a")?.Data);
    }

    [Fact(DisplayName = "replaces custom data when re-adding a toast under an existing id")]
    [Trait("Requirement", "TST-STA-05")]
    public void Replaces_custom_data_when_re_adding_a_toast_under_an_existing_id()
    {
        using ToastHarness<Draft> harness = Create<Draft>([]);

        harness.Store.AddToast(new ToastObject<Draft> { Id = "a", Data = new Draft("error", 42) });
        Draft ok = new("ok", 0);
        harness.Store.AddToast(new ToastObject<Draft> { Id = "a", Data = ok });
        Assert.Same(ok, harness.Find("a")?.Data);

        // An add input that does not set the data leaves it alone.
        harness.Store.AddToast(new ToastObject<Draft> { Id = "a", Title = "Still uploading" });
        Assert.Same(ok, harness.Find("a")?.Data);
        Assert.Equal("Still uploading", harness.Find("a")?.Title);
    }

    [Fact(DisplayName = "stores a function passed as custom data on a promise toast")]
    [Trait("Requirement", "TST-STA-27")]
    public async Task Stores_a_function_passed_as_custom_data_on_a_promise_toast()
    {
        using ToastHarness<Func<string>> harness = Create<Func<string>>([]);
        Func<string> loadingValue = () => "loading";
        Func<string> successValue = () => "done";

        TaskCompletionSource<string> source = new();
        Task<string> pending = harness.Store.PromiseToast(source.Task, new ToastPromiseOptions<string, Func<string>>
        {
            Loading = new ToastObject<Func<string>> { Title = "Saving", Data = loadingValue },
            Success = new ToastObject<Func<string>> { Title = "Saved", Data = successValue },
            Error = "Failed",
        });
        Assert.Same(loadingValue, harness.Toasts[0].Data);

        source.SetResult("done");
        Assert.Equal("done", await pending);
        Assert.Same(successValue, harness.Toasts[0].Data);
        Assert.Equal("Saved", harness.Toasts[0].Title);
    }

    [Fact(DisplayName = "does not invoke onRemove for a toast that is no longer in the store")]
    [Trait("Requirement", "TST-STA-13")]
    [Trait("Requirement", "TST-EVT-03")]
    public void Does_not_invoke_onRemove_for_a_toast_that_is_no_longer_in_the_store()
    {
        int removed = 0;
        using ToastHarness<NoPayload> harness = Create([new ToastObject { Id = "a", OnRemove = () => removed++ }]);

        harness.Store.RemoveToast("a");
        Assert.Equal(1, removed);

        // Removing again must be a no-op rather than firing the callback a second time.
        harness.Store.RemoveToast("a");
        Assert.Equal(1, removed);
    }

    [Fact(DisplayName = "recomputes limited flags when the limit changes")]
    [Trait("Requirement", "TST-STA-03")]
    [Trait("Requirement", "TST-STA-04")]
    public void Recomputes_limited_flags_when_the_limit_changes()
    {
        // Ordered newest first, as the add prepends.
        using ToastHarness<NoPayload> harness = Create([new ToastObject { Id = "c" }, new ToastObject { Id = "b" }, new ToastObject { Id = "a" }]);

        harness.Store.SyncProviderProps(0, 1);
        Assert.False(harness.Find("c")?.Limited);
        Assert.True(harness.Find("b")?.Limited);
        Assert.True(harness.Find("a")?.Limited);

        harness.Store.SyncProviderProps(0, 3);
        Assert.False(harness.Find("c")?.Limited);
        Assert.False(harness.Find("b")?.Limited);
        Assert.False(harness.Find("a")?.Limited);
    }

    [Fact(DisplayName = "re-pauses timers after the last toast is closed and a new one is added")]
    [Trait("Requirement", "TST-STA-24")]
    [Trait("Requirement", "TST-STA-15")]
    public void Re_pauses_timers_after_the_last_toast_is_closed_and_a_new_one_is_added()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        // A toast with a running timer, paused as a hover would.
        store.AddToast(new ToastObject { Title = "a", Timeout = 100 });
        store.PauseTimers();

        // Closing the last toast clears the paused flag, or the next toast's timer could never be paused again.
        store.CloseToast(store.State.Toasts[0].Id);

        store.AddToast(new ToastObject { Title = "b", Timeout = 100 });
        string newToastId = store.State.Toasts[0].Id;

        store.PauseTimers();
        harness.Advance(200);

        Assert.True(harness.IsOpen(newToastId));
    }

    [Fact(DisplayName = "re-pauses timers after all toasts are closed and a new one is added")]
    [Trait("Requirement", "TST-STA-24")]
    [Trait("Requirement", "TST-STA-25")]
    public void Re_pauses_timers_after_all_toasts_are_closed_and_a_new_one_is_added()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 100 });
        store.AddToast(new ToastObject { Id = "b", Title = "b", Timeout = 100 });
        store.PauseTimers();

        store.CloseToast();

        store.AddToast(new ToastObject { Id = "c", Title = "c", Timeout = 100 });
        store.PauseTimers();
        harness.Advance(200);

        Assert.True(harness.IsOpen("c"));
    }

    [Fact(DisplayName = "re-pauses timers after the last active toast closes while ending toasts remain")]
    [Trait("Requirement", "TST-STA-24")]
    public void Re_pauses_timers_after_the_last_active_toast_closes_while_ending_toasts_remain()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 100 });
        store.AddToast(new ToastObject { Id = "b", Title = "b", Timeout = 100 });
        store.PauseTimers();

        store.CloseToast("b");
        store.CloseToast("a");

        store.AddToast(new ToastObject { Id = "c", Title = "c", Timeout = 100 });
        store.PauseTimers();
        harness.Advance(200);

        Assert.True(harness.IsOpen("c"));
    }

    [Fact(DisplayName = "re-pauses timers after the last timed toast closes while untimed toasts remain")]
    [Trait("Requirement", "TST-STA-18")]
    [Trait("Requirement", "TST-STA-24")]
    public void Re_pauses_timers_after_the_last_timed_toast_closes_while_untimed_toasts_remain()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "loading", Title = "loading", Type = "loading" });
        store.AddToast(new ToastObject { Id = "timed", Title = "timed", Timeout = 100 });
        store.PauseTimers();

        store.CloseToast("timed");

        store.AddToast(new ToastObject { Id = "c", Title = "c", Timeout = 100 });
        store.PauseTimers();
        harness.Advance(200);

        Assert.True(harness.IsOpen("c"));
    }

    [Fact(DisplayName = "keeps a rescheduled timer paused while expanded, and runs it once collapsed")]
    [Trait("Requirement", "TST-STA-19")]
    [Trait("Requirement", "TST-STA-23")]
    public void Keeps_a_rescheduled_timer_paused_while_expanded_and_runs_it_once_collapsed()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 100 });

        // Hovering the viewport pauses the running timer.
        store.SetHovering(true);
        store.PauseTimers();

        // Assigning the timeout reschedules the timer; the replacement must not run while the viewport is expanded.
        store.UpdateToast("a", toast => toast with { Timeout = 100 });

        harness.Advance(200);
        Assert.True(harness.IsOpen("a"));

        // Collapsing must still be able to start the rescheduled timer.
        store.SetHovering(false);
        store.ResumeTimers();

        harness.Advance(100);
        Assert.Equal(TransitionStatus.Ending, harness.Find("a")?.TransitionStatus);
    }

    [Fact(DisplayName = "does not extend the remaining time across repeated pause/resume cycles")]
    [Trait("Requirement", "TST-STA-21")]
    public void Does_not_extend_the_remaining_time_across_repeated_pause_resume_cycles()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 5000 });

        // Two hover cycles, each leaving the timer running for 1000 ms.
        for (int cycle = 0; cycle < 2; cycle++)
        {
            harness.Advance(1000);
            store.PauseTimers();
            harness.Advance(1000);
            store.ResumeTimers();
        }

        // 2000 ms of the 5000 ms timeout has been consumed, so 3000 ms must remain.
        harness.Advance(2999);
        Assert.True(harness.IsOpen("a"));

        harness.Advance(2);
        Assert.Equal(TransitionStatus.Ending, harness.Find("a")?.TransitionStatus);
    }

    [Fact(DisplayName = "restarts the full delay when the clock jumped past the timeout before pausing")]
    [Trait("Requirement", "TST-STA-22")]
    public void Restarts_the_full_delay_when_the_clock_jumped_past_the_timeout_before_pausing()
    {
        FakeTimeProvider fake = new();
        JumpingTimeProvider clock = new(fake);
        using ToastStore<NoPayload> store = new(0, 3, clock);

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 5000 });

        // A throttled background tab: wall-clock time passes without the scheduled timer ever running.
        clock.Jump(TimeSpan.FromSeconds(60));
        store.PauseTimers();
        store.ResumeTimers();

        fake.Advance(TimeSpan.FromMilliseconds(4999));
        Assert.NotEqual(TransitionStatus.Ending, ToastSelectors<NoPayload>.Toast(store.State, "a")?.TransitionStatus);

        fake.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(TransitionStatus.Ending, ToastSelectors<NoPayload>.Toast(store.State, "a")?.TransitionStatus);
    }

    [Fact(DisplayName = "re-pauses timers after the last timed toast becomes untimed")]
    [Trait("Requirement", "TST-STA-20")]
    [Trait("Requirement", "TST-STA-24")]
    public void Re_pauses_timers_after_the_last_timed_toast_becomes_untimed()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 100 });
        store.PauseTimers();

        store.UpdateToast("a", toast => toast with { Timeout = 0 });
        Assert.False(store.HasTimer("a"));

        store.AddToast(new ToastObject { Id = "b", Title = "b", Timeout = 100 });
        store.PauseTimers();
        harness.Advance(200);

        Assert.True(harness.IsOpen("b"));
    }

    [Fact(DisplayName = "accumulates active time across hover cycles so the toast still dismisses")]
    [Trait("Requirement", "TST-STA-21")]
    public void Accumulates_active_time_across_hover_cycles_so_the_toast_still_dismisses()
    {
        using ToastHarness<NoPayload> harness = Create([]);
        ToastStore<NoPayload> store = harness.Store;

        store.AddToast(new ToastObject { Id = "a", Title = "a", Timeout = 100 });

        harness.Advance(40);
        store.PauseTimers();
        Assert.True(harness.IsOpen("a"));

        store.ResumeTimers();
        harness.Advance(40);
        store.PauseTimers();
        Assert.True(harness.IsOpen("a"));

        store.ResumeTimers();
        harness.Advance(40);

        Assert.Equal(TransitionStatus.Ending, harness.Find("a")?.TransitionStatus);
    }

    private static ToastHarness<NoPayload> Create(ToastObject<NoPayload>[] toasts) => new(timeout: 0, toasts: toasts);

    private static ToastHarness<TData> Create<TData>(ToastObject<TData>[] toasts) => new(timeout: 0, toasts: toasts);

    // store.test.ts:19-37.
    private static void ExpectToastMetadataToMatchToasts<TData>(ToastStore<TData> store)
    {
        int visibleIndex = 0;
        double offsetY = 0;
        ToastStoreState<TData> state = store.State;
        for (int index = 0; index < state.Toasts.Count; index++)
        {
            ToastObject<TData> toast = state.Toasts[index];
            bool ending = toast.TransitionStatus == TransitionStatus.Ending;
            Assert.Same(toast, ToastSelectors<TData>.Toast(state, toast.Id));
            Assert.Equal(index, ToastSelectors<TData>.ToastIndex(state, toast.Id));
            Assert.Equal(offsetY, ToastSelectors<TData>.ToastOffsetY(state, toast.Id));
            Assert.Equal(ending ? -1 : visibleIndex, ToastSelectors<TData>.ToastVisibleIndex(state, toast.Id));
            offsetY += toast.Height ?? 0;
            if (!ending)
            {
                visibleIndex++;
            }
        }
    }
}

internal sealed record Draft(string? Name, int Count);
