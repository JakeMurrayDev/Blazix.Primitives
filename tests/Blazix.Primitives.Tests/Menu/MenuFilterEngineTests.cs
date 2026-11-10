using System.Globalization;

// Family tests use the project's root namespace (a test namespace is never named like a part).
namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: the C# filter of a filterable menu (MNU §4.4; M5 track 12), Base UI's FilterDropdownRoot rules
/// (filter-dropdown/root/FilterDropdownRoot.tsx:86-196, menu/filter-root/MenuFilterList.tsx:101-123) as tables.
/// </summary>
public sealed class MenuFilterEngineTests
{
    private static readonly Func<string, string, bool> s_contains = MenuFilter.Create(CultureInfo.GetCultureInfo("en-US")).Contains;

    public static TheoryData<string, bool, string, bool> AutoHighlightCases => new()
    {
        { "false", true, "a", false },
        { "true", true, "", false },
        { "true", true, "a", true },
        { "true", false, "a", false },
        { "always", true, "", true },
        { "always", false, "", false },
        { "unset", true, "a", false },
    };

    public static TheoryData<bool, string, string> ValueChangeCases => new()
    {
        { false, "false", "clear" },
        { true, "false", "keep" },
        { false, "true", "keep" },
        { false, "always", "keep" },
    };

    public static TheoryData<bool, bool, int, string> ItemsChangeCases => new()
    {
        { true, true, 3, "keep" },
        { true, false, 3, "keep" },
        { false, true, 3, "first" },
        { false, true, 0, "clear" },
        { false, false, 3, "clear" },
    };

    [Theory(DisplayName = "port: the automatic highlight applies while open with Always, or with true and a non-empty query")]
    [MemberData(nameof(AutoHighlightCases))]
    [Trait("Requirement", "MNU-STA-19")]
    public void The_automatic_highlight_applies_while_open_with_always_or_with_true_and_a_non_empty_query(string autoHighlight, bool open, string filterQuery, bool expected) =>
        Assert.Equal(expected, MenuFilterEngine.AutoHighlightEnabled(open, Auto(autoHighlight), filterQuery));

    [Theory(DisplayName = "port: a query change clears the positional highlight unless it was canceled or the highlight is automatic")]
    [MemberData(nameof(ValueChangeCases))]
    [Trait("Requirement", "MNU-STA-18")]
    public void A_query_change_clears_the_positional_highlight_unless_canceled_or_automatic(bool canceled, string autoHighlight, string expected) =>
        Assert.Equal(expected, Code(MenuFilterEngine.AfterValueChange(canceled, Auto(autoHighlight))));

    [Theory(DisplayName = "port: an item-list change keeps the highlight only on the same element, else the first item or none")]
    [MemberData(nameof(ItemsChangeCases))]
    [Trait("Requirement", "MNU-STA-20")]
    public void An_item_list_change_keeps_the_highlight_only_on_the_same_element(bool sameItemAtHighlight, bool autoHighlightEnabled, int itemCount, string expected) =>
        Assert.Equal(expected, Code(MenuFilterEngine.AfterItemsChange(sameItemAtHighlight, autoHighlightEnabled, itemCount)));

    [Fact(DisplayName = "port: an empty query or a null filter keeps every registered item visible")]
    [Trait("Requirement", "MNU-STA-15")]
    public void An_empty_query_or_a_null_filter_keeps_every_registered_item_visible()
    {
        MenuFilterEngine engine = new();
        MenuFilterItem apple = Register(engine, "Apple");
        MenuFilterItem banana = Register(engine, "Banana");

        engine.Recompute(Pass("  "));
        Assert.False(engine.HasVisibleSet);
        Assert.True(apple.Visible && banana.Visible);

        engine.Recompute(Pass("zz", match: null));
        Assert.False(engine.HasVisibleSet);
        Assert.True(apple.Visible && banana.Visible);
        Assert.False(engine.IsEmpty);
    }

    [Fact(DisplayName = "port: items that do not match the trimmed query are hidden, and the menu is empty when none matches")]
    [Trait("Requirement", "MNU-STA-15")]
    [Trait("Requirement", "MNU-STA-23")]
    public void Items_that_do_not_match_the_trimmed_query_are_hidden_and_the_menu_is_empty_when_none_matches()
    {
        MenuFilterEngine engine = new();
        MenuFilterItem apple = Register(engine, "Apple");
        MenuFilterItem banana = Register(engine, "Banana");

        engine.Recompute(Pass(" AP "));
        Assert.True(engine.HasVisibleSet);
        Assert.True(apple.Visible);
        Assert.False(banana.Visible);
        Assert.False(engine.IsEmpty);

        engine.Recompute(Pass("zz"));
        Assert.False(apple.Visible || banana.Visible);
        Assert.True(engine.IsEmpty);
    }

    [Fact(DisplayName = "port: the first computation waits until an item registers, so arriving items are not hidden and remounted")]
    [Trait("Requirement", "MNU-STA-16")]
    [Trait("Requirement", "MNU-STA-23")]
    public void The_first_computation_waits_until_an_item_registers()
    {
        MenuFilterEngine engine = new();

        Assert.Equal(MenuFilterHighlight.Keep, engine.Recompute(Pass("ap")));
        Assert.False(engine.HasVisibleSet);
        Assert.True(engine.IsEmpty);
        Assert.Equal(0, engine.RegisteredCount);

        MenuFilterItem apple = Register(engine, "Apple");
        MenuFilterItem banana = Register(engine, "Banana");
        Assert.Equal(2, engine.RegisteredCount);
        Assert.False(engine.IsEmpty);

        engine.Recompute(Pass("ap"));
        Assert.True(apple.Visible);
        Assert.False(banana.Visible);
    }

    [Fact(DisplayName = "port: an item whose text has not been reported stays visible until it is, then matches like the others")]
    [Trait("Requirement", "MNU-STA-17")]
    public void An_item_whose_text_has_not_been_reported_stays_visible_until_it_is()
    {
        MenuFilterEngine engine = new();
        MenuFilterItem pending = Register(engine, null);
        _ = Register(engine, "Apple");

        engine.Recompute(Pass("ap"));
        Assert.True(pending.Visible);

        pending.Text = "Banana";
        engine.Recompute(Pass("ap"));
        Assert.False(pending.Visible);
    }

    [Fact(DisplayName = "port: an explicit label wins over the reported text")]
    [Trait("Requirement", "MNU-STA-17")]
    public void An_explicit_label_wins_over_the_reported_text()
    {
        MenuFilterEngine engine = new();
        MenuFilterItem item = Register(engine, "Rendered");
        item.Label = "Labelled";

        engine.Recompute(Pass("label"));
        Assert.True(item.Visible);

        engine.Recompute(Pass("rendered"));
        Assert.False(item.Visible);
    }

    [Fact(DisplayName = "port: an unregistered item leaves the count and the visible set")]
    [Trait("Requirement", "MNU-STA-23")]
    public void An_unregistered_item_leaves_the_count_and_the_visible_set()
    {
        MenuFilterEngine engine = new();
        MenuFilterItem apple = new(static () => { }) { Text = "Apple" };
        IDisposable registration = engine.Register(apple);
        engine.Recompute(Pass("ap"));
        Assert.False(engine.IsEmpty);

        registration.Dispose();
        registration.Dispose();
        Assert.Equal(0, engine.RegisteredCount);
        engine.Recompute(Pass("ap"));
        Assert.True(engine.IsEmpty);
    }

    [Fact(DisplayName = "port: typing with autoHighlight off clears a highlight when the visible set changes, but not on the first computation of an unchanged query")]
    [Trait("Requirement", "MNU-STA-18")]
    public void Typing_with_autoHighlight_off_clears_a_highlight_when_the_visible_set_changes()
    {
        MenuFilterEngine engine = new();
        _ = Register(engine, "Apple");
        _ = Register(engine, "Banana");

        Assert.Equal(MenuFilterHighlight.Keep, engine.Recompute(Pass("a", hasHighlight: true)));
        Assert.Equal(MenuFilterHighlight.Clear, engine.Recompute(Pass("ap", hasHighlight: true)));
        Assert.Equal(MenuFilterHighlight.Keep, engine.Recompute(Pass("app", hasHighlight: true)));
    }

    [Fact(DisplayName = "port: autoHighlight true highlights the first match after typing and clears when the typed query is cleared")]
    [Trait("Requirement", "MNU-STA-19")]
    public void AutoHighlight_true_highlights_the_first_match_after_typing_and_clears_when_the_query_is_cleared()
    {
        MenuFilterEngine engine = new();
        _ = Register(engine, "Apple");
        _ = Register(engine, "Banana");

        Assert.Equal(MenuFilterHighlight.Keep, engine.Recompute(Pass("", auto: true)));
        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("a", auto: true)));
        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("ap", auto: true, hasHighlight: true)));
        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("app", auto: true, hasHighlight: true)));
        Assert.Equal(MenuFilterHighlight.Clear, engine.Recompute(Pass("", auto: true, hasHighlight: true)));
    }

    [Fact(DisplayName = "port: autoHighlight Always highlights the first item with an empty query and returns to it when the query is cleared")]
    [Trait("Requirement", "MNU-STA-19")]
    public void AutoHighlight_always_highlights_the_first_item_with_an_empty_query()
    {
        MenuFilterEngine engine = new();
        _ = Register(engine, "Apple");
        _ = Register(engine, "Banana");

        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("", auto: AutoHighlightValue.Always)));
        Assert.Equal(MenuFilterHighlight.Keep, engine.Recompute(Pass("", auto: AutoHighlightValue.Always, hasHighlight: true)));
        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("b", auto: AutoHighlightValue.Always, hasHighlight: true)));
        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("", auto: AutoHighlightValue.Always, hasHighlight: true)));
    }

    [Fact(DisplayName = "port: the highlight is cleared when no item matches, whatever the automatic highlight")]
    [Trait("Requirement", "MNU-STA-18")]
    public void The_highlight_is_cleared_when_no_item_matches()
    {
        MenuFilterEngine engine = new();
        _ = Register(engine, "Apple");

        Assert.Equal(MenuFilterHighlight.First, engine.Recompute(Pass("a", auto: true)));
        Assert.Equal(MenuFilterHighlight.Clear, engine.Recompute(Pass("zz", auto: true, hasHighlight: true)));
    }

    [Fact(DisplayName = "port: a keystroke notifies only the items whose visibility changed")]
    [Trait("Requirement", "MNU-STA-16")]
    public void A_keystroke_notifies_only_the_items_whose_visibility_changed()
    {
        MenuFilterEngine engine = new();
        int appleRenders = 0;
        int apricotRenders = 0;
        int bananaRenders = 0;
        _ = engine.Register(new MenuFilterItem(() => appleRenders++) { Text = "Apple" });
        _ = engine.Register(new MenuFilterItem(() => apricotRenders++) { Text = "Apricot" });
        _ = engine.Register(new MenuFilterItem(() => bananaRenders++) { Text = "Banana" });

        engine.Recompute(Pass("a"));
        engine.Recompute(Pass("ap"));
        engine.Recompute(Pass("apr"));

        Assert.Equal((1, 0, 1), (appleRenders, apricotRenders, bananaRenders));
    }

    private static MenuFilterItem Register(MenuFilterEngine engine, string? text)
    {
        MenuFilterItem item = new(static () => { }) { Text = text };
        _ = engine.Register(item);
        return item;
    }

    private static MenuFilterPass Pass(string query, AutoHighlightValue? auto = null, bool hasHighlight = false, bool open = true) =>
        new(open, query, s_contains, auto ?? false, hasHighlight);

    private static MenuFilterPass Pass(string query, Func<string, string, bool>? match) =>
        new(true, query, match, false, false);

    private static string Code(MenuFilterHighlight highlight) => highlight.ToString().ToLowerInvariant();

    private static AutoHighlightValue Auto(string code) => code switch
    {
        "true" => true,
        "always" => AutoHighlightValue.Always,
        "unset" => default,
        _ => false,
    };
}
