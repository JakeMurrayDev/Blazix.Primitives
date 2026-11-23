using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// ADR 02 decision 1 for a substitute whose root is a component: the component's own parameters keep the types Razor
/// passed, so a typed callback the component declares (a part's <c>OnPressedChange</c>, <c>OnCheckedChange</c>) stays
/// typed (design point 14.8 of M3 track 4); a DOM handler it does not declare joins the handler chain as before
/// (PartHandlerTests).
/// </summary>
public sealed class SubstituteComponentParameterTests : IDisposable
{
    private readonly BunitContext context = new();

    public SubstituteComponentParameterTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a component substitute's typed On* parameters stay typed parameters and never become handler attributes")]
    [Trait("Requirement", "RND-STR-03")]
    [Trait("Requirement", "RND-DOM-43")]
    public async Task Port_a_component_substitutes_typed_on_parameters_stay_typed()
    {
        List<string> log = [];
        EventCallback<ValueChangeEventArgs<bool>> onValueChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, e => log.Add($"value:{e.Value}"));
        Action<string> onSelect = text => log.Add($"select:{text}");
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.Add(x => x.Render, (RenderFragment)(builder =>
        {
            builder.OpenComponent<CallbackProbe>(0);
            builder.AddComponentParameter(1, nameof(CallbackProbe.OnValueChange), onValueChange);
            builder.AddComponentParameter(2, nameof(CallbackProbe.OnSelect), onSelect);
            builder.CloseComponent();
        })));

        CallbackProbe probe = cut.FindComponent<CallbackProbe>().Instance;
        await cut.InvokeAsync(() => probe.OnValueChange.InvokeAsync(new ValueChangeEventArgs<bool>(true, new ChangeEventDetails(EventReason.None, null))));
        probe.OnSelect!("one");

        Assert.Equal(["value:True", "select:one"], log);

        // The part's own attributes arrive unmatched; the declared callbacks are not among them.
        Assert.DoesNotContain(probe.Attributes!.Keys, key => key.StartsWith("onvalue", StringComparison.OrdinalIgnoreCase) || key.StartsWith("onselect", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A component substitute with a typed callback, a delegate parameter, and the attributes it does not declare.</summary>
    private sealed class CallbackProbe : ComponentBase
    {
        [Parameter]
        public EventCallback<ValueChangeEventArgs<bool>> OnValueChange { get; set; }

        [Parameter]
        public Action<string>? OnSelect { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? Attributes { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, Attributes);
            builder.CloseElement();
        }
    }
}
