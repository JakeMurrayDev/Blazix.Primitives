// Family tests use the project's root namespace (a test namespace is never named like a part).
namespace Blazix.Primitives.Tests;

/// <summary>L1: the items a menu's root counts for the popup's initial focus.</summary>
public sealed class MenuItemRegistryTests
{
    [Fact(DisplayName = "port: each registered item counts once, and an item whose id changes keeps one entry")]
    public void Each_registered_item_counts_once_and_an_item_whose_id_changes_keeps_one_entry()
    {
        MenuItemRegistry registry = new();
        object first = new();
        object second = new();

        registry.Set(first, "item-1");
        registry.Set(second, "item-2");
        registry.Set(first, "item-1-renamed");

        Assert.Equal(2, registry.Count);
    }

    [Fact(DisplayName = "port: a removed item leaves the registry")]
    public void A_removed_item_leaves_the_registry()
    {
        MenuItemRegistry registry = new();
        object item = new();
        registry.Set(item, "item-1");

        registry.Remove(item);
        registry.Remove(item);

        Assert.Equal(0, registry.Count);
    }
}
