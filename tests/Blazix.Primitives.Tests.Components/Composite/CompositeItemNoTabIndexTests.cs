using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The registration without a <c>tabindex</c> (M5 track 1, issue 159, Deliverable 10; NAV-DOM-21 and NAV-FOC-03, sheet F
/// Q15 (a)): a navigation menu link renders no <c>tabindex</c>, so it is a tab stop by its native semantics, and its
/// registration says so, so the module writes none; a consumer <c>tabindex</c> still wins and travels as the consumer's.
/// </summary>
public sealed class CompositeItemNoTabIndexTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "an item registered without a tabindex renders none and its registration says so, and a consumer tabindex still wins")]
    [Trait("Requirement", "NAV-DOM-21")]
    [Trait("Requirement", "NAV-FOC-03")]
    public void An_item_registered_without_a_tabindex_renders_none_and_says_so()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Items()));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        IElement link = cut.Find("[data-item='a']");
        IElement plain = cut.Find("[data-item='b']");
        IElement consumer = cut.Find("[data-item='c']");
        Assert.Equal("-1", plain.GetAttribute("tabindex"));
        Assert.Equal("0", consumer.GetAttribute("tabindex"));
        Assert.False(link.HasAttribute("tabindex"));

        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;
        Assert.Contains($$"""
            "items":[{"id":"{{ids[0]}}","omitTabIndex":true},{"id":"{{ids[1]}}"},{"id":"{{ids[2]}}","tabIndex":"0","omitTabIndex":true}]
            """.Trim(), attached);
    }

    private static RenderFragment Items() => builder =>
    {
        builder.OpenComponent<TestCompositeItem>(0);
        builder.AddAttribute(1, "data-item", "a");
        builder.AddAttribute(2, nameof(TestCompositeItem.Tag), "a");
        builder.AddAttribute(3, nameof(TestCompositeItem.OmitTabIndex), true);
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(4);
        builder.AddAttribute(5, "data-item", "b");
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(6);
        builder.AddAttribute(7, "data-item", "c");
        builder.AddAttribute(8, "tabindex", "0");
        builder.AddAttribute(9, nameof(TestCompositeItem.OmitTabIndex), true);
        builder.CloseComponent();
    };
}
