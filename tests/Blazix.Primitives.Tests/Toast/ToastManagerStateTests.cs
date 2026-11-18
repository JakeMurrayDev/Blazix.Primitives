// Family tests use the project's root namespace: a `Toast` namespace would read as a family type.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The store behaviour behind <c>toast/useToastManager.test.tsx</c> at L1 (TST §4; ADR 06 §1). Base UI's file runs only
/// under JSDOM with a rendered Provider; the titles its <c>[L1]</c> rows cite are kept verbatim here and drive the manager
/// and its provider link without a renderer (ADR 13 §3; the dated notes on the rows). What they assert about rendered
/// text, attributes and removal is the L2 titles' (<c>tests/Blazix.Primitives.Tests.Components/Toast/</c>).
/// </summary>
public sealed class ToastManagerStateTests
{
    [Fact(DisplayName = "adds a toast to the viewport that auto-dismisses after 5s by default")]
    [Trait("Requirement", "TST-STA-07")]
    [Trait("Requirement", "TST-STA-28")]
    public void Adds_a_toast_to_the_viewport_that_auto_dismisses_after_5s_by_default()
    {
        using ToastHarness<NoPayload> harness = new(timeout: ToastStore<NoPayload>.ProviderTimeout(null));
        string id = harness.Manager.Add(new ToastObject { Title = "title" });
        Assert.Equal(0, harness.Find(id)?.UpdateKey);
        Assert.Equal(TransitionStatus.Starting, harness.Find(id)?.TransitionStatus);

        harness.Advance(4999);
        Assert.True(harness.IsOpen(id));

        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "replaces a closing toast when adding again with the same id")]
    [Trait("Requirement", "TST-STA-06")]
    public void Replaces_a_closing_toast_when_adding_again_with_the_same_id()
    {
        using ToastHarness<NoPayload> harness = new();
        harness.Manager.Add(new ToastObject { Id = "same", Title = "first" });
        harness.Manager.Close("same");
        Assert.Equal(TransitionStatus.Ending, harness.Find("same")?.TransitionStatus);

        harness.Manager.Add(new ToastObject { Id = "same", Title = "second" });

        ToastObject<NoPayload> toast = Assert.Single(harness.Toasts);
        Assert.Equal("second", toast.Title);
        Assert.Equal(TransitionStatus.Starting, toast.TransitionStatus);
        Assert.Equal(0, toast.UpdateKey);
    }

    [Fact(DisplayName = "does not call onRemove when replacing an ending toast")]
    [Trait("Requirement", "TST-STA-06")]
    [Trait("Requirement", "TST-EVT-04")]
    public void Does_not_call_onRemove_when_replacing_an_ending_toast()
    {
        using ToastHarness<NoPayload> harness = new();
        int removed = 0;
        harness.Manager.Add(new ToastObject { Id = "same", Title = "first", OnRemove = () => removed++ });
        harness.Manager.Close("same");

        harness.Manager.Add(new ToastObject { Id = "same", Title = "second" });

        Assert.Equal(0, removed);
        Assert.Equal("second", Assert.Single(harness.Toasts).Title);
    }

    [Fact(DisplayName = "calls onRemove once after replacing an ending toast and later removing the replacement")]
    [Trait("Requirement", "TST-STA-06")]
    [Trait("Requirement", "TST-EVT-04")]
    public void Calls_onRemove_once_after_replacing_an_ending_toast_and_later_removing_the_replacement()
    {
        using ToastHarness<NoPayload> harness = new();
        int firstRemoved = 0;
        int secondRemoved = 0;
        harness.Manager.Add(new ToastObject { Id = "same", Title = "first", OnRemove = () => firstRemoved++ });
        harness.Manager.Close("same");
        harness.Manager.Add(new ToastObject { Id = "same", Title = "second", OnRemove = () => secondRemoved++ });

        harness.Manager.Close("same");
        harness.Store.RemoveToast("same");
        harness.Store.RemoveToast("same");

        Assert.Equal(0, firstRemoved);
        Assert.Equal(1, secondRemoved);
        Assert.Empty(harness.Toasts);
    }

    [Fact(DisplayName = "ignores transitionStatus when upserting an existing toast")]
    [Trait("Requirement", "TST-API-11")]
    [Trait("Requirement", "TST-STA-05")]
    public void Ignores_transitionStatus_when_upserting_an_existing_toast()
    {
        // The add input cannot carry a transition status: the property's init accessor is internal. What the title asserts
        // of the store is that an upsert keeps the toast's own status, here the settled status after its measurement.
        using ToastHarness<NoPayload> harness = new();
        harness.Manager.Add(new ToastObject { Id = "same", Title = "first" });
        harness.Store.SetMeasuredHeight("same", 40);
        Assert.Null(harness.Find("same")?.TransitionStatus);

        harness.Manager.Add(new ToastObject { Id = "same", Title = "second" });

        Assert.Null(harness.Find("same")?.TransitionStatus);
        Assert.Equal("second", harness.Find("same")?.Title);
        Assert.Equal(40, harness.Find("same")?.Height);
    }

    [Fact(DisplayName = "increments updateKey when adding again with the same id")]
    [Trait("Requirement", "TST-STA-05")]
    [Trait("Requirement", "TST-STA-07")]
    public void Increments_updateKey_when_adding_again_with_the_same_id()
    {
        using ToastHarness<NoPayload> harness = new();
        harness.Manager.Add(new ToastObject { Id = "same", Title = "first" });
        Assert.Equal(0, harness.Find("same")?.UpdateKey);

        harness.Manager.Add(new ToastObject { Id = "same", Title = "second" });
        Assert.Equal(1, harness.Find("same")?.UpdateKey);

        harness.Manager.Add(new ToastObject { Id = "same", Title = "third" });
        Assert.Equal(2, harness.Find("same")?.UpdateKey);
        Assert.Single(harness.Toasts);
    }

    [Fact(DisplayName = "dismisses the toast after the specified timeout")]
    [Trait("Requirement", "TST-STA-18")]
    public void Dismisses_the_toast_after_the_specified_timeout()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title", Timeout = 1000 });

        harness.Advance(999);
        Assert.True(harness.IsOpen(id));

        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "calls onClose when the toast is closed")]
    [Trait("Requirement", "TST-STA-11")]
    [Trait("Requirement", "TST-EVT-01")]
    public void Calls_onClose_when_the_toast_is_closed()
    {
        using ToastHarness<NoPayload> harness = new();
        List<TransitionStatus?> statusAtClose = [];
        string id = string.Empty;
        id = harness.Manager.Add(new ToastObject { Title = "title", OnClose = () => statusAtClose.Add(harness.Find(id)?.TransitionStatus) });

        harness.Manager.Close(id);
        harness.Manager.Close(id);

        // Once, after the store marked the toast ending.
        Assert.Equal([TransitionStatus.Ending], statusAtClose);
    }

    [Fact(DisplayName = "calls onClose when the toast auto-dismisses")]
    [Trait("Requirement", "TST-STA-28")]
    [Trait("Requirement", "TST-EVT-01")]
    public void Calls_onClose_when_the_toast_auto_dismisses()
    {
        using ToastHarness<NoPayload> harness = new();
        int closed = 0;
        harness.Manager.Add(new ToastObject { Title = "title", Timeout = 1000, OnClose = () => closed++ });

        harness.Advance(1000);

        Assert.Equal(1, closed);
        Assert.False(harness.Store.AreTimersPaused);
    }

    [Fact(DisplayName = "calls onRemove when the toast is removed")]
    [Trait("Requirement", "TST-STA-13")]
    [Trait("Requirement", "TST-EVT-03")]
    public void Calls_onRemove_when_the_toast_is_removed()
    {
        using ToastHarness<NoPayload> harness = new();
        List<int> countAtRemove = [];
        string id = harness.Manager.Add(new ToastObject { Title = "title", OnRemove = () => countAtRemove.Add(harness.Toasts.Count) });
        harness.Manager.Close(id);
        Assert.Empty(countAtRemove);

        // The root's completion watcher removes the toast; onRemove runs before the list write.
        harness.Store.RemoveToast(id);

        Assert.Equal([1], countAtRemove);
        Assert.Empty(harness.Toasts);
    }

    [Fact(DisplayName = "displays success state as description after promise resolves")]
    [Trait("Requirement", "TST-API-07")]
    [Trait("Requirement", "TST-API-12")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-EVT-07")]
    public async Task Displays_success_state_as_description_after_promise_resolves()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource<string> source = new();
        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string> { Loading = "loading", Success = "success", Error = "error" });
        Assert.Equal("loading", harness.Toasts[0].Description);
        Assert.False(handled.IsCompleted);

        source.SetResult("done");

        Assert.Equal("done", await handled);
        Assert.Equal("success", harness.Toasts[0].Description);
    }

    [Fact(DisplayName = "displays error state as description after promise rejects")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-EVT-08")]
    public async Task Displays_error_state_as_description_after_promise_rejects()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource<string> source = new();
        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string> { Loading = "loading", Success = "success", Error = "error" });
        InvalidOperationException failure = new("error");

        source.SetException(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => handled));
        Assert.Equal("error", harness.Toasts[0].Description);
    }

    [Fact(DisplayName = "passes data when success is a function")]
    [Trait("Requirement", "TST-API-12")]
    public async Task Passes_data_when_success_is_a_function()
    {
        using ToastHarness<NoPayload> harness = new();
        int calls = 0;
        Task<string> handled = harness.Manager.Promise(Task.FromResult("test success"), new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = new(data =>
            {
                calls++;
                return $"{data}";
            }),
            Error = "error",
        });

        await handled;
        Assert.Equal(1, calls);
        Assert.Equal("test success", harness.Toasts[0].Description);
    }

    [Fact(DisplayName = "accepts a function that returns full options for the success state")]
    [Trait("Requirement", "TST-API-12")]
    public async Task Accepts_a_function_that_returns_full_options_for_the_success_state()
    {
        using ToastHarness<NoPayload> harness = new();
        Task<string> handled = harness.Manager.Promise(Task.FromResult("everything"), new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = new(data => new ToastObject { Title = $"saved {data}", Description = "done", Timeout = 2000 }),
            Error = "error",
        });
        await handled;
        string id = harness.Toasts[0].Id;

        Assert.Equal("saved everything", harness.Find(id)?.Title);
        Assert.Equal("done", harness.Find(id)?.Description);

        // The timeout of the resolved options is honored too.
        harness.Advance(1999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "passes data when error is a function")]
    [Trait("Requirement", "TST-API-12")]
    public async Task Passes_data_when_error_is_a_function()
    {
        using ToastHarness<NoPayload> harness = new();
        Task<string> handled = harness.Manager.Promise(Task.FromException<string>(new InvalidOperationException("test error")), new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = "success",
            Error = new(error => error.Message),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => handled);
        Assert.Equal("test error", harness.Toasts[0].Description);
    }

    [Fact(DisplayName = "supports custom options")]
    [Trait("Requirement", "TST-API-12")]
    public void Supports_custom_options()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource<string> source = new();

        _ = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = new ToastObject { Title = "loading title", Description = "loading description" },
            Success = "success",
            Error = "error",
        });

        Assert.Equal("loading title", harness.Toasts[0].Title);
        Assert.Equal("loading description", harness.Toasts[0].Description);
        Assert.Equal("loading", harness.Toasts[0].Type);
        Assert.False(harness.Store.HasTimer(harness.Toasts[0].Id));
    }

    [Fact(DisplayName = "does not reopen a dismissed promise toast when it resolves")]
    [Trait("Requirement", "TST-STA-08")]
    [Trait("Requirement", "TST-STA-17")]
    public async Task Does_not_reopen_a_dismissed_promise_toast_when_it_resolves()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource<string> source = new();
        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string> { Loading = "loading", Success = "success", Error = "error" });
        string id = harness.Toasts[0].Id;

        harness.Manager.Close(id);
        harness.Store.RemoveToast(id);
        source.SetResult("success");
        await handled;

        Assert.Empty(harness.Toasts);
    }

    [Fact(DisplayName = "auto-dismisses success toast after default timeout when promise resolves")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Auto_dismisses_success_toast_after_default_timeout_when_promise_resolves()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = await Settle(harness, Task.FromResult("ok"), "success", "error");

        harness.Advance(4999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "auto-dismisses error toast after default timeout when promise rejects")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Auto_dismisses_error_toast_after_default_timeout_when_promise_rejects()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = await Settle(harness, Task.FromException<string>(new InvalidOperationException()), "success", "error");
        Assert.Equal("error", harness.Find(id)?.Description);

        harness.Advance(4999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "uses custom timeout from success options when promise resolves")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Uses_custom_timeout_from_success_options_when_promise_resolves()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = await Settle(harness, Task.FromResult("ok"), new ToastObject { Description = "success", Timeout = 2000 }, "error");

        harness.Advance(1999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "uses custom timeout from error options when promise rejects")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Uses_custom_timeout_from_error_options_when_promise_rejects()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = await Settle(harness, Task.FromException<string>(new InvalidOperationException()), "success", new ToastObject { Description = "error", Timeout = 3000 });

        harness.Advance(2999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "uses provider timeout when no custom timeout is specified")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Uses_provider_timeout_when_no_custom_timeout_is_specified()
    {
        using ToastHarness<NoPayload> harness = new(timeout: 1000);
        string id = await Settle(harness, Task.FromResult("ok"), "success", "error");

        harness.Advance(999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "does not inherit a loading timeout when success does not specify one")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Does_not_inherit_a_loading_timeout_when_success_does_not_specify_one()
    {
        using ToastHarness<NoPayload> harness = new();
        TaskCompletionSource<string> source = new();
        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = new ToastObject { Description = "loading", Timeout = 0 },
            Success = "success",
            Error = "error",
        });
        string id = harness.Toasts[0].Id;

        source.SetResult("ok");
        await handled;
        Assert.Equal("success", harness.Find(id)?.Description);

        harness.Advance(5000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "does not auto-dismiss when timeout is set to 0")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-STA-18")]
    public async Task Does_not_auto_dismiss_when_timeout_is_set_to_0()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = await Settle(harness, Task.FromResult("ok"), new ToastObject { Description = "success", Timeout = 0 }, "error");

        harness.Advance(10_000);
        Assert.True(harness.IsOpen(id));
        Assert.False(harness.Store.HasTimer(id));
    }

    [Fact(DisplayName = "updates the toast")]
    [Trait("Requirement", "TST-API-07")]
    [Trait("Requirement", "TST-STA-09")]
    public void Updates_the_toast()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title", Description = "description" });

        harness.Manager.Update(id, toast => toast with { Title = "updated" });

        Assert.Equal("updated", harness.Find(id)?.Title);
        Assert.Equal("description", harness.Find(id)?.Description);
    }

    [Fact(DisplayName = "increments updateKey when updating a toast")]
    [Trait("Requirement", "TST-STA-09")]
    public void Increments_updateKey_when_updating_a_toast()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title" });

        harness.Manager.Update(id, toast => toast with { Title = "one" });
        harness.Manager.Update(id, toast => toast with { Title = "two" });

        Assert.Equal(2, harness.Find(id)?.UpdateKey);
        Assert.Equal(TransitionStatus.Starting, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "schedules a timer when updating a loading toast to a non-loading type")]
    [Trait("Requirement", "TST-STA-18")]
    public void Schedules_a_timer_when_updating_a_loading_toast_to_a_non_loading_type()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "loading", Type = "loading" });

        harness.Advance(10_000);
        Assert.True(harness.IsOpen(id));

        harness.Manager.Update(id, toast => toast with { Type = "success" });
        harness.Advance(4999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "closes a toast")]
    [Trait("Requirement", "TST-API-07")]
    [Trait("Requirement", "TST-STA-11")]
    public void Closes_a_toast()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title" });
        string other = harness.Manager.Add(new ToastObject { Title = "other" });
        ToastObject<NoPayload>? untouched = harness.Find(other);

        harness.Manager.Close(id);

        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
        Assert.Same(untouched, harness.Find(other));
    }

    [Fact(DisplayName = "closes all toasts")]
    [Trait("Requirement", "TST-API-07")]
    [Trait("Requirement", "TST-STA-12")]
    public void Closes_all_toasts()
    {
        using ToastHarness<NoPayload> harness = new();
        List<string> closed = [];
        harness.Manager.Add(new ToastObject { Id = "a", OnClose = () => closed.Add("a") });
        harness.Manager.Add(new ToastObject { Id = "b", OnClose = () => closed.Add("b") });
        harness.Store.SetHovering(true);

        harness.Manager.Close();

        // In list order, newest first; no active toast remains, so the interaction flags are cleared.
        Assert.Equal(["b", "a"], closed);
        Assert.False(harness.Store.State.Hovering);
        Assert.False(harness.Store.HasTimer("a"));
        Assert.False(harness.Store.HasTimer("b"));
    }

    [Fact(DisplayName = "applies a changed timeout to toasts added afterwards")]
    [Trait("Requirement", "TST-STA-29")]
    [Trait("Requirement", "TST-API-01")]
    public void Applies_a_changed_timeout_to_toasts_added_afterwards()
    {
        using ToastHarness<NoPayload> harness = new(timeout: 1000);
        string before = harness.Manager.Add(new ToastObject { Title = "before" });

        harness.Store.SyncProviderProps(3000, 3);
        string after = harness.Manager.Add(new ToastObject { Title = "after" });

        harness.Advance(1000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(before)?.TransitionStatus);
        Assert.True(harness.IsOpen(after));
        harness.Advance(2000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(after)?.TransitionStatus);
    }

    [Fact(DisplayName = "marks toasts as limited when the limit is exceeded")]
    [Trait("Requirement", "TST-STA-03")]
    public void Marks_toasts_as_limited_when_the_limit_is_exceeded()
    {
        using ToastHarness<NoPayload> harness = new(limit: 2);
        string toast1 = harness.Manager.Add(new ToastObject { Title = "toast-1" });
        Assert.False(harness.Find(toast1)?.Limited);
        string toast2 = harness.Manager.Add(new ToastObject { Title = "toast-2" });
        Assert.False(harness.Find(toast2)?.Limited);

        string toast3 = harness.Manager.Add(new ToastObject { Title = "toast-3" });

        Assert.False(harness.Find(toast3)?.Limited);
        Assert.False(harness.Find(toast2)?.Limited);
        Assert.True(harness.Find(toast1)?.Limited);
    }

    [Fact(DisplayName = "unmarks toasts as limited when the limit is not exceeded")]
    [Trait("Requirement", "TST-STA-04")]
    public void Unmarks_toasts_as_limited_when_the_limit_is_not_exceeded()
    {
        using ToastHarness<NoPayload> harness = new(limit: 2);
        string toast1 = harness.Manager.Add(new ToastObject { Title = "toast-1" });
        harness.Manager.Add(new ToastObject { Title = "toast-2" });
        string toast3 = harness.Manager.Add(new ToastObject { Title = "toast-3" });
        Assert.True(harness.Find(toast1)?.Limited);

        harness.Manager.Close(toast3);

        Assert.False(harness.Find(toast1)?.Limited);
    }

    [Fact(DisplayName = "preserves limited state when upserting a limited toast")]
    [Trait("Requirement", "TST-STA-04")]
    [Trait("Requirement", "TST-STA-05")]
    public void Preserves_limited_state_when_upserting_a_limited_toast()
    {
        using ToastHarness<NoPayload> harness = new(limit: 1);
        harness.Manager.Add(new ToastObject { Id = "save", Title = "Saving…", Timeout = 0 });
        Assert.False(harness.Find("save")?.Limited);
        harness.Manager.Add(new ToastObject { Id = "other", Title = "Other toast", Timeout = 0 });
        Assert.True(harness.Find("save")?.Limited);
        Assert.False(harness.Find("other")?.Limited);

        harness.Manager.Add(new ToastObject { Id = "save", Title = "Saved", Timeout = 0 });

        Assert.Equal("Saved", harness.Find("save")?.Title);
        Assert.True(harness.Find("save")?.Limited);
        Assert.False(harness.Find("other")?.Limited);
        Assert.Equal(["other", "save"], harness.Toasts.Select(toast => toast.Id));
    }

    [Fact(DisplayName = "recomputes limited toasts when the limit prop changes")]
    [Trait("Requirement", "TST-STA-04")]
    public void Recomputes_limited_toasts_when_the_limit_prop_changes()
    {
        using ToastHarness<NoPayload> harness = new(limit: 1);
        string toast1 = harness.Manager.Add(new ToastObject { Title = "toast-1" });
        string toast2 = harness.Manager.Add(new ToastObject { Title = "toast-2" });
        Assert.False(harness.Find(toast2)?.Limited);
        Assert.True(harness.Find(toast1)?.Limited);

        // Raising the limit un-limits the older toast.
        harness.Store.SyncProviderProps(5000, 2);
        Assert.False(harness.Find(toast1)?.Limited);

        // Lowering it again re-limits it.
        harness.Store.SyncProviderProps(5000, 1);
        Assert.True(harness.Find(toast1)?.Limited);
    }

    [Fact(DisplayName = "removes expanded on mouseleave when focus-visible not inside")]
    [Trait("Requirement", "TST-STA-14")]
    public void Removes_expanded_on_mouseleave_when_focus_visible_not_inside()
    {
        // ToastViewport.test.tsx: the store's part of it. The mouse leave the module reports clears hovering.
        using ToastHarness<NoPayload> harness = new();
        harness.Manager.Add(new ToastObject { Title = "title" });
        harness.Store.SetHovering(true);
        Assert.True(ToastSelectors<NoPayload>.Expanded(harness.Store.State));

        harness.Store.SetHovering(false);

        Assert.False(ToastSelectors<NoPayload>.Expanded(harness.Store.State));
    }

    [Fact(DisplayName = "keeps expanded on mouseleave when focus-visible is inside")]
    [Trait("Requirement", "TST-STA-14")]
    public void Keeps_expanded_on_mouseleave_when_focus_visible_is_inside()
    {
        using ToastHarness<NoPayload> harness = new();
        harness.Manager.Add(new ToastObject { Title = "title" });
        harness.Store.SetHovering(true);
        harness.Store.SetFocused(true);

        harness.Store.SetHovering(false);

        Assert.True(ToastSelectors<NoPayload>.Expanded(harness.Store.State));
        harness.Store.SetWindowFocused(false);
        harness.Store.SetFocused(false);
        Assert.False(ToastSelectors<NoPayload>.Expanded(harness.Store.State));
        Assert.True(ToastSelectors<NoPayload>.ExpandedOrOutOfFocus(harness.Store.State));
    }

    [Fact(DisplayName = "reflects the expanded state when the viewport is hovered")]
    [Trait("Requirement", "TST-STA-14")]
    public void Reflects_the_expanded_state_when_the_viewport_is_hovered()
    {
        // ToastContent.test.tsx: the selector the Content's data-expanded reads.
        using ToastHarness<NoPayload> harness = new();
        harness.Manager.Add(new ToastObject { Title = "title" });
        Assert.False(ToastSelectors<NoPayload>.Expanded(harness.Store.State));

        harness.Store.SetHovering(true);

        Assert.True(ToastSelectors<NoPayload>.Expanded(harness.Store.State));
    }

    [Fact(DisplayName = "collapses a deferred mouseleave after a closing toast is removed while blurred")]
    [Trait("Requirement", "TST-STA-15")]
    public void Collapses_a_deferred_mouseleave_after_a_closing_toast_is_removed_while_blurred()
    {
        // ToastViewport.test.tsx: the store's part of it. The deferral is the module's; removing the last toast leaves the
        // list empty, which clears hovering and focus.
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title" });
        harness.Store.SetHovering(true);
        harness.Store.SetWindowFocused(false);
        harness.Manager.Close(id);

        // Closing the only active toast already leaves none active.
        Assert.False(harness.Store.State.Hovering);
        harness.Store.SetHovering(true);

        harness.Store.RemoveToast(id);

        Assert.False(harness.Store.State.Hovering);
        Assert.False(ToastSelectors<NoPayload>.Expanded(harness.Store.State));
    }

    [Fact(DisplayName = "pauses timers when hovering")]
    [Trait("Requirement", "TST-STA-21")]
    public void Pauses_timers_when_hovering()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title" });

        harness.Store.PauseTimers();
        harness.Store.SetHovering(true);
        harness.Advance(10_000);

        Assert.True(harness.IsOpen(id));
    }

    [Fact(DisplayName = "resumes timers when not hovering")]
    [Trait("Requirement", "TST-STA-22")]
    public void Resumes_timers_when_not_hovering()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title" });
        harness.Store.SetHovering(true);
        harness.Store.PauseTimers();
        harness.Advance(10_000);

        harness.Store.SetHovering(false);
        harness.Store.ResumeTimers();

        harness.Advance(4999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(2);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "keeps timers paused on viewport blur while the window is blurred")]
    [Trait("Requirement", "TST-STA-23")]
    public void Keeps_timers_paused_on_viewport_blur_while_the_window_is_blurred()
    {
        // ToastViewport.test.tsx: the store's part of it. A toast added while the window is out of focus starts paused,
        // with its full delay, and only a resume starts it.
        using ToastHarness<NoPayload> harness = new();
        harness.Store.SetWindowFocused(false);
        string id = harness.Manager.Add(new ToastObject { Title = "title" });
        Assert.True(harness.Store.AreTimersPaused);

        harness.Advance(10_000);
        Assert.True(harness.IsOpen(id));

        harness.Store.SetWindowFocused(true);
        harness.Store.ResumeTimers();
        harness.Advance(4999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "pauses timers when hovering over toast")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-STA-21")]
    public async Task Pauses_timers_when_hovering_over_toast()
    {
        // useToastManager.test.tsx's promise block: the store's part of it (the hover is the module's report, L4).
        using ToastHarness<NoPayload> harness = new();
        string id = await Settle(harness, Task.FromResult("ok"), new ToastObject { Description = "success", Timeout = 3000 }, "error");

        harness.Store.SetHovering(true);
        harness.Store.PauseTimers();
        harness.Advance(5000);
        Assert.True(harness.IsOpen(id));

        harness.Store.SetHovering(false);
        harness.Store.ResumeTimers();
        harness.Advance(3000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "auto-dismisses when timeout changes from 0 to a positive value")]
    [Trait("Requirement", "TST-STA-19")]
    public void Auto_dismisses_when_timeout_changes_from_0_to_a_positive_value()
    {
        using ToastHarness<NoPayload> harness = new();
        string id = harness.Manager.Add(new ToastObject { Title = "title", Timeout = 0 });
        harness.Advance(10_000);
        Assert.True(harness.IsOpen(id));

        harness.Manager.Update(id, toast => toast with { Timeout = 1000 });
        harness.Advance(999);
        Assert.True(harness.IsOpen(id));
        harness.Advance(1);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    private static async Task<string> Settle(ToastHarness<NoPayload> harness, Task<string> task, ToastPromiseState<string, NoPayload> success, ToastPromiseState<Exception, NoPayload> error)
    {
        Task<string> handled = harness.Manager.Promise(task, new ToastPromiseOptions<string> { Loading = "loading", Success = success, Error = error });
        try
        {
            await handled;
        }
        catch (InvalidOperationException)
        {
            // The error branch was asked for.
        }

        return harness.Toasts[0].Id;
    }
}
