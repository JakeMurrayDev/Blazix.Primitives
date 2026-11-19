using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// DP3 (i) of the Toast track (chosen by the user 2026-11-19): a close's focus hand-off.
/// <summary>The focus hand-off a close computed.</summary>
/// <param name="Revision">The close's number, increasing with each close.</param>
/// <param name="TargetId">The id of the toast to focus; <see langword="null"/> to return focus to the element focused before F6.</param>
internal sealed record ToastHandOff(int Revision, string? TargetId);

// TST §4.1 (toast/store.ts:25-35); M6 track 9a design point 2 (a), pending the user: one ComponentStore per Provider, the
// timers, the paused flag and (from 9b) the element handles and the previous focus in non-notifying fields.
/// <summary>Holds the state of one toast provider.</summary>
/// <typeparam name="TData">The type of the custom data the toasts carry.</typeparam>
/// <param name="Toasts">The toasts, newest first.</param>
/// <param name="Hovering">Whether the pointer is over the viewport, or a swipe is in progress.</param>
/// <param name="Focused">Whether keyboard focus is inside the viewport.</param>
/// <param name="IsWindowFocused">Whether the browser window has focus.</param>
/// <param name="Timeout">The provider's auto-dismiss delay in milliseconds.</param>
/// <param name="Limit">The provider's limit of toasts shown at once.</param>
/// <param name="HasPreviousFocus">Whether F6 has recorded the element focused before it, which the focus guards return to.</param>
[StoreState]
internal sealed partial record ToastStoreState<TData>(
    ToastList<TData> Toasts,
    bool Hovering,
    bool Focused,
    bool IsWindowFocused,
    double Timeout,
    int Limit,
    bool HasPreviousFocus = false)
    : IStoreState<ToastStoreState<TData>>;

// TST §4.1, the selectors of store.ts:86-97. Named selectors are static readonly fields (UTL-STA-17).
internal static class ToastSelectors<TData>
{
    public static readonly Func<ToastStoreState<TData>, IReadOnlyList<ToastObject<TData>>> Toasts = static state => state.Toasts;

    public static readonly Func<ToastStoreState<TData>, bool> IsEmpty = static state => state.Toasts.Count == 0;

    public static readonly Func<ToastStoreState<TData>, string, ToastObject<TData>?> Toast = static (state, id) => state.Toasts.Find(id);

    public static readonly Func<ToastStoreState<TData>, string, int> ToastIndex = static (state, id) => state.Toasts.DomIndex(id);

    public static readonly Func<ToastStoreState<TData>, string, double> ToastOffsetY = static (state, id) => state.Toasts.OffsetY(id);

    public static readonly Func<ToastStoreState<TData>, string, int> ToastVisibleIndex = static (state, id) => state.Toasts.VisibleIndex(id);

    public static readonly Func<ToastStoreState<TData>, bool> Focused = static state => state.Focused;

    // TST-DOM-04, TST-FOC-13: the guards exist while the list is non-empty and F6 has recorded the previous focus.
    public static readonly Func<ToastStoreState<TData>, bool> Guarded = static state => state.Toasts.Count > 0 && state.HasPreviousFocus;

    // TST-PTR-03: a toast is ending, which defers the collapse of a pointer leaving.
    public static readonly Func<ToastStoreState<TData>, bool> HasEnding = static state => state.Toasts.HasEnding;

    // TST-STA-14
    public static readonly Func<ToastStoreState<TData>, bool> Expanded = static state => state.Hovering || state.Focused;

    // TST-STA-14
    public static readonly Func<ToastStoreState<TData>, bool> ExpandedOrOutOfFocus =
        static state => state.Hovering || state.Focused || !state.IsWindowFocused;
}

// TST §4 (toast/store.ts:99-516). Every write runs on the renderer's synchronization context: the provider's link
// marshals manager calls there (ADR 05 Amendments 2026-11-18 item 3), and `dispatch` brings timer callbacks and task
// settlements back to it. The timers are C# timers on the provider's TimeProvider (ADR 01 decision 4; the M6 plan's
// decision 19 (a), chosen by the user 2026-11-18), one per timed toast, with Base UI's pause and resume arithmetic.
internal sealed class ToastStore<TData> : ComponentStore<ToastStoreState<TData>>, IDisposable
{
    private static readonly NoPayload s_noValue = new();

    private readonly TimeProvider time;
    private readonly Func<Action, Task> dispatch;
    private readonly Dictionary<string, ToastTimer> timers = new(StringComparer.Ordinal);
    // DP3 (i) of the Toast track (chosen by the user 2026-11-19): the element-handle cell (store.ts's `ref` field), each
    // Root's element by its toast's id, the latest registration winning (TST-ANI-08, TST-ANI-09); non-notifying.
    private readonly Dictionary<string, ToastElementRegistration> elements = new(StringComparer.Ordinal);
    private bool areTimersPaused;
    private int handOffRevision;
    private bool disposed;

    // For the fixtures: raised after a part applied one of the `toast` module's reports, with its kind (the readiness marker).
    public event Action<string>? ReportApplied;

    // DP3 (i): raised at each close, so the Viewport sends the hand-off before the render that starts the exit; on
    // WebAssembly an exit with no animation removes the toast, and its focus, within that render's after-render work.
    public event Action? HandOffRequested;

    public ToastStore(double timeout, int limit, TimeProvider? time = null, Func<Action, Task>? dispatch = null, IEnumerable<ToastObject<TData>>? toasts = null)
        : base(new ToastStoreState<TData>(Seed(toasts), false, false, true, timeout, limit))
    {
        this.time = time ?? TimeProvider.System;
        this.dispatch = dispatch ?? RunInline;
    }

    // DP3 (i): the focus hand-off of the latest close, which the Viewport sends to the `toast` module; non-notifying.
    public ToastHandOff? HandOff { get; private set; }

    // For the tests: whether the timers are paused.
    internal bool AreTimersPaused => areTimersPaused;

    // TST-EDGE-06, TST-STA-25: the provider's disposal clears every timer; the store is never reused.
    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
        foreach (ToastTimer timer in timers.Values)
        {
            timer.Stop();
        }

        timers.Clear();
    }

    // TST-API-01: the Provider's `timeout`, 5000 ms when not supplied (ADR 08 Amendments 2026-10-27 and 2026-11-18: null
    // means not supplied, never 0).
    public static double ProviderTimeout(Duration? timeout) => timeout is { } value ? value.ToMilliseconds() : 5000;

    // TST-API-01, TST-API-02, TST-API-04, TST-STA-04, TST-STA-29 (syncProviderProps, store.ts:119-138).
    public void SyncProviderProps(double timeout, int limit)
    {
        ToastStoreState<TData> state = State;
        bool limitChanged = state.Limit != limit;
        if (state.Timeout.Equals(timeout) && !limitChanged)
        {
            return;
        }

        SetState(state with
        {
            Timeout = timeout,
            Limit = limit,
            Toasts = limitChanged ? new ToastList<TData>(ApplyLimited(state.Toasts.Items, limit)) : state.Toasts,
        });
    }

    public void SetHovering(bool hovering) => Update(static (state, value) => state with { Hovering = value }, hovering);

    public void SetFocused(bool focused) => Update(static (state, value) => state with { Focused = value }, focused);

    public void SetWindowFocused(bool focused) => Update(static (state, value) => state with { IsWindowFocused = value }, focused);

    // TST-STA-13, TST-EVT-03, TST-EVT-04 (removeToast, store.ts:149-163).
    public void RemoveToast(string id, bool skipOnRemove = false)
    {
        ToastObject<TData>[] items = State.Toasts.Items;
        int index = State.Toasts.DomIndex(id);
        if (index == -1)
        {
            return;
        }

        if (!skipOnRemove)
        {
            items[index].OnRemove?.Invoke();
        }

        // The callback may have written: splice the current list.
        items = State.Toasts.Items;
        index = State.Toasts.DomIndex(id);
        if (index == -1)
        {
            return;
        }

        ToastObject<TData>[] next = new ToastObject<TData>[items.Length - 1];
        Array.Copy(items, 0, next, 0, index);
        Array.Copy(items, index + 1, next, index, items.Length - index - 1);
        SetToasts(next);
    }

    // TST-API-06, TST-STA-05..07, TST-STA-18 (addToast, store.ts:165-203).
    public string AddToast(ToastObject<TData> toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        string id = toast.Id.Length > 0 ? toast.Id : ToastIds.Next();
        if (toast.Id.Length > 0)
        {
            ToastObject<TData>? existing = State.Toasts.Find(id);
            if (existing is not null)
            {
                if (existing.TransitionStatus == TransitionStatus.Ending)
                {
                    RemoveToast(id, skipOnRemove: true);
                }
                else
                {
                    UpdateToastInternal(id, toast, resetTimer: true, markUpdated: true);
                    return id;
                }
            }
        }

        ToastObject<TData> added = toast.Stored(id, 0, TransitionStatus.Starting);
        ToastObject<TData>[] items = State.Toasts.Items;
        ToastObject<TData>[] next = new ToastObject<TData>[items.Length + 1];
        next[0] = added;
        Array.Copy(items, 0, next, 1, items.Length);
        SetToasts(ApplyLimited(next, State.Limit));

        double duration = Duration(added, State.Timeout);
        if (added.Type != "loading" && duration > 0)
        {
            ScheduleTimer(id, duration);
        }

        if (ToastSelectors<TData>.ExpandedOrOutOfFocus(State))
        {
            PauseTimers();
        }

        return id;
    }

    // TST-STA-08..10 (updateToast, store.ts:205-225): the updater runs once, with the current toast, and never for an
    // update the store is going to ignore; the internal update reads the state again, since the updater may have written.
    public void UpdateToast(string id, Func<ToastObject<TData>, ToastObject<TData>> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        ToastObject<TData>? previous = State.Toasts.Find(id);
        if (previous is null || previous.TransitionStatus == TransitionStatus.Ending)
        {
            return;
        }

        UpdateToastInternal(id, update(previous), resetTimer: false, markUpdated: true);
    }

    // TST-ANI-02, TST-ANI-10: the Root's measurement write, `{ height, transitionStatus: undefined }`, no update key.
    public void SetMeasuredHeight(string id, double height)
    {
        ToastObject<TData>? previous = State.Toasts.Find(id);
        if (previous is null || previous.TransitionStatus == TransitionStatus.Ending)
        {
            return;
        }

        // updateToastInternal without markUpdated: the timer rules run too, though neither key they read is written.
        ToastObject<TData> next = previous with { Height = height, TransitionStatus = null };
        ReplaceToast(id, next);
        RescheduleAfterUpdate(id, previous, next, timeoutUpdated: false, resetTimer: false);
    }

    // TST-STA-11, TST-STA-12, TST-EVT-01, TST-EVT-02, TST-EVT-10, TST-STA-15, TST-STA-25 (closeToast, store.ts:288-320).
    public void CloseToast(string? id = null)
    {
        bool closeAll = id is null;
        ToastObject<TData>[] items = State.Toasts.Items;
        ToastObject<TData>[] toClose;
        if (closeAll)
        {
            toClose = items;
            ClearTimers();
        }
        else
        {
            ToastObject<TData>? toast = State.Toasts.Find(id!);
            if (toast is null)
            {
                return;
            }

            toClose = [toast];
            ClearTimer(id!);
        }

        ToastObject<TData>[] ending = new ToastObject<TData>[items.Length];
        bool anyActive = false;
        for (int i = 0; i < items.Length; i++)
        {
            ToastObject<TData> item = items[i];
            ending[i] = closeAll || item.Id == id ? item with { TransitionStatus = TransitionStatus.Ending, Height = 0 } : item;
        }

        ToastObject<TData>[] limited = ApplyLimited(ending, State.Limit);
        for (int i = 0; i < limited.Length; i++)
        {
            anyActive |= limited[i].TransitionStatus != TransitionStatus.Ending;
        }

        // TST-FOC-07..09 (handleFocusManagement, store.ts:480-515), DP3 (i) of the Toast track (chosen by the user
        // 2026-11-19): C# names the target over the list as closed, the next non-ending toast after the closed one, else the
        // nearest before it, else the element focused before F6; the `toast` module focuses it only while focus-visible focus
        // is inside the viewport. It is sent before the state write: a write can render at once (WebAssembly), and an exit
        // with no animation then removes the closed toast, with the focus it holds, before any later message.
        HandOff = new ToastHandOff(++handOffRevision, closeAll ? null : HandOffTarget(limited, id!));
        HandOffRequested?.Invoke();

        SetToasts(limited, clearInteraction: !anyActive);

        foreach (ToastObject<TData> toast in toClose)
        {
            if (toast.TransitionStatus != TransitionStatus.Ending)
            {
                toast.OnClose?.Invoke();
            }
        }
    }

    // For the fixtures: announces a report a part applied.
    public void OnReportApplied(string kind) => ReportApplied?.Invoke(kind);

    // TST-ANI-02, TST-ANI-08, TST-ANI-09: a Root registers its element under its toast's id; the latest registration wins.
    public void RegisterElement(string id, object owner, Func<Microsoft.AspNetCore.Components.ElementReference?> element) =>
        elements[id] = new ToastElementRegistration(owner, element);

    // A Root that leaves, or moves to another id, withdraws only its own registration.
    public void UnregisterElement(string id, object owner)
    {
        if (elements.TryGetValue(id, out ToastElementRegistration? registration) && ReferenceEquals(registration.Owner, owner))
        {
            elements.Remove(id);
        }
    }

    // The element a Root registered for the toast, if it has one.
    public Microsoft.AspNetCore.Components.ElementReference? ElementOf(string? id) =>
        id is not null && elements.TryGetValue(id, out ToastElementRegistration? registration) ? registration.Element() : null;

    // TST-FOC-02, TST-FOC-10 (handleFocusGuard, ToastViewport.tsx:108-112): the first toast that is neither ending nor limited.
    public string? FirstFocusableToast()
    {
        ToastObject<TData>[] items = State.Toasts.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].TransitionStatus != TransitionStatus.Ending && !items[i].Limited)
            {
                return items[i].Id;
            }
        }

        return null;
    }

    // The reports of the Viewport's role in the `toast` module (ADR 04 Amendments 2026-11-18, the `toast` scope; M6 track
    // 9b), applied as Base UI's handlers write the store (ToastViewport.tsx:57-209; store.ts:402-416).
    public void ApplyViewportReport(string kind, bool resume)
    {
        switch (kind)
        {
            // TST-PTR-01.
            case "enter":
                PauseTimers();
                SetHovering(true);
                break;

            // TST-PTR-02..04 (flushMouseLeave).
            case "leave":
                SetHovering(false);
                ResumeUnlessExpandedOrOutOfFocus();
                break;

            // TST-FOC-05, TST-PTR-25.
            case "focus":
                SetFocused(true);
                PauseTimers();
                break;

            // TST-FOC-06.
            case "blur":
                SetFocused(false);
                ResumeUnlessExpandedOrOutOfFocus();
                break;

            // TST-KEY-01, TST-FOC-01, TST-FOC-13.
            case "f6":
                Update(static (state, _) => state with { HasPreviousFocus = true }, true);
                PauseTimers();
                SetFocused(true);
                break;

            // TST-FOC-11.
            case "window-blur":
                SetWindowFocused(false);
                PauseTimers();
                break;

            // TST-FOC-12.
            case "window-focus":
                if (resume)
                {
                    ResumeTimers();
                }

                break;

            case "window-focused":
                SetWindowFocused(true);
                break;

            // TST-PTR-05.
            case "touch-outside":
                ResumeTimers();
                Update(static (state, _) => state with { Hovering = false, Focused = false }, true);
                break;
        }
    }

    // TST-STA-16, TST-STA-17, TST-EVT-07, TST-EVT-08, TST-API-12 (promiseToast, store.ts:322-362). The settlement is brought
    // back to the renderer's context by `dispatch`; a cancellation takes the error branch, and the returned task faults
    // with the same exception, unwrapped (ADR 08 Amendments 2026-11-18). Base UI's `.then(success).catch(error)` runs the
    // error branch for an exception thrown by the success branch too, and so does this.
    public Task<TValue> PromiseToast<TValue>(Task<TValue> task, ToastPromiseOptions<TValue, TData> options)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(options);
        ToastObject<TData> loading = options.Loading.Resolve();
        string id = AddToast(loading with { Id = string.Empty, Type = "loading" });
        TaskCompletionSource<TValue> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PromiseSettlement<TValue> settlement = new(this, id, options, completion);
        task.ContinueWith(
            static (settled, state) => ((PromiseSettlement<TValue>)state!).Dispatch(settled),
            settlement,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return completion.Task;
    }

    public Task PromiseToast(Task task, ToastPromiseOptions<NoPayload, TData> options)
    {
        ArgumentNullException.ThrowIfNull(task);
        return PromiseToast(Completed(task), options);

        static async Task<NoPayload> Completed(Task task)
        {
            await task.ConfigureAwait(false);
            return s_noValue;
        }
    }

    // TST-STA-21 (pauseTimers, store.ts:364-380).
    public void PauseTimers()
    {
        if (areTimersPaused)
        {
            return;
        }

        areTimersPaused = true;
        foreach (ToastTimer timer in timers.Values)
        {
            // A record created while paused has no running timer and keeps its full delay.
            if (timer.Timer is not null)
            {
                timer.Stop();
                timer.Remaining = Math.Max(timer.Remaining - time.GetElapsedTime(timer.Start).TotalMilliseconds, 0);
            }
        }
    }

    // TST-STA-22 (resumeTimers, store.ts:382-396): a record whose remaining time ran out while paused restarts its full delay.
    public void ResumeTimers()
    {
        if (!areTimersPaused)
        {
            return;
        }

        areTimersPaused = false;
        foreach (ToastTimer timer in timers.Values)
        {
            timer.Remaining = timer.Remaining > 0 ? timer.Remaining : timer.Delay;
            timer.Run(timer.Remaining);
            timer.Start = time.GetTimestamp();
        }
    }

    // For the tests: whether a timer record exists for the toast.
    internal bool HasTimer(string id) => timers.ContainsKey(id);

    private void ResumeUnlessExpandedOrOutOfFocus()
    {
        if (!ToastSelectors<TData>.ExpandedOrOutOfFocus(State))
        {
            ResumeTimers();
        }
    }

    // TST-FOC-07, TST-FOC-08: the nearest non-ending toast after the closed one, else before it; null for the previous focus.
    private static string? HandOffTarget(ToastObject<TData>[] items, string id)
    {
        int index = Array.FindIndex(items, toast => toast.Id == id);
        for (int i = index + 1; i < items.Length; i++)
        {
            if (items[i].TransitionStatus != TransitionStatus.Ending)
            {
                return items[i].Id;
            }
        }

        for (int i = index - 1; i >= 0; i--)
        {
            if (items[i].TransitionStatus != TransitionStatus.Ending)
            {
                return items[i].Id;
            }
        }

        return null;
    }

    private static Task RunInline(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static ToastList<TData> Seed(IEnumerable<ToastObject<TData>>? toasts)
    {
        if (toasts is null)
        {
            return ToastList<TData>.Empty;
        }

        List<ToastObject<TData>> seeded = [];
        foreach (ToastObject<TData> toast in toasts)
        {
            seeded.Add(toast.Stored(toast.Id, toast.UpdateKey, toast.TransitionStatus));
        }

        return new ToastList<TData>([.. seeded]);
    }

    // TST-STA-18: `toast.timeout ?? providerTimeout`.
    private static double Duration(ToastObject<TData> toast, double providerTimeout) =>
        toast.Timeout is { } timeout ? timeout.ToMilliseconds() : providerTimeout;

    // TST-STA-03 (applyLimited, store.ts:74-84): an unchanged flag keeps the toast's reference.
    private static ToastObject<TData>[] ApplyLimited(ToastObject<TData>[] toasts, int limit)
    {
        ToastObject<TData>[] next = new ToastObject<TData>[toasts.Length];
        int activeIndex = 0;
        for (int i = 0; i < toasts.Length; i++)
        {
            ToastObject<TData> toast = toasts[i];
            if (toast.TransitionStatus == TransitionStatus.Ending)
            {
                next[i] = toast;
                continue;
            }

            bool limited = activeIndex >= limit;
            activeIndex++;
            next[i] = toast.Limited == limited ? toast : toast with { Limited = limited };
        }

        return next;
    }

    // TST-STA-05, TST-STA-09, TST-STA-19, TST-STA-20 (updateToastInternal, store.ts:227-286).
    private void UpdateToastInternal(string id, ToastObject<TData> updates, bool resetTimer, bool markUpdated)
    {
        ToastObject<TData>? previous = State.Toasts.Find(id);

        // An update of a closing toast is ignored, so a late promise settlement cannot block its dismissal.
        if (previous is null || previous.TransitionStatus == TransitionStatus.Ending)
        {
            return;
        }

        ToastObject<TData> next = previous.Apply(updates, markUpdated ? previous.UpdateKey + 1 : previous.UpdateKey);
        ReplaceToast(id, next);
        RescheduleAfterUpdate(id, previous, next, updates.Assigns(ToastFields.Timeout), resetTimer);
    }

    private void RescheduleAfterUpdate(string id, ToastObject<TData> previous, ToastObject<TData> next, bool timeoutUpdated, bool resetTimer)
    {
        double providerTimeout = State.Timeout;
        double nextTimeout = Duration(next, providerTimeout);
        double previousTimeout = Duration(previous, providerTimeout);
        bool shouldHaveTimer = next.TransitionStatus != TransitionStatus.Ending && next.Type != "loading" && nextTimeout > 0;
        bool hasTimer = timers.ContainsKey(id);
        bool timeoutChanged = previousTimeout != nextTimeout;
        bool wasLoading = previous.Type == "loading";

        if (!shouldHaveTimer && hasTimer)
        {
            ClearTimer(id);
            return;
        }

        if (shouldHaveTimer && (!hasTimer || timeoutChanged || timeoutUpdated || wasLoading || resetTimer))
        {
            ClearTimer(id);
            ScheduleTimer(id, nextTimeout);
            if (ToastSelectors<TData>.ExpandedOrOutOfFocus(State))
            {
                PauseTimers();
            }
        }
    }

    private void ReplaceToast(string id, ToastObject<TData> toast)
    {
        ToastObject<TData>[] items = State.Toasts.Items;
        ToastObject<TData>[] next = new ToastObject<TData>[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            next[i] = items[i].Id == id ? toast : items[i];
        }

        SetToasts(next, clearInteraction: false);
    }

    // TST-STA-15 (setToasts, store.ts:466-478).
    private void SetToasts(ToastObject<TData>[] toasts) => SetToasts(toasts, clearInteraction: toasts.Length == 0);

    private void SetToasts(ToastObject<TData>[] toasts, bool clearInteraction)
    {
        ToastStoreState<TData> state = State;
        ToastList<TData> list = new(toasts);
        SetState(clearInteraction ? state with { Toasts = list, Hovering = false, Focused = false } : state with { Toasts = list });
    }

    // TST-STA-18, TST-STA-23 (scheduleTimer, store.ts:418-435).
    private void ScheduleTimer(string id, double delay)
    {
        ToastTimer timer = new(this, id, delay) { Start = time.GetTimestamp() };
        if (!ToastSelectors<TData>.ExpandedOrOutOfFocus(State))
        {
            timer.Run(delay);
        }

        timers[id] = timer;
    }

    // TST-STA-24, TST-STA-25 (clearTimers, store.ts:437-443).
    private void ClearTimers()
    {
        foreach (ToastTimer timer in timers.Values)
        {
            timer.Stop();
        }

        timers.Clear();
        areTimersPaused = false;
    }

    // TST-STA-24 (clearTimer, store.ts:445-451).
    private void ClearTimer(string id)
    {
        if (timers.Remove(id, out ToastTimer? timer))
        {
            timer.Stop();
        }

        ResetPausedStateIfNoTimersRemain();
    }

    // TST-STA-28 (handleTimerFired, store.ts:453-456): a fired timer's record goes, then the toast closes as by close(id).
    private void OnTimerElapsed(ToastTimer timer, int generation)
    {
        if (disposed || timer.Generation != generation || !timers.TryGetValue(timer.Id, out ToastTimer? current) || !ReferenceEquals(current, timer))
        {
            return;
        }

        timer.Stop();
        timers.Remove(timer.Id);
        ResetPausedStateIfNoTimersRemain();
        CloseToast(timer.Id);
    }

    // TST-STA-24 (resetPausedStateIfNoTimersRemain, store.ts:458-464).
    private void ResetPausedStateIfNoTimersRemain()
    {
        if (timers.Count == 0)
        {
            areTimersPaused = false;
        }
    }

    private void Settle<TValue>(Task<TValue> settled, string id, ToastPromiseOptions<TValue, TData> options, TaskCompletionSource<TValue> completion)
    {
        if (settled.IsCompletedSuccessfully)
        {
            try
            {
                ApplyPromiseState(id, options.Success.Resolve(settled.Result), "success");
                completion.SetResult(settled.Result);
                return;
            }
            catch (Exception exception)
            {
                Fail(exception);
                return;
            }
        }

        Fail(settled.IsCanceled ? new TaskCanceledException(settled) : settled.Exception!.InnerExceptions[0]);

        void Fail(Exception exception)
        {
            try
            {
                ApplyPromiseState(id, options.Error.Resolve(exception), "error");
            }
            finally
            {
                completion.SetException(exception);
            }
        }
    }

    // TST-STA-16: `{ ...options, type, timeout: options.timeout }`, the timeout present even when not given, so a loading
    // toast's timeout is never inherited.
    private void ApplyPromiseState(string id, ToastObject<TData> options, string type)
    {
        ToastObject<TData> updates = options with { Type = type, Timeout = options.Timeout };
        UpdateToast(id, _ => updates);
    }

    private sealed record ToastElementRegistration(object Owner, Func<Microsoft.AspNetCore.Components.ElementReference?> Element);

    private sealed class PromiseSettlement<TValue>(ToastStore<TData> store, string id, ToastPromiseOptions<TValue, TData> options, TaskCompletionSource<TValue> completion)
    {
        public void Dispatch(Task<TValue> settled)
        {
            _ = store.dispatch(() => store.Settle(settled, id, options, completion));
        }
    }

    // One record per timed toast (store.ts:518-527). Each start draws a generation, so a callback queued by a timer that
    // was stopped or restarted meanwhile is ignored when it reaches the renderer's context.
    private sealed class ToastTimer(ToastStore<TData> store, string id, double delay)
    {
        private ITimer? timer;

        public string Id { get; } = id;

        public double Delay { get; } = delay;

        public double Remaining { get; set; } = delay;

        public long Start { get; set; }

        public int Generation { get; private set; }

        public ITimer? Timer => timer;

        public void Run(double milliseconds)
        {
            Stop();
            int generation = ++Generation;
            timer = store.time.CreateTimer(
                static state => ((TimerRun)state!).Elapsed(),
                new TimerRun(this, generation),
                TimeSpan.FromMilliseconds(milliseconds),
                System.Threading.Timeout.InfiniteTimeSpan);
        }

        public void Stop()
        {
            if (timer is not null)
            {
                timer.Dispose();
                timer = null;
                Generation++;
            }
        }

        public void Elapsed(int generation) => _ = store.dispatch(() => store.OnTimerElapsed(this, generation));
    }

    private sealed class TimerRun(ToastTimer timer, int generation)
    {
        public void Elapsed() => timer.Elapsed(generation);
    }
}
