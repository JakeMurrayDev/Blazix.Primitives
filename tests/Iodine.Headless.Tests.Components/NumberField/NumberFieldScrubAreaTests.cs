using System.Globalization;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// 6b's scrub role at L2 (issue 277): the ScrubArea and ScrubAreaCursor parts over the `number-field` module's scrub
// reports (ADR 06 §1). NumberFieldScrubAreaCursor.test.tsx's titles drive the start, lock and end reports where Base UI
// drives a pointer; the pointer itself, the lock and the moves are L3 (number-field.scrub.test.ts) and L4
// (number-field.scrub.spec.ts).
/// <summary>The scrub area and its cursor over the module's scrub reports.</summary>
public sealed class NumberFieldScrubAreaTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private readonly List<ValueChangeEventArgs<double?>> changes = [];
    private readonly List<ValueCommittedEventArgs<double?>> commits = [];

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "has presentation role")]
    [Trait("Requirement", "NUM-DOM-12")]
    public void Has_presentation_role()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.CloseComponent();
        });

        Assert.Single(cut.FindAll("[role=presentation]"));
    }

    [Fact(DisplayName = "port: the scrub area's inline style keeps touch and selection off under the consumer's style")]
    [Trait("Requirement", "NUM-DOM-12")]
    public void The_scrub_areas_inline_style_is_under_the_consumers()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.AddAttribute(1, "style", "cursor: ew-resize; touch-action: pan-y");
            b.CloseComponent();
        });

        Assert.Equal(
            "touch-action: pan-y; -webkit-user-select: none; user-select: none; cursor: ew-resize;",
            cut.Find("span[role=presentation]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the scrub area sends its direction, sensitivity and teleport distance, omitting the defaults")]
    [Trait("Requirement", "NUM-API-24")]
    public void The_scrub_area_sends_its_options()
    {
        Render(b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            b.OpenComponent<NumberFieldScrubArea>(1);
            b.CloseComponent();
            b.OpenComponent<NumberFieldScrubArea>(2);
            b.AddComponentParameter(3, nameof(NumberFieldScrubArea.Direction), Orientation.Vertical);
            b.AddComponentParameter(4, nameof(NumberFieldScrubArea.PixelSensitivity), 5d);
            b.AddComponentParameter(5, nameof(NumberFieldScrubArea.TeleportDistance), 100d);
            b.CloseComponent();
        });

        Assert.Equal(
            ["""{"part":"scrub"}""", """{"part":"scrub","vertical":true,"pixelSensitivity":5,"teleportDistance":100}"""],
            test.ScrubOptions);
    }

    // Issue 370: the module focuses the input on a press and clicks through a locked release only with the input's element;
    // a scrub area rendered before its input attaches without it, so the input's first render must send it.
    [Fact(DisplayName = "port: a scrub area rendered before its input sends the input to the module once the input renders")]
    [Trait("Requirement", "NUM-FOC-04")]
    [Trait("Requirement", "NUM-PTR-14")]
    public void A_scrub_area_before_its_input_sends_the_input_once_it_renders()
    {
        Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.CloseComponent();
            b.OpenComponent<NumberFieldInput>(1);
            b.CloseComponent();
        });

        JSRuntimeInvocation inputAttach = Assert.Single(test.NumberFieldModule.Invocations["attach"], i => !IsScrub(i.Arguments[1]));
        JSRuntimeInvocation scrubAttach = Assert.Single(test.NumberFieldModule.Invocations["attach"], i => IsScrub(i.Arguments[1]));
        // The last references the scrub handle received: its last update's, or its attach's when it had no update.
        List<JSRuntimeInvocation> scrubUpdates = [.. test.NumberFieldHandle.Invocations["update"].Where(i => IsScrub(i.Arguments[0]))];
        IReadOnlyList<object?> last = scrubUpdates.Count > 0 ? scrubUpdates[^1].Arguments : scrubAttach.Arguments;
        int index = scrubUpdates.Count > 0 ? 1 : 3;
        object[] references = Assert.IsType<object[]>(last.Count > index ? last[index] : null);
        Assert.Equal(inputAttach.Arguments[0], Assert.Single(references));

        static bool IsScrub(object? options) => ((string)options!).Contains("\"part\":\"scrub\"", StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a scrub sets data-scrubbing on every part, steps by the delta with reason scrub, and the release commits once")]
    [Trait("Requirement", "NUM-STA-15")]
    [Trait("Requirement", "NUM-DOM-02")]
    [Trait("Requirement", "NUM-PTR-11")]
    [Trait("Requirement", "NUM-PTR-13")]
    [Trait("Requirement", "NUM-EVT-05")]
    public async Task A_scrub_sets_scrubbing_steps_and_commits_once()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        // The root, the input, the scrub area and the cursor.
        Assert.Equal(4, cut.FindAll("[data-scrubbing]").Count);

        await test.ScrubStepAsync(cut, -10);
        await test.ScrubStepAsync(cut, 5);
        Assert.Equal("-5", test.Text);
        Assert.Equal([EventReason.Scrub, EventReason.Scrub], changes.Select(c => c.Details.Reason));
        Assert.Equal([-1, 1], changes.Select(c => ((NumberFieldChangeEventDetails)c.Details).Direction));
        Assert.Empty(commits);

        await test.ScrubEndAsync(cut);

        Assert.Empty(cut.FindAll("[data-scrubbing]"));
        ValueCommittedEventArgs<double?> commit = Assert.Single(commits);
        Assert.Equal(-5, commit.Value);
        Assert.Equal(EventReason.Scrub, commit.Details.Reason);
    }

    [Fact(DisplayName = "port: a release with no step still commits the current value (NUM-PTR-13)")]
    [Trait("Requirement", "NUM-PTR-13")]
    [Trait("Requirement", "NUM-EVT-05")]
    public async Task A_release_with_no_step_commits()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(7);

        await test.ScrubStartAsync(cut);
        await test.ScrubEndAsync(cut);

        Assert.Empty(changes);
        Assert.Equal(7, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "port: a scrub step uses the move's modifiers and echoes the step's revision to the input")]
    [Trait("Requirement", "NUM-STA-16")]
    [Trait("Requirement", "NUM-PTR-11")]
    public async Task A_scrub_step_uses_modifiers_and_echoes_the_revision()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, 2, shiftKey: true);
        await test.ScrubStepAsync(cut, 1, altKey: true);

        Assert.Equal("20.1", test.Text);
        Assert.Equal(2, JsonDocument.Parse(test.NumberFieldOptions[^1]).RootElement.GetProperty("revision").GetInt32());
    }

    [Fact(DisplayName = "syncs the visible input value when scrubbing after pasting")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-EVT-16")]
    public async Task Syncs_the_visible_input_value_when_scrubbing_after_pasting()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(10);

        await test.PasteAsync(cut, "20");
        Assert.Equal("20", test.Text);
        Assert.Equal(20, changes[^1].Value);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, 2);

        Assert.Equal(22, changes[^1].Value);
        Assert.Equal("22", test.Text);
    }

    [Fact(DisplayName = "port: a read-only or disabled field ignores a scrub step")]
    [Trait("Requirement", "NUM-PTR-19")]
    public async Task A_read_only_field_ignores_a_scrub_step()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0, readOnly: true);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, 4);

        Assert.Empty(changes);
        Assert.Contains("\"readOnly\":true", test.ScrubOptions[0], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "clears the root scrubbing state when the scrub area unmounts mid-scrub")]
    [Trait("Requirement", "NUM-STA-15")]
    [Trait("Requirement", "NUM-PTR-17")]
    public async Task Clears_the_root_scrubbing_state_when_the_scrub_area_unmounts_mid_scrub()
    {
        bool mounted = true;
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            if (mounted)
            {
                b.OpenComponent<NumberFieldScrubArea>(1);
                b.CloseComponent();
            }
        }, value: 0);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, -10);
        Assert.True(cut.Find("div").HasAttribute("data-scrubbing"));

        mounted = false;
        cut.Render();

        Assert.False(cut.Find("div").HasAttribute("data-scrubbing"));
        Assert.Empty(commits);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NumberField.ScrubArea>")]
    [Trait("Requirement", "NUM-EDGE-02")]
    [Trait("Requirement", "NUM-STR-03")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_scrub_area()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render(b =>
        {
            b.OpenComponent<NumberFieldScrubAreaCursor>(0);
            b.CloseComponent();
        }));

        Assert.Equal(
            "Iodine: `NumberFieldScrubAreaContext` is missing. NumberFieldScrubArea parts must be placed within `<NumberFieldScrubArea>`.",
            error.Message);
    }

    [Fact(DisplayName = "renders when using mouse input")]
    [Trait("Requirement", "NUM-STR-06")]
    [Trait("Requirement", "NUM-DOM-13")]
    public async Task Renders_when_using_mouse_input()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);
        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));

        await test.ScrubStartAsync(cut, cursor: true);

        AngleSharp.Dom.IElement cursor = cut.Find("[data-testid=scrub-area-cursor]");
        Assert.Equal("presentation", cursor.GetAttribute("role"));
        Assert.Equal("position: fixed; top: 0; left: 0; pointer-events: none", cursor.GetAttribute("style"));
        Assert.True(cursor.ParentElement!.HasAttribute("data-base-ui-portal"));
    }

    [Fact(DisplayName = "only renders a cursor for the active scrub area")]
    [Trait("Requirement", "NUM-STR-05")]
    public async Task Only_renders_a_cursor_for_the_active_scrub_area()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            for (int i = 0; i < 2; i++)
            {
                b.OpenComponent<NumberFieldScrubArea>(1);
                b.AddComponentParameter(2, nameof(NumberFieldScrubArea.ChildContent), (RenderFragment)Cursor);
                b.CloseComponent();
            }
        }, value: 0);

        await test.ScrubStartAsync(cut, area: 0);

        Assert.Single(cut.FindAll("[data-testid=scrub-area-cursor]"));
    }

    [Fact(DisplayName = "does not render when using touch input")]
    [Trait("Requirement", "NUM-STR-06")]
    public async Task Does_not_render_when_using_touch_input()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut, cursor: false);

        Assert.NotNull(cut.Find("[data-scrubbing]"));
        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));
    }

    [Fact(DisplayName = "handles pointer lock denial through requestPointerLock API")]
    [Trait("Requirement", "NUM-STR-06")]
    [Trait("Requirement", "NUM-EDGE-09")]
    public async Task Handles_pointer_lock_denial()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        Assert.Single(cut.FindAll("[data-testid=scrub-area-cursor]"));
        await test.ScrubLockAsync(cut, denied: true);

        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));
        await test.ScrubStepAsync(cut, 3);
        Assert.Equal("3", test.Text);
    }

    [Fact(DisplayName = "does not render after a quick tap when pointer lock resolves later")]
    [Trait("Requirement", "NUM-STR-06")]
    [Trait("Requirement", "NUM-DOM-14")]
    public async Task Does_not_render_after_a_quick_tap_when_pointer_lock_resolves_later()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        Assert.Single(cut.FindAll("[data-testid=scrub-area-cursor]"));
        await test.ScrubEndAsync(cut);
        await test.ScrubLockAsync(cut, denied: false);

        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));
    }

    [Fact(DisplayName = "port: a consumer's PreventPartHandler() in onpointerdown stops the scrub-start decision")]
    [Trait("Requirement", "NUM-PTR-19")]
    [Trait("Requirement", "NUM-API-25")]
    public void A_consumer_PreventPartHandler_stops_the_scrub_start_decision()
    {
        int calls = 0;
        Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.AddAttribute(1, "onpointerdown", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.PointerEventArgs>(this, e =>
            {
                calls++;
                e.PreventPartHandler();
            }));
            b.CloseComponent();
        });

        string verdict = test.ScrubTwin().Decide("scrub-start", """{"kind":"PointerEvent","type":"pointerdown","pointerType":"mouse"}""");

        Assert.Equal("stop", verdict);
        Assert.Equal(1, calls);
    }

    private static void Cursor(RenderTreeBuilder b)
    {
        b.OpenComponent<NumberFieldScrubAreaCursor>(0);
        b.AddAttribute(1, "data-testid", "scrub-area-cursor");
        b.CloseComponent();
    }

    // Issue 375's review, nit (a): an attach that faults leaves the scrub area free to attach again at its next render,
    // rather than rethrowing the first fault at every render and never attaching.
    [Fact(DisplayName = "port: a scrub area whose attach faulted attaches again at its next render")]
    [Trait("Requirement", "NUM-API-24")]
    public async Task A_scrub_area_whose_attach_faulted_attaches_again()
    {
        FaultGate gate = new();
        FaultOnceRuntime runtime = new(test.Context.JSInterop.JSRuntime, gate);
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.CloseComponent();
        });
        gate.Release();

        // The renderer receives the fault from the part's after-render, so the part has handled it by then.
        Exception fault = await test.Context.Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<JSException>(fault);

        RenderTolerating(() => cut.Render(p => p.Add(x => x.ReadOnly, true)));

        Assert.True(SpinWait.SpinUntil(() => runtime.ScrubAttaches == 2, TimeSpan.FromSeconds(5)), $"{runtime.ScrubAttaches} scrub attaches");
        Assert.True(SpinWait.SpinUntil(() => test.ScrubOptions.Count == 1, TimeSpan.FromSeconds(5)));
    }

    // The review of PR 398: a retry whose options did not change since the faulted attempt still attaches with the full
    // options; the attempt that faulted sent nothing the module kept.
    [Fact(DisplayName = "port: a scrub area whose attach faulted attaches again with its full options when nothing changed")]
    [Trait("Requirement", "NUM-API-24")]
    public async Task A_scrub_area_whose_attach_faulted_attaches_again_with_its_full_options()
    {
        FaultGate gate = new();
        FaultOnceRuntime runtime = new(test.Context.JSInterop.JSRuntime, gate);
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        IRenderedComponent<NumberFieldRoot> cut = Render(ScrubAreaWithClass("a"));
        gate.Release();
        Exception fault = await test.Context.Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<JSException>(fault);

        // Only the consumer's class changes: the scrub area renders again with the options of the faulted attempt.
        RenderTolerating(() => cut.Render(p => p.Add(x => x.ChildContent, ScrubAreaWithClass("b"))));

        Assert.True(SpinWait.SpinUntil(() => runtime.ScrubAttaches == 2, TimeSpan.FromSeconds(5)), $"{runtime.ScrubAttaches} scrub attaches");
        Assert.True(SpinWait.SpinUntil(() => test.ScrubOptions.Count == 1, TimeSpan.FromSeconds(5)));
        Assert.Equal(["""{"part":"scrub"}"""], test.ScrubOptions);
    }

    private static RenderFragment ScrubAreaWithClass(string className) => b =>
    {
        b.OpenComponent<NumberFieldScrubArea>(0);
        b.AddAttribute(1, "class", className);
        b.CloseComponent();
    };

    private static void RenderTolerating(Action render)
    {
        try
        {
            render();
        }
        catch (JSException)
        {
            // The first attach's fault surfaces through the renderer once.
        }
    }

    private IRenderedComponent<NumberFieldRoot> RenderScrub(double? value, bool readOnly = false) => Render(b =>
    {
        b.OpenComponent<NumberFieldInput>(0);
        b.CloseComponent();
        b.OpenComponent<NumberFieldScrubArea>(1);
        b.AddComponentParameter(2, nameof(NumberFieldScrubArea.ChildContent), (RenderFragment)Cursor);
        b.CloseComponent();
    }, value, readOnly);

    private IRenderedComponent<NumberFieldRoot> Render(RenderFragment parts, double? value = null, bool readOnly = false) =>
        test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, value)
            .Add(x => x.ReadOnly, readOnly)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e))
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double?> e) => commits.Add(e))
            .Add(x => x.ChildContent, parts));

    // The `number-field` module's first scrub attach throws, as a module that failed to attach; every other call goes to
    // bUnit's runtime. A test double, not interop.
#pragma warning disable BL0016
    private sealed class FaultOnceRuntime(IJSRuntime inner, FaultGate gate) : IJSRuntime
    {
        public int ScrubAttaches { get; private set; }

        public FaultGate Gate => gate;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            TValue result = await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            return identifier == "import" && args is [string path] && path.EndsWith("/number-field.js", StringComparison.Ordinal) && result is IJSObjectReference module
                ? (TValue)(object)new Module(this, module)
                : result;
        }

        private sealed class Module(FaultOnceRuntime runtime, IJSObjectReference inner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "attach" && args is [_, string options, ..] && options.Contains("\"part\":\"scrub\"", StringComparison.Ordinal))
                {
                    runtime.ScrubAttaches++;
                    if (runtime.ScrubAttaches == 1)
                    {
                        return new ValueTask<TValue>(FaultAsync<TValue>());
                    }
                }

                return inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            }

            // The fault arrives after the render returns, as a module's rejected attach does.
            private async Task<TValue> FaultAsync<TValue>()
            {
                await runtime.Gate.Released;
                throw new JSException("Iodine: the scrub attach failed.");
            }

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
#pragma warning restore BL0016

    // PR 404's CI run 45957621568: a fault that only yielded could still land while a slow first Render ran, and bUnit
    // rethrew it from that Render. The test releases the fault once its first Render has returned.
    /// <summary>Holds <see cref="FaultOnceRuntime"/>'s fault until <see cref="Release"/>.</summary>
    private sealed class FaultGate
    {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Released => released.Task;

        public void Release() => released.TrySetResult();
    }
}
