using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The shared suites' titles the store observes, at L2 over the test-only hosts (detachedTriggersConformanceTests.tsx and
/// popupConformanceTests.tsx, titles verbatim with the host's values: role <c>dialog</c>, <c>aria-haspopup="dialog"</c>,
/// trigger press by click; PSH-STR-03..05, PSH-API-08, PSH-STA-03/28/32/40, PSH-ARIA-02..04/08). The tree is the suites'
/// fixture: triggers <c>trigger-1</c>.. with their number as payload, contained in the root or detached with a handle, the
/// root's payload template rendering <c>root-payload</c> and the popup with its <c>content</c>. The browser runs the suites
/// themselves at L4 on the fixture popup host.
/// </summary>
public sealed class PopupSuiteStoreTests : IDisposable
{
    private const string DetachedWarning = "no root using this handle is mounted";

    private readonly PopupTestContext test = new();
    private readonly CapturingLogger<TestPopupRoot<string>> logger = new();

    public PopupSuiteStoreTests()
    {
        DevelopmentWarningsSwitch.TurnOn();
        test.Context.Services.AddSingleton<ILogger<TestPopupRoot<string>>>(logger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Theory(DisplayName = "marks the trigger matching a controlled `triggerId` as active")]
    [InlineData("contained")]
    [InlineData("detached")]
    [Trait("Requirement", "PSH-API-08")]
    [Trait("Requirement", "PSH-STA-03")]
    public void Marks_the_trigger_matching_a_controlled_triggerId_as_active(string mode)
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["mode"] = mode, ["open"] = true, ["triggerId"] = "trigger-2" });

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"content\"]").TextContent));
        ExpectActiveTrigger(cut, 2);
    }

    [Theory(DisplayName = "opens initially with the payload of a non-first `defaultTriggerId`")]
    [InlineData("contained")]
    [InlineData("detached")]
    [Trait("Requirement", "PSH-STA-28")]
    public void Opens_initially_with_the_payload_of_a_non_first_defaultTriggerId(string mode)
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["mode"] = mode, ["triggerCount"] = 3, ["defaultOpen"] = true, ["defaultTriggerId"] = "trigger-2" });

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"content\"]").TextContent));
        ExpectActiveTrigger(cut, 2, 3);
    }

    [Fact(DisplayName = "ignores imperative calls while no root is attached and follows the root across remounts")]
    [Trait("Requirement", "PSH-STR-03")]
    [Trait("Requirement", "PSH-STA-32")]
    [Trait("Requirement", "PSH-STA-40")]
    [Trait("Requirement", "PSH-EDGE-02")]
    public async Task Ignores_imperative_calls_while_no_root_is_attached_and_follows_the_root_across_remounts()
    {
        TestPopupHandle<string> handle = new();

        // Before any root attached the handle has no logger, so its two warnings are dropped (design point 6, settled by
        // the orchestrator 2026-10-27); Base UI counts two here.
        await handle.OpenAsync("trigger-1");
        await handle.CloseAsync();
        Assert.False(handle.IsOpen);
        Assert.Equal(0, CountWarnings(DetachedWarning));

        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["mode"] = "detached", ["handle"] = handle, ["rootMounted"] = true });
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal("No payload", cut.Find("[data-testid=\"root-payload\"]").TextContent);

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid=\"content\"]").TextContent));
        Assert.True(handle.IsOpen);

        // Unmounting the root while open detaches it from the handle.
        await cut.Instance.SetAsync("rootMounted", false);
        Assert.False(handle.IsOpen);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        int before = CountWarnings(DetachedWarning);
        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        await cut.InvokeAsync(handle.CloseAsync);
        Assert.False(handle.IsOpen);
        Assert.Equal(2, CountWarnings(DetachedWarning) - before);

        // A remounted root starts closed, without the previous payload, and is controllable.
        await cut.Instance.SetAsync("rootMounted", true);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal("No payload", cut.Find("[data-testid=\"root-payload\"]").TextContent);

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-2"));
        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"content\"]").TextContent));
        Assert.True(handle.IsOpen);
    }

    [Fact(DisplayName = "registers a detached trigger declared after the root")]
    [Trait("Requirement", "PSH-STR-05")]
    public async Task Registers_a_detached_trigger_declared_after_the_root()
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["mode"] = "detached", ["rootFirst"] = true });

        await cut.Find("[data-testid=\"trigger-2\"]").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"content\"]").TextContent));
        ExpectActiveTrigger(cut, 2);
    }

    [Fact(DisplayName = "resolves a trigger still registered to the previous root during a transient overlap")]
    [Trait("Requirement", "PSH-STR-04")]
    public async Task Resolves_a_trigger_still_registered_to_the_previous_root_during_a_transient_overlap()
    {
        TestPopupHandle<string> handle = new();
        List<Exception> openErrors = [];
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) =>
            {
                string phase = host.Get<string>("phase")!;
                for (int number = 1; number <= 2; number++)
                {
                    builder.OpenComponent<TestPopupTrigger<string>>(0);
                    builder.SetKey(number);
                    builder.AddComponentParameter(1, nameof(TestPopupTrigger<string>.Handle), handle);
                    builder.AddComponentParameter(2, "id", $"trigger-{number}");
                    builder.AddComponentParameter(3, "data-testid", $"trigger-{number}");
                    builder.CloseComponent();
                }

                if (phase is "outgoing" or "overlap")
                {
                    RenderOverlapRoot(builder, handle, "outgoing");
                }

                if (phase is "overlap" or "incoming")
                {
                    RenderOverlapRoot(builder, handle, "incoming");

                    // OpenOnMount: the incoming root's batch opens by trigger id before the trigger migrated to its store.
                    builder.OpenComponent<AfterFirstRender>(10);
                    builder.AddComponentParameter(11, nameof(AfterFirstRender.Run), (Func<Task>)(async () =>
                    {
                        try
                        {
                            await handle.OpenAsync("trigger-2");
                        }
                        catch (InvalidOperationException error)
                        {
                            openErrors.Add(error);
                        }
                    }));
                    builder.CloseComponent();
                }
            },
            new() { ["phase"] = "outgoing" });

        await cut.Instance.SetAsync("phase", "overlap");

        Assert.Empty(openErrors);
        Assert.Equal(0, CountWarnings("No trigger found"));
        Assert.True(handle.IsOpen);
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=\"trigger-2\"]").HasAttribute("data-popup-open")));
        Assert.False(cut.Find("[data-testid=\"trigger-1\"]").HasAttribute("data-popup-open"));

        // Completing the handoff (the outgoing root unmounts) keeps the popup open and associated.
        await cut.Instance.SetAsync("phase", "incoming");
        Assert.True(handle.IsOpen);
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=\"trigger-2\"]").HasAttribute("data-popup-open")));
        Assert.False(cut.Find("[data-testid=\"trigger-1\"]").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "has the dialog role on the popup")]
    [Trait("Requirement", "PSH-ARIA-08")]
    public void Has_the_dialog_role_on_the_popup()
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["triggerCount"] = 1, ["open"] = true });

        Assert.Equal("dialog", cut.Find("[data-testid=\"popup\"]").GetAttribute("role"));
    }

    [Fact(DisplayName = "has the `aria-controls` attribute on the trigger")]
    [Trait("Requirement", "PSH-ARIA-02")]
    public void Has_the_aria_controls_attribute_on_the_trigger()
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["triggerCount"] = 1, ["open"] = true });

        string? popupId = cut.Find("[data-testid=\"popup\"]").Id;
        Assert.False(string.IsNullOrEmpty(popupId));
        cut.WaitForAssertion(() => Assert.Equal(popupId, cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-controls")));
    }

    [Fact(DisplayName = "has the `aria-haspopup` attribute on the trigger")]
    [Trait("Requirement", "PSH-ARIA-04")]
    public async Task Has_the_aria_haspopup_attribute_on_the_trigger()
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["triggerCount"] = 1, ["open"] = false });
        Assert.Equal("dialog", cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-haspopup"));

        await cut.Instance.SetAsync("open", true);
        Assert.Equal("dialog", cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-haspopup"));
    }

    [Fact(DisplayName = "allows a custom `id` prop")]
    [Trait("Requirement", "PSH-ARIA-02")]
    [Trait("Requirement", "PSH-ARIA-03")]
    public void Allows_a_custom_id_prop()
    {
        IRenderedComponent<PopupTestHost> cut = RenderFixture(new() { ["triggerCount"] = 1, ["open"] = true, ["popupId"] = "TestId" });

        Assert.Equal("TestId", cut.Find("[data-testid=\"popup\"]").Id);
        cut.WaitForAssertion(() => Assert.Equal("TestId", cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-controls")));
    }

    private static void RenderOverlapRoot(RenderTreeBuilder builder, TestPopupHandle<string> handle, string key)
    {
        builder.OpenComponent<TestPopupRoot<string>>(20);
        builder.SetKey(key);
        builder.AddComponentParameter(21, nameof(TestPopupRoot<string>.Handle), handle);
        builder.AddComponentParameter(22, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<TestPopupPopup<string>>(0);
            content.AddComponentParameter(1, "data-testid", $"popup-{key}");
            content.AddComponentParameter(2, nameof(TestPopupPopup<string>.ChildContent), (RenderFragment)(text => text.AddContent(0, key)));
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }

    // expectActiveTrigger: only the active trigger carries the open marker, aria-expanded="true" and aria-controls.
    private static void ExpectActiveTrigger(IRenderedComponent<PopupTestHost> cut, int? activeNumber, int triggerCount = 2)
    {
        string? popupId = activeNumber is null ? null : cut.Find("[data-testid=\"popup\"]").Id;
        for (int number = 1; number <= triggerCount; number++)
        {
            IElement trigger = cut.Find($"[data-testid=\"trigger-{number}\"]");
            bool active = number == activeNumber;
            Assert.Equal(active, trigger.HasAttribute("data-popup-open"));
            Assert.Equal(active ? "true" : "false", trigger.GetAttribute("aria-expanded"));
            if (active)
            {
                Assert.Equal(popupId, trigger.GetAttribute("aria-controls"));
            }
            else if (popupId is not null)
            {
                Assert.False(trigger.HasAttribute("aria-controls"));
            }
        }
    }

    private int CountWarnings(string text) => logger.Entries.Count(entry => entry.Message.Contains(text, StringComparison.Ordinal));

    // The suites' Fixture: "mode" contained or detached, "triggerCount" (2), "handle", "rootMounted" (true), "rootFirst",
    // the root's "open", "triggerId", "defaultOpen" and "defaultTriggerId" (each passed only when given), the popup's id.
    private IRenderedComponent<PopupTestHost> RenderFixture(Dictionary<string, object?> values)
    {
        values.TryAdd("mode", "contained");
        values.TryAdd("triggerCount", 2);
        values.TryAdd("rootMounted", true);
        if (Equals(values["mode"], "detached"))
        {
            values.TryAdd("handle", new TestPopupHandle<string>());
        }

        return test.Render(
            (builder, host) =>
            {
                bool detached = Equals(host.Get<string>("mode"), "detached");
                TestPopupHandle<string>? handle = host.Get<TestPopupHandle<string>>("handle");
                if (detached && !host.Get<bool>("rootFirst"))
                {
                    RenderTriggers(builder, host, handle);
                }

                if (host.Get<bool>("rootMounted"))
                {
                    RenderRoot(builder, host, detached ? handle : null, detached);
                }

                if (detached && host.Get<bool>("rootFirst"))
                {
                    RenderTriggers(builder, host, handle);
                }
            },
            values);
    }

    private static void RenderTriggers(RenderTreeBuilder builder, PopupTestHost host, TestPopupHandle<string>? handle)
    {
        int count = host.Get<int>("triggerCount");
        for (int number = 1; number <= count; number++)
        {
            builder.OpenComponent<TestPopupTrigger<string>>(30);
            builder.SetKey(number);
            if (handle is not null)
            {
                builder.AddComponentParameter(31, nameof(TestPopupTrigger<string>.Handle), handle);
            }

            builder.AddComponentParameter(32, "id", $"trigger-{number}");
            builder.AddComponentParameter(33, "data-testid", $"trigger-{number}");
            builder.AddComponentParameter(34, nameof(TestPopupTrigger<string>.Payload), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string label = $"Trigger {number}";
            builder.AddComponentParameter(35, nameof(TestPopupTrigger<string>.ChildContent), (RenderFragment)(text => text.AddContent(0, label)));
            builder.CloseComponent();
        }
    }

    private static void RenderRoot(RenderTreeBuilder builder, PopupTestHost host, TestPopupHandle<string>? handle, bool detached)
    {
        builder.OpenComponent<TestPopupRoot<string>>(40);
        if (handle is not null)
        {
            builder.AddComponentParameter(41, nameof(TestPopupRoot<string>.Handle), handle);
        }

        if (host.Values.TryGetValue("open", out object? open))
        {
            builder.AddComponentParameter(42, nameof(TestPopupRoot<string>.Open), (bool)open!);
        }

        if (host.Values.TryGetValue("triggerId", out object? triggerId))
        {
            builder.AddComponentParameter(43, nameof(TestPopupRoot<string>.TriggerId), (string?)triggerId);
        }

        if (host.Values.TryGetValue("defaultOpen", out object? defaultOpen))
        {
            builder.AddComponentParameter(44, nameof(TestPopupRoot<string>.DefaultOpen), (bool)defaultOpen!);
        }

        if (host.Values.TryGetValue("defaultTriggerId", out object? defaultTriggerId))
        {
            builder.AddComponentParameter(45, nameof(TestPopupRoot<string>.DefaultTriggerId), (string?)defaultTriggerId);
        }

        if (!detached)
        {
            builder.AddComponentParameter(46, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content => RenderTriggers(content, host, handle: null)));
        }

        string? popupId = host.Get<string>("popupId");
        builder.AddComponentParameter(47, nameof(TestPopupRoot<string>.PayloadContent), (RenderFragment<string?>)(payload => content =>
        {
            content.OpenElement(0, "span");
            content.AddAttribute(1, "data-testid", "root-payload");
            content.AddContent(2, payload ?? "No payload");
            content.CloseElement();
            content.OpenComponent<TestPopupPopup<string>>(3);
            content.AddComponentParameter(4, "data-testid", "popup");
            if (popupId is not null)
            {
                content.AddComponentParameter(5, "id", popupId);
            }

            content.AddComponentParameter(6, nameof(TestPopupPopup<string>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenElement(0, "span");
                inner.AddAttribute(1, "data-testid", "content");
                inner.AddContent(2, payload);
                inner.CloseElement();
            }));
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
