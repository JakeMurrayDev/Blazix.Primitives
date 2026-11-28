using AngleSharp.Dom;
using Iodine.Headless.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless.Tests.Components.Rendering;

/// <summary>
/// A part's created handler is on its element only in a render whose <c>AddPartAttributes</c> adds it (issue 87; ADR 02
/// decision 3, the part registers the real handler; ADR 16 rule 6, a C# handler only where its result is state). Before
/// issue 87 the pipeline emitted every handler <c>CreateHandler</c> had made on every render, so a part that adds its key
/// handlers only to run a declined consumer handler (the button mechanism, the composite behaviours) carried them always,
/// and on Server every keydown reached C# as a message.
/// </summary>
public sealed class CreatedHandlerEmissionTests : IDisposable
{
    private readonly BunitContext context = new();

    public CreatedHandlerEmissionTests()
    {
        context.Services.AddIodineHeadless();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a part's created handler is on the element in a render that adds it and absent from one that does not")]
    [Trait("Requirement", "RND-EVT-02")]
    [Trait("Requirement", "ADR16-6")]
    public void A_parts_created_handler_is_on_the_element_only_in_a_render_that_adds_it()
    {
        IRenderedComponent<ConditionalHandlerPart> cut = context.Render<ConditionalHandlerPart>(p => p.Add(x => x.AddsHandler, true));

        Assert.True(cut.Find("div").HasAttribute("blazor:onkeydown"));
        cut.Find("div").KeyDown("Enter");
        Assert.Equal(1, cut.Instance.HandlerRuns);

        cut.Render(p => p.Add(x => x.AddsHandler, false));

        Assert.False(cut.Find("div").HasAttribute("blazor:onkeydown"));
    }

    [Fact(DisplayName = "a consumer handler for an event whose created handler the part did not add runs alone")]
    [Trait("Requirement", "RND-EVT-02")]
    [Trait("Requirement", "ADR16-6")]
    public void A_consumer_handler_for_an_event_whose_created_handler_the_part_did_not_add_runs_alone()
    {
        List<string> log = [];
        IRenderedComponent<ConditionalHandlerPart> cut = context.Render<ConditionalHandlerPart>(p => p
            .Add(x => x.AddsHandler, false)
            .Add(x => x.Log, log)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(new Receiver(), _ => log.Add("consumer"))));

        cut.Find("div").KeyDown("Enter");

        Assert.Equal(["consumer"], log);
        Assert.Equal(0, cut.Instance.HandlerRuns);
    }

    [Fact(DisplayName = "a button renders its key handlers with a consumer key handler and none without one")]
    [Trait("Requirement", "BTN-API-05")]
    [Trait("Requirement", "ADR16-6")]
    public void A_button_renders_its_key_handlers_with_a_consumer_key_handler_and_none_without_one()
    {
        int keyDowns = 0;
        IRenderedComponent<Button> handled = context.Render<Button>(p => p
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(new Receiver(), _ => keyDowns++))
            .AddChildContent("Save"));
        IRenderedComponent<Button> plain = context.Render<Button>(p => p.AddChildContent("Save"));

        IElement button = handled.Find("button");
        Assert.True(button.HasAttribute("blazor:onkeydown"));
        button.KeyDown("Shift");
        Assert.Equal(1, keyDowns);

        IElement bare = plain.Find("button");
        Assert.False(bare.HasAttribute("blazor:onkeydown"));
        Assert.False(bare.HasAttribute("blazor:onkeyup"));
    }

    // ComboboxTrigger.tsx:182-199: the trigger's own mousedown forces the list to mount and, with the input outside the popup or
    // none, makes the trigger the floating root's reference. Its result is state, so the trigger adds it while enabled whatever
    // the consumer supplies; its key handlers it adds only to run a declined consumer handler.
    [Fact(DisplayName = "a combobox trigger without consumer handlers carries its mousedown handler, whose press mounts the list, and no key handler")]
    [Trait("Requirement", "CMB-STA-32")]
    [Trait("Requirement", "CMB-PTR-02")]
    [Trait("Requirement", "ADR16-6")]
    public async Task A_combobox_trigger_without_consumer_handlers_carries_its_mousedown_handler_and_no_key_handler()
    {
        using ComboboxTestContext combobox = new();
        IRenderedComponent<ComboboxHarness> cut = combobox.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)(builder =>
            {
                builder.OpenComponent<ComboboxTrigger>(0);
                builder.AddComponentParameter(1, "data-testid", "trigger");
                builder.CloseComponent();
            })));
        IElement trigger = cut.Find("[data-testid=trigger]");

        Assert.True(trigger.HasAttribute("blazor:onmousedown"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=trigger]").MouseDownAsync(new MouseEventArgs { Button = 0, Type = "mousedown" }));
        Assert.True(cut.Instance.Root!.Store.State.ForceMounted);

        trigger = cut.Find("[data-testid=trigger]");
        Assert.False(trigger.HasAttribute("blazor:onkeydown"));
        Assert.False(trigger.HasAttribute("blazor:onkeyup"));
    }

    /// <summary>A part that creates an <c>onkeydown</c> handler and adds it only while <see cref="AddsHandler"/> is set.</summary>
    public sealed class ConditionalHandlerPart : PartBase<EmptyState>
    {
        private readonly EventCallback onKeyDown;

        public ConditionalHandlerPart()
        {
            onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        }

        [Parameter]
        public bool AddsHandler { get; set; }

        [Parameter]
        public List<string>? Log { get; set; }

        public int HandlerRuns { get; private set; }

        /// <inheritdoc />
        protected override string DefaultTagName => "div";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override void AddPartAttributes(AttributeList attributes)
        {
            if (AddsHandler)
            {
                attributes.Set("onkeydown", onKeyDown);
            }
        }

        private Task HandleKeyDownAsync(KeyboardEventArgs e)
        {
            HandlerRuns++;
            Log?.Add("part");
            return Task.CompletedTask;
        }
    }

    private sealed class Receiver : IHandleEvent
    {
        /// <inheritdoc />
        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
    }
}
