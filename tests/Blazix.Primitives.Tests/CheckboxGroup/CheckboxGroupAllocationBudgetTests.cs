namespace Blazix.Primitives.Tests;

/// <summary>
/// ADR 16 rule 7 for CheckboxGroup at L1: the proposal of one member toggle, which builds the new list a commit produces
/// (checkbox-group §4, CKG-STA-03: a commit produces a new list). Measured on the test thread; the budget was taken from
/// the first measurement and only tightens afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class CheckboxGroupAllocationBudgetTests
{
    // First measurement 2026-10-23 (arm64, .NET 11 RC2, Release): 72 bytes, the new six-entry array and nothing else.
    private const long MemberToggleBudgetBytes = 72;

    [Fact(DisplayName = "a member toggle's proposal stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_member_toggles_proposal_stays_within_its_budget()
    {
        IReadOnlyList<string> value = ["a", "b", "c", "d", "e"];
        for (int i = 0; i < 20; i++)
        {
            _ = CheckboxGroupParent.ProposeMemberToggle(value, "f", check: true);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        IReadOnlyList<string> next = CheckboxGroupParent.ProposeMemberToggle(value, "f", check: true);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(6, next.Count);
        Assert.True(allocated <= MemberToggleBudgetBytes, $"a member toggle allocated {allocated} bytes; the budget is {MemberToggleBudgetBytes}");
    }
}
