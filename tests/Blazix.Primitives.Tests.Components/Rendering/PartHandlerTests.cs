using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// The handler chain and veto on a rendered part (RND-EVT-02/03/04, ADR 02 decision 3), the directive-key diagnostic
/// (ADR 04 Amendments 2026-10-18 item 3), and the <see cref="IHandleEvent"/> rule (ADR 16 rule 8) through bUnit's
/// render counts.
/// </summary>
public sealed class PartHandlerTests : IDisposable
{
    private readonly BunitContext context = new();

    public PartHandlerTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "handlers run substitute, then consumer, then part")]
    [Trait("Requirement", "RND-EVT-02")]
    public void Handlers_run_substitute_then_consumer_then_part()
    {
        List<string> log = [];
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("onclick", Handler(_ => log.Add("consumer")))
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "onclick", Handler(_ => log.Add("substitute")));
                builder.CloseElement();
            })));

        cut.Find("div").Click();

        Assert.Equal(["substitute", "consumer"], log);
        Assert.Equal(1, cut.Instance.HandlerRuns);
        Assert.True(cut.Instance.Open);
    }

    [Fact(DisplayName = "a consumer veto skips the part's handler and the flag reads true for the rest of the dispatch")]
    [Trait("Requirement", "RND-EVT-04")]
    public void A_consumer_veto_skips_the_parts_handler_and_the_flag_reads_true_for_the_rest_of_the_dispatch()
    {
        bool? flagBefore = null;
        bool? flagAfter = null;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("onclick", Handler(e =>
            {
                flagBefore = e.PartHandlerPrevented();
                e.PreventPartHandler();
                flagAfter = e.PartHandlerPrevented();
            })));

        cut.Find("div").Click();

        Assert.False(flagBefore);
        Assert.True(flagAfter);
        Assert.Equal(0, cut.Instance.HandlerRuns);
        Assert.False(cut.Instance.Open);
    }

    [Fact(DisplayName = "a substitute veto skips the consumer's handler and the part's")]
    [Trait("Requirement", "RND-EVT-03")]
    public void A_substitute_veto_skips_the_consumers_handler_and_the_parts()
    {
        List<string> log = [];
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("onclick", Handler(_ => log.Add("consumer")))
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "onclick", Handler(e =>
                {
                    log.Add("substitute");
                    e.PreventPartHandler();
                }));
                builder.CloseElement();
            })));

        cut.Find("div").Click();

        Assert.Equal(["substitute"], log);
        Assert.Equal(0, cut.Instance.HandlerRuns);
    }

    [Fact(DisplayName = "a consumer handler for an event the part does not handle runs alone")]
    [Trait("Requirement", "RND-EVT-06")]
    public void A_consumer_handler_for_an_event_the_part_does_not_handle_runs_alone()
    {
        int calls = 0;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("onkeydown", Handler<KeyboardEventArgs>(_ => calls++)));

        cut.Find("div").KeyDown("Enter");

        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "a substitute handler and a consumer handler for an event the part does not handle chain in order")]
    [Trait("Requirement", "RND-EVT-02")]
    public void A_substitute_handler_and_a_consumer_handler_for_an_event_the_part_does_not_handle_chain_in_order()
    {
        List<string> log = [];
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("onkeydown", Handler<KeyboardEventArgs>(_ => log.Add("consumer")))
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "onkeydown", Handler<KeyboardEventArgs>(e =>
                {
                    log.Add("substitute");
                    e.PreventPartHandler();
                }));
                builder.CloseElement();
            })));

        cut.Find("div").KeyDown("Enter");

        Assert.Equal(["substitute"], log);
    }

    [Fact(DisplayName = "the part's own handler re-renders the part, a vetoed or locked one does not")]
    [Trait("Requirement", "ADR16-8")]
    public void The_parts_own_handler_re_renders_the_part_a_vetoed_or_locked_one_does_not()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>();
        int renders = cut.RenderCount;

        cut.Find("div").Click();
        Assert.Equal(renders + 1, cut.RenderCount);
        Assert.Equal("", cut.Find("div").GetAttribute("data-open"));

        IRenderedComponent<SampleRoot> locked = context.Render<SampleRoot>(p => p.Add(x => x.Locked, true));
        renders = locked.RenderCount;
        locked.Find("div").Click();
        Assert.Equal(1, locked.Instance.HandlerRuns);
        Assert.Equal(renders, locked.RenderCount);

        IRenderedComponent<SampleRoot> vetoed = context.Render<SampleRoot>(p => p.AddUnmatched("onclick", Handler(e => e.PreventPartHandler())));
        renders = vetoed.RenderCount;
        vetoed.Find("div").Click();
        Assert.Equal(renders, vetoed.RenderCount);
    }

    [Fact(DisplayName = "a consumer-only handler does not re-render the part")]
    [Trait("Requirement", "ADR16-8")]
    public void A_consumer_only_handler_does_not_re_render_the_part()
    {
        int calls = 0;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("onkeydown", Handler<KeyboardEventArgs>(_ => calls++)));
        int renders = cut.RenderCount;

        cut.Find("div").KeyDown("Enter");

        Assert.Equal(1, calls);
        Assert.Equal(renders, cut.RenderCount);
    }

    [Fact(DisplayName = "an exception in the part's handler reaches the renderer's error handling")]
    [Trait("Requirement", "ADR16-8")]
    public void An_exception_in_the_parts_handler_reaches_the_renderers_error_handling()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.Add(x => x.ThrowOnClick, true));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => cut.Find("div").Click());

        Assert.Equal("SampleRoot click failed on purpose.", error.Message);
    }

    [Fact(DisplayName = "the directive key literal is the one RenderTreeBuilder writes")]
    [Trait("Requirement", "ADR04-2026-10-18-3")]
    public void The_directive_key_literal_is_the_one_RenderTreeBuilder_writes()
    {
        // ADR 04 Amendments 2026-10-18, verified paragraph: confirm the literal in the first L2 test.
        using RenderTreeBuilder builder = new();
        builder.OpenComponent<SampleRoot>(0);
        builder.AddEventPreventDefaultAttribute(1, "onclick", true);
        builder.AddEventStopPropagationAttribute(2, "onkeydown", true);
        builder.CloseComponent();

        ArrayRange<RenderTreeFrame> frames = builder.GetFrames();
        Assert.Equal("__internal_preventDefault_onclick", frames.Array[1].AttributeName);
        Assert.Equal("__internal_stopPropagation_onkeydown", frames.Array[2].AttributeName);
    }

    [Fact(DisplayName = "a preventDefault or stopPropagation directive on a part throws and names the two sanctioned channels")]
    [Trait("Requirement", "ADR04-2026-10-18-3")]
    public void A_preventDefault_or_stopPropagation_directive_on_a_part_throws_and_names_the_two_sanctioned_channels()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<SampleRoot>(p => p.AddUnmatched("__internal_preventDefault_onclick", true)));

        Assert.Contains("@onclick:preventDefault", error.Message);
        Assert.Contains("PreventPartHandler()", error.Message);
        Assert.Contains("preventDefault()", error.Message);

        error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<SampleRoot>(p => p.AddUnmatched("__internal_stopPropagation_onkeydown", true)));

        Assert.Contains("@onkeydown:stopPropagation", error.Message);
    }

    [Fact(DisplayName = "a preventDefault directive on a substitute element throws")]
    [Trait("Requirement", "ADR04-2026-10-18-3")]
    public void A_preventDefault_directive_on_a_substitute_element_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<SampleRoot>(p => p.Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddEventPreventDefaultAttribute(1, "onclick", true);
                builder.CloseElement();
            }))));

        Assert.Contains("@onclick:preventDefault", error.Message);
        Assert.Contains("PreventPartHandler()", error.Message);
    }

    private static EventCallback<MouseEventArgs> Handler(Action<MouseEventArgs> handler) => EventCallback.Factory.Create(new Receiver(), handler);

    private static EventCallback<TArgs> Handler<TArgs>(Action<TArgs> handler) => EventCallback.Factory.Create(new Receiver(), handler);

    private sealed class Receiver : IHandleEvent
    {
        /// <inheritdoc />
        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
    }
}
