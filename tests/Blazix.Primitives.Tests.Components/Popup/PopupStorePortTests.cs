using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Port titles at L2 over the test-only hosts for the rows whose cited titles press Escape (PSH-STA-01, PSH-EVT-03, which
/// are DIS-01's) or sit in a browser-only suite block (PSH-STR-01/02/06, PSH-API-07, PSH-STA-31's switch clause,
/// PSH-STA-45, PSH-ARIA-01, PSH-DIS-06; their verbatim titles run at L4 on the fixture popup host), each keeping the store's
/// clause; and PSH-EDGE-06's three popupHandle.test.ts titles restated at the render-batch boundary (ADR 01 Amendments
/// 2026-10-27). The test trigger toggles from a C# onclick (design point 4).
/// </summary>
public sealed class PopupStorePortTests : IDisposable
{
    private const string OverlapWarning = "A handle is attached to more than one mounted root at the same time.";

    private readonly PopupTestContext test = new();
    private readonly CapturingLogger<TestPopupRoot<string>> logger = new();
    private TestPopupRoot<string>? root;

    public PopupStorePortTests()
    {
        DevelopmentWarningsSwitch.TurnOn();
        test.Context.Services.AddSingleton<ILogger<TestPopupRoot<string>>>(logger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a root opens from defaultOpen with its default trigger, mounted, and stays uncontrolled")]
    [Trait("Requirement", "PSH-STA-01")]
    public async Task A_root_opens_from_defaultOpen_with_its_default_trigger_mounted_and_stays_uncontrolled()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["defaultOpen"] = true, ["defaultTriggerId"] = "trigger-1" });

        PopupStoreState<string> popup = root!.Store.Popup;
        Assert.True(popup.Open);
        Assert.Null(popup.OpenProp);
        Assert.True(popup.Mounted);
        Assert.Equal("trigger-1", popup.ActiveTriggerId);
        Assert.Null(popup.TriggerIdProp);

        await cut.Find("#trigger-1").ClickAsync(new());
        Assert.False(root.Store.IsOpen);
        await cut.Find("#trigger-1").ClickAsync(new());
        Assert.True(root.Store.IsOpen);
    }

    [Fact(DisplayName = "port: onOpenChange is not called on mount or for a controlled prop change, and is called with the new open state and the change details")]
    [Trait("Requirement", "PSH-EVT-03")]
    public async Task OnOpenChange_is_not_called_on_mount_or_for_a_controlled_prop_change()
    {
        List<ValueChangeEventArgs<bool>> calls = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["triggerCount"] = 1, ["open"] = false, ["onOpenChange"] = (Action<ValueChangeEventArgs<bool>>)calls.Add });
        Assert.Empty(calls);

        // The lone trigger is claimed at the boundary, so its press closes.
        await cut.Instance.SetAsync("open", true);
        Assert.True(root!.Store.IsOpen);
        Assert.Equal("trigger-1", root.Store.Popup.ActiveTriggerId);
        Assert.Empty(calls);

        await cut.Find("#trigger-1").ClickAsync(new() { Type = "click" });

        ValueChangeEventArgs<bool> call = Assert.Single(calls);
        Assert.False(call.Value);
        Assert.Equal(EventReason.TriggerPress, call.Details.Reason);
        Assert.Equal("click", call.Details.Event.Type);
        Assert.Equal("trigger-1", call.Details.Trigger?.Id);
    }

    [Fact(DisplayName = "port: a root keeps one store across its parameter changes, a new handle included")]
    [Trait("Requirement", "PSH-STR-01")]
    public async Task A_root_keeps_one_store_across_its_parameter_changes_a_new_handle_included()
    {
        TestPopupHandle<string> first = new();
        TestPopupHandle<string> second = new();
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["handle"] = first });
        TestPopupStore<string> store = root!.Store;
        Assert.Same(store, first.Store);

        await cut.Instance.SetAsync("handle", second);
        await cut.Instance.SetAsync("defaultOpen", true);

        Assert.Same(store, root.Store);
        Assert.Same(store, second.Store);
        Assert.NotSame(store, first.Store);
    }

    [Theory(DisplayName = "port: any of three triggers, contained or detached, opens on click and only that trigger is active, and none after the close")]
    [InlineData("contained")]
    [InlineData("detached")]
    [Trait("Requirement", "PSH-STR-02")]
    [Trait("Requirement", "PSH-STR-06")]
    [Trait("Requirement", "PSH-STA-45")]
    [Trait("Requirement", "PSH-ARIA-01")]
    public async Task Any_of_three_triggers_opens_on_click_and_only_that_trigger_is_active(string mode)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["triggerCount"] = 3, ["handle"] = mode == "detached" ? new TestPopupHandle<string>() : null });
        ExpectActiveTrigger(cut, null);

        for (int number = 1; number <= 3; number++)
        {
            await cut.Find($"#trigger-{number}").ClickAsync(new());
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]")));
            ExpectActiveTrigger(cut, number);

            await cut.Find($"#trigger-{number}").ClickAsync(new());

            // The popup's transitions handle reports the finished exit (no animation), which unmounts it.
            await cut.InvokeAsync(() => root!.Store.CompleteTransitionAsync(opened: false));
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=\"popup\"]")));
            ExpectActiveTrigger(cut, null);
            Assert.Null(root!.Store.Popup.ActiveTriggerId);
            Assert.False(root.Store.Popup.Mounted);
        }
    }

    [Fact(DisplayName = "port: a handle takes precedence over the enclosing root")]
    [Trait("Requirement", "PSH-STR-02")]
    public async Task A_handle_takes_precedence_over_the_enclosing_root()
    {
        TestPopupHandle<string> handle = new();
        TestPopupRoot<string>? other = null;
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<TestPopupTrigger<string>>(0);
                content.AddComponentParameter(1, "id", "trigger-1");
                content.AddComponentParameter(2, nameof(TestPopupTrigger<string>.Handle), handle);
                content.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(2, reference => root = (TestPopupRoot<string>)reference);
            builder.CloseComponent();
            builder.OpenComponent<TestPopupRoot<string>>(3);
            builder.AddComponentParameter(4, nameof(TestPopupRoot<string>.Handle), handle);
            builder.AddComponentReferenceCapture(5, reference => other = (TestPopupRoot<string>)reference);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.NotNull(other!.Store.Triggers.GetById("trigger-1")));
        Assert.Null(root!.Store.Triggers.GetById("trigger-1"));

        await cut.Find("#trigger-1").ClickAsync(new());
        Assert.True(other!.Store.IsOpen);
        Assert.False(root.Store.IsOpen);
    }

    [Fact(DisplayName = "port: switching the active trigger by click moves the payload and the root's payload template without closing, and keeps the popup")]
    [Trait("Requirement", "PSH-API-07")]
    [Trait("Requirement", "PSH-STA-31")]
    public async Task Switching_the_active_trigger_moves_the_payload_without_closing_and_keeps_the_popup()
    {
        List<bool> calls = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["onOpenChange"] = (Action<ValueChangeEventArgs<bool>>)(args => calls.Add(args.Value)) });

        await cut.Find("#trigger-1").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid=\"content\"]").TextContent));
        Assert.Equal("1", cut.Find("[data-testid=\"root-payload\"]").TextContent);
        TestPopupPopup<string> popup = cut.FindComponent<TestPopupPopup<string>>().Instance;
        ElementReference? element = popup.Element;

        await cut.Find("#trigger-2").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"content\"]").TextContent));
        Assert.Equal("2", cut.Find("[data-testid=\"root-payload\"]").TextContent);
        ExpectActiveTrigger(cut, 2);
        Assert.Equal([true, true], calls);
        Assert.Same(popup, cut.FindComponent<TestPopupPopup<string>>().Instance);
        Assert.Equal(element?.Id, cut.FindComponent<TestPopupPopup<string>>().Instance.Element?.Id);
    }

    [Fact(DisplayName = "port: a canceled open request leaves the popup closed and the trigger without data-popup-open, with no further onOpenChange call")]
    [Trait("Requirement", "PSH-DIS-06")]
    public async Task A_canceled_open_request_leaves_the_popup_closed()
    {
        int calls = 0;
        IRenderedComponent<PopupTestHost> cut = RenderTree(new()
        {
            ["onOpenChange"] = (Action<ValueChangeEventArgs<bool>>)(args =>
            {
                calls++;
                args.Details.Cancel();
            }),
        });

        await cut.Find("#trigger-1").ClickAsync(new());

        Assert.Equal(1, calls);
        Assert.False(root!.Store.IsOpen);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.False(cut.Find("#trigger-1").HasAttribute("data-popup-open"));
        Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "port: warns when more than one root is still attached at the next render-batch boundary")]
    [Trait("Requirement", "PSH-EDGE-06")]
    public void Warns_when_more_than_one_root_is_still_attached_at_the_next_render_batch_boundary()
    {
        TestPopupHandle<string> handle = new();
        IRenderedComponent<PopupTestHost> cut = RenderRoots(handle, removeAfterFirstRender: null);

        cut.WaitForAssertion(() => Assert.Equal(1, CountWarnings(OverlapWarning)));
    }

    [Theory(DisplayName = "port: does not warn when $name before the next render-batch boundary")]
    [InlineData("the older root detaches", "older")]
    [InlineData("the newer root detaches", "newer")]
    [Trait("Requirement", "PSH-EDGE-06")]
    public void Does_not_warn_when_a_root_detaches_before_the_next_render_batch_boundary(string name, string detaching)
    {
        Assert.NotEmpty(name);
        TestPopupHandle<string> handle = new();
        Dictionary<string, TestPopupRoot<string>> roots = [];
        IRenderedComponent<PopupTestHost> cut = RenderRoots(handle, removeAfterFirstRender: detaching, roots);

        cut.WaitForAssertion(() => Assert.False(cut.Instance.Get<bool>(detaching)));
        cut.WaitForAssertion(() => Assert.Same(roots[detaching == "older" ? "newer" : "older"].Store, handle.Store));
        Assert.Equal(0, CountWarnings(OverlapWarning));
    }

    [Fact(DisplayName = "port: does not warn for a single attached root")]
    [Trait("Requirement", "PSH-EDGE-06")]
    public async Task Does_not_warn_for_a_single_attached_root()
    {
        TestPopupHandle<string> handle = new();
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["handle"] = handle });

        // Further batches bring further boundaries; none warns.
        await cut.Instance.SetAsync("tick", 1);
        await cut.Instance.SetAsync("tick", 2);

        Assert.Same(root!.Store, handle.Store);
        Assert.Equal(0, CountWarnings(OverlapWarning));
    }

    private static void ExpectActiveTrigger(IRenderedComponent<PopupTestHost> cut, int? activeNumber, int triggerCount = 3)
    {
        string? popupId = activeNumber is null ? null : cut.Find("[data-testid=\"popup\"]").Id;
        foreach (AngleSharp.Dom.IElement trigger in cut.FindAll("button[id^=\"trigger-\"]").Take(triggerCount))
        {
            bool active = trigger.Id == $"trigger-{activeNumber}";
            Assert.Equal(active, trigger.HasAttribute("data-popup-open"));
            Assert.Equal(active ? "true" : "false", trigger.GetAttribute("aria-expanded"));
            if (active)
            {
                Assert.Equal(popupId, trigger.GetAttribute("aria-controls"));
            }
        }
    }

    private int CountWarnings(string text) => logger.Entries.Count(entry => entry.Message.Contains(text, StringComparison.Ordinal));

    // Two roots on one handle, the older first; "removeAfterFirstRender" names the one the first batch's after-render removes.
    private IRenderedComponent<PopupTestHost> RenderRoots(TestPopupHandle<string> handle, string? removeAfterFirstRender, Dictionary<string, TestPopupRoot<string>>? roots = null) =>
        test.Render(
            (builder, host) =>
            {
                foreach (string name in new[] { "older", "newer" })
                {
                    if (!host.Get<bool>(name))
                    {
                        continue;
                    }

                    builder.OpenComponent<TestPopupRoot<string>>(0);
                    builder.SetKey(name);
                    builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.Handle), handle);
                    builder.AddComponentReferenceCapture(2, reference =>
                    {
                        if (roots is not null)
                        {
                            roots[name] = (TestPopupRoot<string>)reference;
                        }
                    });
                    builder.CloseComponent();
                }

                if (removeAfterFirstRender is not null)
                {
                    builder.OpenComponent<AfterFirstRender>(3);
                    builder.AddComponentParameter(4, nameof(AfterFirstRender.Run), (Func<Task>)(() => host.SetAsync(removeAfterFirstRender, false)));
                    builder.CloseComponent();
                }
            },
            new() { ["older"] = true, ["newer"] = true });

    // The suites' fixture reduced: "triggerCount" triggers trigger-1.. with their number as payload, contained, or detached
    // when a "handle" is given; the root's controlled "open" and its defaults when given; the payload template renders
    // root-payload and the popup with its content.
    private IRenderedComponent<PopupTestHost> RenderTree(Dictionary<string, object?> values)
    {
        values.TryAdd("triggerCount", 2);
        return test.Render(
            (builder, host) =>
            {
                TestPopupHandle<string>? handle = host.Get<TestPopupHandle<string>>("handle");
                if (handle is not null)
                {
                    RenderTriggers(builder, host, handle);
                }

                builder.OpenComponent<TestPopupRoot<string>>(10);
                if (handle is not null)
                {
                    builder.AddComponentParameter(11, nameof(TestPopupRoot<string>.Handle), handle);
                }

                if (host.Values.TryGetValue("open", out object? open))
                {
                    builder.AddComponentParameter(12, nameof(TestPopupRoot<string>.Open), (bool)open!);
                }

                builder.AddComponentParameter(13, nameof(TestPopupRoot<string>.DefaultOpen), host.Get<bool>("defaultOpen"));
                builder.AddComponentParameter(14, nameof(TestPopupRoot<string>.DefaultTriggerId), host.Get<string>("defaultTriggerId"));
                if (host.Get<Action<ValueChangeEventArgs<bool>>>("onOpenChange") is { } onOpenChange)
                {
                    builder.AddComponentParameter(15, nameof(TestPopupRoot<string>.OnOpenChange), EventCallback.Factory.Create(this, onOpenChange));
                }

                if (handle is null)
                {
                    builder.AddComponentParameter(16, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content => RenderTriggers(content, host, handle: null)));
                }

                builder.AddComponentParameter(17, nameof(TestPopupRoot<string>.PayloadContent), (RenderFragment<string?>)(payload => content =>
                {
                    content.OpenElement(0, "span");
                    content.AddAttribute(1, "data-testid", "root-payload");
                    content.AddContent(2, payload ?? "No payload");
                    content.CloseElement();
                    content.OpenComponent<TestPopupPopup<string>>(3);
                    content.AddComponentParameter(4, "data-testid", "popup");
                    content.AddComponentParameter(5, nameof(TestPopupPopup<string>.ChildContent), (RenderFragment)(inner =>
                    {
                        inner.OpenElement(0, "span");
                        inner.AddAttribute(1, "data-testid", "content");
                        inner.AddContent(2, payload);
                        inner.CloseElement();
                    }));
                    content.CloseComponent();
                }));
                builder.AddComponentReferenceCapture(18, reference => root = (TestPopupRoot<string>)reference);
                builder.CloseComponent();
            },
            values);
    }

    private static void RenderTriggers(RenderTreeBuilder builder, PopupTestHost host, TestPopupHandle<string>? handle)
    {
        int count = host.Get<int>("triggerCount");
        for (int number = 1; number <= count; number++)
        {
            builder.OpenComponent<TestPopupTrigger<string>>(0);
            builder.SetKey(number);
            if (handle is not null)
            {
                builder.AddComponentParameter(1, nameof(TestPopupTrigger<string>.Handle), handle);
            }

            builder.AddComponentParameter(2, "id", $"trigger-{number}");
            builder.AddComponentParameter(3, nameof(TestPopupTrigger<string>.Payload), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.CloseComponent();
        }
    }
}
