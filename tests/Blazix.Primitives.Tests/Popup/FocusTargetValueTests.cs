using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="FocusTargetValue"/> at L1: the value form of <c>initialFocus</c> and <c>finalFocus</c> (PSH-FOC-02/04;
/// ADR 08 Amendments 2026-10-27, written by track 1): a boolean, an element, or the id of an element, that allocates
/// nothing (ADR 16 rule 7). Its resolution against the interaction type is each family's.
/// </summary>
public sealed class FocusTargetValueTests
{
    [Fact(DisplayName = "holds a boolean, an element or an element id and reads each case without the others")]
    public void Holds_a_boolean_an_element_or_an_element_id_and_reads_each_case_without_the_others()
    {
        FocusTargetValue flag = false;
        FocusTargetValue element = new ElementReference("element-2");
        FocusTargetValue id = "close-button";

        Assert.True(flag.TryGetValue(out bool value));
        Assert.False(value);
        Assert.False(flag.TryGetValue(out string? _));
        Assert.True(element.TryGetValue(out ElementReference held));
        Assert.Equal("element-2", held.Id);
        Assert.False(element.TryGetValue(out bool _));
        Assert.True(id.TryGetValue(out string? elementId));
        Assert.Equal("close-button", elementId);
        Assert.False(id.TryGetValue(out ElementReference _));
        Assert.All([flag, element, id], target => Assert.True(target.HasValue));
        Assert.False(default(FocusTargetValue).HasValue);
    }

    [Fact(DisplayName = "building, converting and reading a focus target allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_converting_and_reading_a_focus_target_allocates_nothing()
    {
        ElementReference reference = new("element-3");
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            FocusTargetValue fromBool = true;
            FocusTargetValue fromElement = reference;
            FocusTargetValue fromId = "id";
            FocusTargetValue? nullable = fromBool;
            bool read = fromBool.TryGetValue(out bool focus) & fromElement.TryGetValue(out ElementReference _) & fromId.HasValue & nullable.HasValue;
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(read);
            Assert.True(focus);
        }

        Assert.Equal(0, total);
    }
}
