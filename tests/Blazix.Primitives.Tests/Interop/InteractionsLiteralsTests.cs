using Blazix.Primitives.Interop;

namespace Blazix.Primitives.Tests.Interop;

/// <summary>
/// L1 (ADR 06 §1): the literals the <c>interactions</c> module reports, parsed back to <see cref="EventReason"/>
/// (FLT-EVT-01's list of the reasons the module requests with).
/// </summary>
public sealed class InteractionsLiteralsTests
{
    [Theory]
    [InlineData("none", EventReason.None)]
    [InlineData("trigger-press", EventReason.TriggerPress)]
    [InlineData("trigger-hover", EventReason.TriggerHover)]
    [InlineData("trigger-focus", EventReason.TriggerFocus)]
    [InlineData("outside-press", EventReason.OutsidePress)]
    [InlineData("input-press", EventReason.InputPress)]
    [InlineData("focus-out", EventReason.FocusOut)]
    [InlineData("escape-key", EventReason.EscapeKey)]
    public void Every_reason_the_module_reports_parses_to_its_member_and_round_trips(string literal, EventReason reason)
    {
        Assert.Equal(reason, InteractionsLiterals.ParseReason(literal));
        Assert.Equal(literal, reason.ToLiteral());
    }

    [Fact]
    public void A_literal_the_module_never_reports_throws_a_prefixed_message()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => InteractionsLiterals.ParseReason("item-press"));

        Assert.StartsWith("Blazix: ", exception.Message, StringComparison.Ordinal);
    }
}
