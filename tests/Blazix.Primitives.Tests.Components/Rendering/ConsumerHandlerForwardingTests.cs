using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// The part's say over the consumer's handlers (ADR 02 Amendments 2026-10-21): a part declines a consumer handler by
/// event name through <c>PartBase.ForwardsConsumerHandler</c>, and the pipeline leaves it off the element, which is how a
/// button-like part gates the consumer's handlers while disabled (UBT-EVT-01/03, BTN-DOM-05) without copying the bag.
/// </summary>
public sealed class ConsumerHandlerForwardingTests : IDisposable
{
    private readonly BunitContext context = new();

    public ConsumerHandlerForwardingTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a declined consumer handler is not rendered, so Blazor has nothing to invoke")]
    [Trait("Requirement", "UBT-EVT-03")]
    public void A_declined_consumer_handler_is_not_rendered_so_Blazor_has_nothing_to_invoke()
    {
        List<string> calls = [];
        IRenderedComponent<GatingPart> cut = context.Render<GatingPart>(p => p
            .Add(x => x.Declined, "onclick")
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("click")))
            .AddUnmatched("onmousemove", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("mousemove"))));

        Assert.Throws<MissingEventHandlerException>(() => cut.Find("div").Click());
        cut.Find("div").MouseMove();

        Assert.Equal(["mousemove"], calls);
    }

    [Fact(DisplayName = "the event name is asked in lowercase, whatever the consumer's casing")]
    [Trait("Requirement", "UBT-EVT-03")]
    public void The_event_name_is_asked_in_lowercase_whatever_the_consumers_casing()
    {
        IRenderedComponent<GatingPart> cut = context.Render<GatingPart>(p => p
            .Add(x => x.Declined, "onclick")
            .AddUnmatched("onClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => { })));

        Assert.Contains("onclick", cut.Instance.Asked);
        Assert.Throws<MissingEventHandlerException>(() => cut.Find("div").Click());
    }

    [Fact(DisplayName = "a declined name keeps a substitute element's own handler")]
    [Trait("Requirement", "BTN-DOM-05")]
    public void A_declined_name_keeps_a_substitute_elements_own_handler()
    {
        List<string> calls = [];
        IRenderedComponent<GatingPart> cut = context.Render<GatingPart>(p => p
            .Add(x => x.Declined, "onclick")
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("consumer")))
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("substitute")));
                builder.CloseElement();
            })));

        cut.Find("span").Click();

        Assert.Equal(["substitute"], calls);
    }

    [Fact(DisplayName = "a value that is not a handler is never asked about and stays an attribute")]
    [Trait("Requirement", "RND-ALG-13")]
    public void A_value_that_is_not_a_handler_is_never_asked_about_and_stays_an_attribute()
    {
        IRenderedComponent<GatingPart> cut = context.Render<GatingPart>(p => p
            .Add(x => x.Declined, "onclick")
            .AddUnmatched("onclick", "alert(1)"));

        Assert.Empty(cut.Instance.Asked);
        Assert.Equal("alert(1)", cut.Find("div").GetAttribute("onclick"));
    }

    [Fact(DisplayName = "the part is asked on every render, so a handler comes back when it stops declining")]
    [Trait("Requirement", "UBT-EVT-03")]
    public void The_part_is_asked_on_every_render_so_a_handler_comes_back_when_it_stops_declining()
    {
        int clicks = 0;
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++);
        IRenderedComponent<GatingPart> cut = context.Render<GatingPart>(p => p.Add(x => x.Declined, "onclick").AddUnmatched("onclick", onClick));

        cut.Render(p => p.Add(x => x.Declined, null).AddUnmatched("onclick", onClick));
        cut.Find("div").Click();

        Assert.Equal(1, clicks);
    }

    /// <summary>A part that declines the consumer handler named by <see cref="Declined"/> and records every name it was asked about.</summary>
    private sealed class GatingPart : PartBase<EmptyState>
    {
        [Parameter]
        public string? Declined { get; set; }

        public List<string> Asked { get; } = [];

        /// <inheritdoc />
        protected override string DefaultTagName => "div";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override bool ForwardsConsumerHandler(string eventName)
        {
            Asked.Add(eventName);
            return !string.Equals(eventName, Declined, StringComparison.Ordinal);
        }
    }
}
