using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.Logging.Abstractions;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `server-side rendering` titles at L2 (batch A). Base UI's renderToString: HtmlRenderer renders the
// HTML a prerender does and runs no OnAfterRender, so no module attaches.
/// <summary>Combobox's root at L2: server-side rendering.</summary>
public sealed class ComboboxRootServerSideRenderingTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets combobox aria attributes on the input")]
    [Trait("Requirement", "CMB-DOM-01")]
    [Trait("Requirement", "CMB-ARIA-01")]
    [Trait("Requirement", "CMB-EDGE-06")]
    public async Task Sets_combobox_aria_attributes_on_the_input()
    {
        using AngleSharp.Html.Dom.IHtmlDocument document = await RenderToStringAsync(builder =>
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.AddComponentParameter(1, "data-testid", "input");
            builder.CloseComponent();
            Portal(builder);
        });

        IElement input = document.QuerySelector("[data-testid=input]")!;
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("false", input.GetAttribute("aria-expanded"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal("listbox", input.GetAttribute("aria-haspopup"));
    }

    [Fact(DisplayName = "sets combobox aria attributes on the trigger when input is inside popup")]
    [Trait("Requirement", "CMB-ARIA-07")]
    [Trait("Requirement", "CMB-EDGE-06")]
    public async Task Sets_combobox_aria_attributes_on_the_trigger_when_input_is_inside_popup()
    {
        using AngleSharp.Html.Dom.IHtmlDocument document = await RenderToStringAsync(builder =>
        {
            builder.OpenComponent<ComboboxTrigger>(0);
            builder.AddComponentParameter(1, "data-testid", "trigger");
            builder.CloseComponent();
            Portal(builder);
        });

        IElement trigger = document.QuerySelector("[data-testid=trigger]")!;
        Assert.Equal("combobox", trigger.GetAttribute("role"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
    }

    // <Combobox.Portal><Combobox.Positioner /></Combobox.Portal>.
    private static void Portal(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ComboboxPortal>(10);
        builder.AddComponentParameter(11, nameof(ComboboxPortal.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<ComboboxPositioner>(0);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private async Task<AngleSharp.Html.Dom.IHtmlDocument> RenderToStringAsync(RenderFragment parts)
    {
        await using HtmlRenderer renderer = new(test.Context.Services, NullLoggerFactory.Instance);
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent root = await renderer.RenderComponentAsync<ComboboxRoot<string>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ComboboxRoot<string>.ChildContent)] = parts,
            }));
            return root.ToHtmlString();
        });
        return new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
    }
}
