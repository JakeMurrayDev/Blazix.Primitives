using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// DRW-DOM-22's consumer clause (DrawerViewport.tsx:1052-1054: the consumer's props are merged, then the attribute is set to
/// undefined): the viewport never renders <c>data-nested-dialog-open</c>, even when the consumer passes it.
/// </summary>
public sealed class DrawerViewportTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the viewport drops a data-nested-dialog-open the consumer passes")]
    [Trait("Requirement", "DRW-DOM-22")]
    public void The_viewport_drops_a_consumer_data_nested_dialog_open()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(builder =>
        {
            builder.OpenComponent<DrawerRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.Open), true);
            builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
            {
                root.OpenComponent<DrawerPortal>(0);
                root.AddComponentParameter(1, nameof(DrawerPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<DrawerViewport>(0);
                    portal.AddAttribute(1, "data-testid", "viewport");
                    portal.AddAttribute(2, "data-nested-dialog-open", string.Empty);
                    portal.CloseComponent();
                }));
                root.CloseComponent();
            }));
            builder.CloseComponent();
        });

        IElement viewport = cut.Find("[data-testid=viewport]");
        Assert.True(viewport.HasAttribute("data-open"));
        Assert.False(viewport.HasAttribute("data-nested-dialog-open"));
    }
}
