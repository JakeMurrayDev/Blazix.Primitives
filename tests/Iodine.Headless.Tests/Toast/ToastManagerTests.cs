// Family tests use the project's root namespace: a `Toast` namespace would read as a family type.
namespace Iodine.Headless.Tests;

/// <summary>
/// The toast manager at L1 (TST §3.2, TST §4; ADR 06 §1). Base UI's <c>toast/createToastManager.test.tsx</c>, every title
/// verbatim. It runs only under JSDOM with a rendered Provider and list; here the titles drive a
/// <see cref="ToastManager"/> through one provider link, under a fake clock. Base UI's "removed from the DOM" with
/// animations disabled is the toast's close (<see cref="TransitionStatus.Ending"/>): the removal after the exit is the
/// root's, at L2 and L4.
/// </summary>
public sealed class ToastManagerTests
{
    [Fact(DisplayName = "adds a toast")]
    [Trait("Requirement", "TST-API-03")]
    [Trait("Requirement", "TST-API-08")]
    [Trait("Requirement", "TST-EVT-06")]
    public void Adds_a_toast()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());

        Assert.Empty(harness.Manager.Toasts);
        string id = harness.Manager.Add(new ToastObject { Title = "title" });

        // Delivered synchronously to the subscribed provider.
        ToastObject<NoPayload> toast = Assert.Single(harness.Manager.Toasts);
        Assert.Equal("title", toast.Title);
        Assert.Equal(id, toast.Id);
        Assert.Equal(TransitionStatus.Starting, toast.TransitionStatus);

        harness.Advance(5000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "returns a toast id")]
    [Trait("Requirement", "TST-API-06")]
    [Trait("Requirement", "TST-API-08")]
    public void Returns_a_toast_id()
    {
        // No provider: the id is still drawn, and the call is discarded.
        ToastManager manager = new();

        string id = manager.Add(new ToastObject { Title = "title" });

        Assert.Matches("^toast-[0-9a-z]{4}-[0-9]+$", id);
        Assert.Empty(manager.Toasts);
    }

    [Fact(DisplayName = "upserts a toast when adding with an existing id")]
    [Trait("Requirement", "TST-API-06")]
    [Trait("Requirement", "TST-STA-05")]
    public void Upserts_a_toast_when_adding_with_an_existing_id()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());

        string firstToastId = harness.Manager.Add(new ToastObject { Id = "save", Title = "Saving…", Timeout = 1000 });
        Assert.Equal("Saving…", harness.Find("save")?.Title);
        Assert.Single(harness.Toasts);

        harness.Advance(900);

        string secondToastId = harness.Manager.Add(new ToastObject { Id = "save", Title = "Saved", Timeout = 1000 });
        Assert.Equal("save", firstToastId);
        Assert.Equal(firstToastId, secondToastId);
        Assert.Equal("Saved", harness.Find("save")?.Title);
        Assert.Single(harness.Toasts);

        // The timer restarted from the full duration at the upsert.
        harness.Advance(200);
        Assert.True(harness.IsOpen("save"));

        harness.Advance(800);
        Assert.Equal(TransitionStatus.Ending, harness.Find("save")?.TransitionStatus);
    }

    [Fact(DisplayName = "adds a toast with the loading state that is updated with the success state")]
    [Trait("Requirement", "TST-STA-16")]
    [Trait("Requirement", "TST-EVT-07")]
    public async Task Adds_a_toast_with_the_loading_state_that_is_updated_with_the_success_state()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        TaskCompletionSource<string> source = new();

        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = "success",
            Error = "error",
        });

        Assert.Equal("loading", harness.Toasts[0].Description);
        Assert.Equal("loading", harness.Toasts[0].Type);

        source.SetResult("value");
        Assert.Equal("value", await handled);
        Assert.Equal("success", harness.Toasts[0].Description);
        Assert.Equal("success", harness.Toasts[0].Type);
    }

    [Fact(DisplayName = "does not inherit a loading timeout when success does not specify one")]
    [Trait("Requirement", "TST-STA-16")]
    public async Task Does_not_inherit_a_loading_timeout_when_success_does_not_specify_one()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        TaskCompletionSource<string> source = new();

        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = new ToastObject { Description = "loading", Timeout = 0 },
            Success = new ToastObject { Description = "success" },
            Error = "error",
        });
        string id = harness.Toasts[0].Id;

        harness.Advance(1000);
        Assert.Equal("loading", harness.Find(id)?.Description);

        source.SetResult("value");
        await handled;
        Assert.Equal("success", harness.Find(id)?.Description);
        Assert.Null(harness.Find(id)?.Timeout);

        // The provider's 5000 ms, not the loading toast's 0.
        harness.Advance(5000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "adds a toast with the loading state that is updated with the error state")]
    [Trait("Requirement", "TST-API-09")]
    [Trait("Requirement", "TST-EVT-08")]
    public async Task Adds_a_toast_with_the_loading_state_that_is_updated_with_the_error_state()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        TaskCompletionSource<string> source = new();
        InvalidOperationException failure = new("error");

        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = "success",
            Error = "error",
        });
        Assert.NotSame(source.Task, handled);

        source.SetException(failure);
        InvalidOperationException caught = await Assert.ThrowsAsync<InvalidOperationException>(() => handled);
        Assert.Same(failure, caught);
        Assert.Equal("error", harness.Toasts[0].Description);
        Assert.Equal("error", harness.Toasts[0].Type);
    }

    [Fact(DisplayName = "does not reopen a dismissed promise toast when it resolves")]
    [Trait("Requirement", "TST-STA-08")]
    [Trait("Requirement", "TST-STA-17")]
    public async Task Does_not_reopen_a_dismissed_promise_toast_when_it_resolves()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        TaskCompletionSource<string> source = new();

        Task<string> handled = harness.Manager.Promise(source.Task, new ToastPromiseOptions<string>
        {
            Loading = "loading",
            Success = "success",
            Error = "error",
        });
        string id = harness.Toasts[0].Id;
        Assert.Equal("loading", harness.Find(id)?.Description);

        harness.Manager.Close(id);
        source.SetResult("value");
        Assert.Equal("value", await handled);

        ToastObject<NoPayload>? toast = harness.Find(id);
        Assert.Equal(TransitionStatus.Ending, toast?.TransitionStatus);
        Assert.Equal("loading", toast?.Description);
        Assert.False(harness.Store.HasTimer(id));
    }

    [Fact(DisplayName = "updates a toast")]
    [Trait("Requirement", "TST-STA-09")]
    public void Updates_a_toast()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "title" });

        harness.Manager.Update(id, toast => toast with { Title = "updated" });

        Assert.Equal("updated", harness.Find(id)?.Title);
    }

    [Fact(DisplayName = "derives the update from the current toast when given a function")]
    [Trait("Requirement", "TST-API-08")]
    [Trait("Requirement", "TST-STA-10")]
    public void Derives_the_update_from_the_current_toast_when_given_a_function()
    {
        using ToastHarness<Draft> harness = new();
        List<ToastObject<Draft>> calls = [];
        string id = harness.Manager.Add(new ToastObject<Draft> { Title = "title", Data = new Draft(null, 1) });

        harness.Manager.Update(id, previous =>
        {
            calls.Add(previous);
            return previous with { Title = $"{previous.Title} updated" };
        });

        Assert.Equal(new Draft(null, 1), Assert.Single(calls).Data);
        Assert.Equal("title updated", harness.Find(id)?.Title);
    }

    [Fact(DisplayName = "resets the auto-dismiss timer when updating with the same timeout value")]
    [Trait("Requirement", "TST-STA-19")]
    public void Resets_the_auto_dismiss_timer_when_updating_with_the_same_timeout_value()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "title", Timeout = 1000 });

        harness.Advance(900);
        Assert.True(harness.IsOpen(id));

        harness.Manager.Update(id, toast => toast with { Timeout = 1000 });

        harness.Advance(200);
        Assert.True(harness.IsOpen(id));

        harness.Advance(800);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "resets the auto-dismiss timer when updating from 0 to a timeout, then updating with the same timeout again")]
    [Trait("Requirement", "TST-STA-19")]
    public void Resets_the_auto_dismiss_timer_when_updating_from_0_to_a_timeout_then_updating_with_the_same_timeout_again()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "title", Timeout = 0 });

        harness.Manager.Update(id, toast => toast with { Timeout = 1000 });
        harness.Advance(900);
        Assert.True(harness.IsOpen(id));

        harness.Manager.Update(id, toast => toast with { Timeout = 1000 });
        harness.Advance(200);
        Assert.True(harness.IsOpen(id));

        harness.Advance(800);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "auto-dismisses when updating timeout from 0 to a positive value")]
    [Trait("Requirement", "TST-STA-19")]
    public void Auto_dismisses_when_updating_timeout_from_0_to_a_positive_value()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "title", Timeout = 0 });
        Assert.False(harness.Store.HasTimer(id));

        harness.Manager.Update(id, toast => toast with { Timeout = 1000 });
        harness.Advance(1000);

        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "schedules a timer when updating a loading toast to a non-loading type")]
    [Trait("Requirement", "TST-STA-18")]
    [Trait("Requirement", "TST-STA-19")]
    public void Schedules_a_timer_when_updating_a_loading_toast_to_a_non_loading_type()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "loading", Type = "loading" });
        Assert.False(harness.Store.HasTimer(id));

        harness.Manager.Update(id, toast => toast with { Title = "success", Type = "success", Timeout = 1000 });
        Assert.Equal("success", harness.Find(id)?.Title);

        harness.Advance(1000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "does not clear the auto-dismiss timer when updated twice before a re-render")]
    [Trait("Requirement", "TST-STA-19")]
    public void Does_not_clear_the_auto_dismiss_timer_when_updated_twice_before_a_re_render()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "loading", Type = "loading" });

        harness.Manager.Update(id, toast => toast with { Type = "success", Timeout = 1000 });
        harness.Manager.Update(id, toast => toast with { Title = "new" });
        Assert.Equal("new", harness.Find(id)?.Title);

        harness.Advance(1000);
        Assert.Equal(TransitionStatus.Ending, harness.Find(id)?.TransitionStatus);
    }

    [Fact(DisplayName = "closes a toast")]
    [Trait("Requirement", "TST-STA-11")]
    public void Closes_a_toast()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        string id = harness.Manager.Add(new ToastObject { Title = "title" });

        harness.Manager.Close(id);

        ToastObject<NoPayload>? toast = harness.Find(id);
        Assert.Equal(TransitionStatus.Ending, toast?.TransitionStatus);
        Assert.Equal(0, toast?.Height);
        Assert.False(harness.Store.HasTimer(id));
    }

    [Fact(DisplayName = "closes all toasts")]
    [Trait("Requirement", "TST-STA-12")]
    public void Closes_all_toasts()
    {
        using ToastHarness<NoPayload> harness = new(manager: new ToastManager());
        harness.Manager.Add(new ToastObject { Title = "title" });
        harness.Manager.Add(new ToastObject { Title = "title" });

        harness.Manager.Close();

        Assert.All(harness.Toasts, toast => Assert.Equal(TransitionStatus.Ending, toast.TransitionStatus));
        Assert.Equal(2, harness.Toasts.Count);
        Assert.False(harness.Store.AreTimersPaused);
    }

    [Fact(DisplayName = "does not call onClose when closing toasts that are already ending")]
    [Trait("Requirement", "TST-EVT-02")]
    [Trait("Requirement", "TST-EVT-10")]
    [Trait("Requirement", "TST-STA-12")]
    public void Does_not_call_onClose_when_closing_toasts_that_are_already_ending()
    {
        ToastManager manager = new();
        using ToastHarness<NoPayload> harness = new(manager: manager);
        int closed1 = 0;
        int closed2 = 0;
        string toastId1 = manager.Add(new ToastObject
        {
            Title = "toast 1",
            OnClose = () =>
            {
                closed1++;
                manager.Close();
            },
        });
        manager.Add(new ToastObject { Title = "toast 2", OnClose = () => closed2++ });

        manager.Close(toastId1);

        Assert.Equal(1, closed1);
        Assert.Equal(1, closed2);
        Assert.All(harness.Toasts, toast => Assert.Equal(TransitionStatus.Ending, toast.TransitionStatus));
    }
}
