using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The items' flags (M3 track 1, E3; ADR 04 Amendments 2026-10-26, chosen by the user): each item's registration carries
/// its <c>disabled</c> and <c>focusableWhenDisabled</c> flags, set by its part on every parameter set, and the root's
/// <c>DeriveDisabledIndices</c> asks the module to derive the indices navigation skips from them. JavaScript derives the
/// indices (ADR 01 decision 2); C# only carries the flags, and a change sends one <c>update</c>.
/// </summary>
public sealed class CompositeItemFlagsTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "the items' flags travel in their registrations, and one flag change sends one update")]
    [Trait("Requirement", "CMP-STA-08")]
    [Trait("Requirement", "CMP-STA-29")]
    [Trait("Requirement", "CMP-STA-10")]
    public void The_items_flags_travel_and_one_flag_change_sends_one_update()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.DeriveDisabledIndices, true)
            .AddChildContent(Items(aFocusable: false)));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;
        Assert.True(CompositeTestContext.Parse(attached).GetProperty("deriveDisabledIndices").GetBoolean());
        Assert.Contains($$"""
            "items":[{"id":"{{ids[0]}}","disabled":true},{"id":"{{ids[1]}}"}]
            """.Trim(), attached);

        cut.Render(p => p
            .Add(x => x.DeriveDisabledIndices, true)
            .AddChildContent(Items(aFocusable: true)));

        string updated = Assert.Single(host.UpdateOptions);
        Assert.Contains($$"""
            "items":[{"id":"{{ids[0]}}","disabled":true,"focusableWhenDisabled":true},{"id":"{{ids[1]}}"}]
            """.Trim(), updated);

        cut.Render(p => p
            .Add(x => x.DeriveDisabledIndices, true)
            .AddChildContent(Items(aFocusable: true)));
        Assert.Single(host.UpdateOptions);
    }

    private static RenderFragment Items(bool aFocusable) => builder =>
    {
        builder.OpenComponent<TestCompositeItem>(0);
        builder.SetKey("a");
        builder.AddAttribute(1, "data-item", "a");
        builder.AddAttribute(2, nameof(TestCompositeItem.Disabled), true);
        builder.AddAttribute(3, nameof(TestCompositeItem.FocusableWhenDisabled), aFocusable);
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(4);
        builder.SetKey("b");
        builder.AddAttribute(5, "data-item", "b");
        builder.CloseComponent();
    };
}
