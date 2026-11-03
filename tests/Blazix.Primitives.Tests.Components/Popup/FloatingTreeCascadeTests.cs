using System.Text.Json;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The cross-family floating tree at L2 (ADR 04 Amendments 2026-11-03 (5), chosen by the user 2026-11-03; M5 track 2, brief
/// deliverable 3): a popup Root of one family inside another family's open popup names the enclosing popup's session as
/// its parent, read from the one family-agnostic cascade. Tooltip neither reads nor cascades it (ADR 04 Amendments
/// 2026-11-04, design point 2), so a popup inside a tooltip parents to the popup around the tooltip.
/// </summary>
public sealed class FloatingTreeCascadeTests : IDisposable
{
    private readonly PreviewCardTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Theory(DisplayName = "port: a popup root of one family inside another family's popup names that popup's session as its parent")]
    [InlineData("popover-in-dialog")]
    [InlineData("dialog-in-popover")]
    [InlineData("preview-card-in-alert-dialog")]
    [InlineData("alert-dialog-in-preview-card")]
    [Trait("Requirement", "FLT-DIS-08")]
    [Trait("Requirement", "FLT-KEY-02")]
    [Trait("Requirement", "POP-STR-02")]
    [Trait("Requirement", "PRV-STR-10")]
    public void A_popup_root_of_one_family_inside_another_familys_popup_names_that_popups_session_as_its_parent(string nesting)
    {
        host.Context.Render<FloatingTreeCascadeHarness>(p => p.Add(x => x.Case, nesting));

        IReadOnlyList<JsonElement> sessions = host.SessionAttaches;
        Assert.Equal(2, sessions.Count);
        Assert.False(sessions[0].GetProperty("session").TryGetProperty("parentToken", out _));
        Assert.Equal(sessions[0].GetProperty("sessionToken").GetString(), sessions[1].GetProperty("session").GetProperty("parentToken").GetString());
    }

    [Fact(DisplayName = "port: a dialog inside a tooltip's popup inside a popover names the popover's session as its parent, and the tooltip's session names none")]
    [Trait("Requirement", "TIP-DIS-08")]
    [Trait("Requirement", "FLT-DIS-08")]
    public void A_dialog_inside_a_tooltips_popup_inside_a_popover_names_the_popovers_session_as_its_parent()
    {
        host.Context.Render<FloatingTreeCascadeHarness>(p => p.Add(x => x.Case, "dialog-in-tooltip-in-popover"));

        IReadOnlyList<JsonElement> sessions = host.SessionAttaches;
        Assert.Equal(3, sessions.Count);
        string popover = sessions[0].GetProperty("sessionToken").GetString()!;
        Assert.Equal(popover, sessions[2].GetProperty("session").GetProperty("parentToken").GetString());
        Assert.False(sessions[1].GetProperty("session").TryGetProperty("parentToken", out _));
    }
}
