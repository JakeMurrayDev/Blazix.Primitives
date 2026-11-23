using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>L2: <c>MenuPopup.test.tsx</c>'s L2 titles over <see cref="MenuTreeHost"/> (MNU-STR-05, EDGE-03, ARIA-04, ARIA-19).</summary>
public sealed class MenuPopupTests : BunitContext
{
    public MenuPopupTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside Menu.Positioner")]
    [Trait("Requirement", "MNU-STR-05")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_the_positioner()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuTreeHost>(p => p.Add(x => x.NoPositioner, true)));

        Assert.Equal("Blazix: The `MenuPositioner` context is missing. MenuPositioner parts must be placed within `<MenuPositioner>`.", error.Message);
    }

    [Fact(DisplayName = "uses an aria-label instead of the trigger label")]
    [Trait("Requirement", "MNU-ARIA-04")]
    [Trait("Requirement", "MNU-ARIA-19")]
    public void Uses_an_aria_label_instead_of_the_trigger_label()
    {
        IRenderedComponent<MenuTreeHost> cut = Render<MenuTreeHost>(p => p
            .Add(x => x.WithTrigger, true)
            .Add(x => x.TriggerText, "Actions")
            .Add(x => x.PopupAttributes, new Dictionary<string, object> { ["aria-label"] = "Commands" }));

        IElement popup = cut.Find("[role=menu]");
        Assert.Equal("Commands", popup.GetAttribute("aria-label"));
        Assert.False(popup.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "uses aria-labelledby from a render element")]
    [Trait("Requirement", "MNU-ARIA-04")]
    [Trait("Requirement", "MNU-ARIA-19")]
    public void Uses_aria_labelledby_from_a_render_element()
    {
        IRenderedComponent<MenuTreeHost> cut = Render<MenuTreeHost>(p => p
            .Add(x => x.BeforePortal, (RenderFragment)(b => b.AddMarkupContent(0, "<span id=\"commands-label\">Commands</span>")))
            .Add(x => x.PopupRenderFunction, (RenderFragment<RenderProps<MenuPopupState>>)(props => b =>
            {
                b.OpenElement(0, "section");
                b.AddMultipleAttributes(1, props.Attributes);
                b.AddAttribute(2, "aria-labelledby", "commands-label");
                b.AddContent(3, props.ChildContent);
                b.CloseElement();
            })));

        Assert.Equal("commands-label", cut.Find("[role=menu]").GetAttribute("aria-labelledby"));
    }
}
