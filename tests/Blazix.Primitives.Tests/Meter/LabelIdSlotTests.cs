using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// FFI-ARIA-08 and MTR-STA-03 (meter/label/MeterLabel.tsx through useRegisteredLabelId): a label publishes its id to its scope
// and withdraws it only while the scope still holds that same id, so an older label's cleanup never clears a newer one's.
/// <summary>The label-id slot a root holds for its label (the registration pattern shared by Meter, Progress and Field).</summary>
public sealed class LabelIdSlotTests
{
    [Fact(DisplayName = "holds no id until a label publishes one")]
    [Trait("Requirement", "MTR-STA-03")]
    public void Holds_no_id_until_a_label_publishes_one()
    {
        LabelIdSlot slot = new();

        Assert.Null(slot.Id);
    }

    [Fact(DisplayName = "tracks the id a label publishes and notifies the owner")]
    [Trait("Requirement", "MTR-STA-03")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Tracks_the_id_a_label_publishes_and_notifies_the_owner()
    {
        int changes = 0;
        LabelIdSlot slot = new() { Changed = () => changes++ };

        slot.Publish("label-a");

        Assert.Equal("label-a", slot.Id);
        Assert.Equal(1, changes);
    }

    [Fact(DisplayName = "does not notify when the published id is the tracked one")]
    [Trait("Requirement", "MTR-STA-03")]
    public void Does_not_notify_when_the_published_id_is_the_tracked_one()
    {
        int changes = 0;
        LabelIdSlot slot = new() { Changed = () => changes++ };
        slot.Publish("label-a");

        slot.Publish("label-a");

        Assert.Equal(1, changes);
    }

    [Fact(DisplayName = "replaces the tracked id when a label changes its id")]
    [Trait("Requirement", "MTR-STA-03")]
    public void Replaces_the_tracked_id_when_a_label_changes_its_id()
    {
        LabelIdSlot slot = new();
        slot.Publish("label-a");

        slot.Publish("label-b");

        Assert.Equal("label-b", slot.Id);
    }

    [Fact(DisplayName = "withdraws the tracked id and notifies the owner")]
    [Trait("Requirement", "MTR-STA-03")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Withdraws_the_tracked_id_and_notifies_the_owner()
    {
        int changes = 0;
        LabelIdSlot slot = new() { Changed = () => changes++ };
        slot.Publish("label-a");

        slot.Withdraw("label-a");

        Assert.Null(slot.Id);
        Assert.Equal(2, changes);
    }

    // The older label's cleanup must not clear the newer label's id (FFI-ARIA-08; MTR §16 item 9).
    [Fact(DisplayName = "withdraws an id only while it is still the tracked one")]
    [Trait("Requirement", "MTR-STA-03")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Withdraws_an_id_only_while_it_is_still_the_tracked_one()
    {
        int changes = 0;
        LabelIdSlot slot = new() { Changed = () => changes++ };
        slot.Publish("older");
        slot.Publish("newer");

        slot.Withdraw("older");

        Assert.Equal("newer", slot.Id);
        Assert.Equal(2, changes);
    }

    [Fact(DisplayName = "keeps an empty id as published")]
    [Trait("Requirement", "MTR-STA-03")]
    public void Keeps_an_empty_id_as_published()
    {
        // MTR §16 item 8: `<Meter.Label id="">` is used verbatim and published.
        LabelIdSlot slot = new();

        slot.Publish(string.Empty);

        Assert.Equal(string.Empty, slot.Id);
    }
}
