// Family tests use the project's root namespace (a test namespace is never named like a part).
namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: the items a menu's root records for MNU-EVT-10's label (design point 9 (b)) and for the popup's initial focus.
/// </summary>
public sealed class MenuItemRegistryTests
{
    [Fact(DisplayName = "port: a registered item's explicit label is read by its rendered id")]
    public void ReadsTheLabelByTheRenderedId()
    {
        MenuItemRegistry registry = new();
        object first = new();
        object second = new();

        registry.Set(first, "item-1", "Copy");
        registry.Set(second, "item-2", null);

        Assert.Equal(2, registry.Count);
        Assert.Equal("Copy", registry.LabelOf("item-1"));
        Assert.Null(registry.LabelOf("item-2"));
        Assert.Null(registry.LabelOf("unknown"));
        Assert.Null(registry.LabelOf(null));
    }

    [Fact(DisplayName = "port: an item whose id changes keeps one entry, under its new id")]
    public void ReplacesTheEntryOfAnItemWhoseIdChanges()
    {
        MenuItemRegistry registry = new();
        object item = new();

        registry.Set(item, "before", "Paste");
        registry.Set(item, "after", "Paste special");

        Assert.Equal(1, registry.Count);
        Assert.Null(registry.LabelOf("before"));
        Assert.Equal("Paste special", registry.LabelOf("after"));
    }

    [Fact(DisplayName = "port: a removed item leaves the registry")]
    public void RemovesAnItem()
    {
        MenuItemRegistry registry = new();
        object item = new();
        registry.Set(item, "item-1", "Cut");

        registry.Remove(item);
        registry.Remove(item);

        Assert.Equal(0, registry.Count);
        Assert.Null(registry.LabelOf("item-1"));
    }
}
