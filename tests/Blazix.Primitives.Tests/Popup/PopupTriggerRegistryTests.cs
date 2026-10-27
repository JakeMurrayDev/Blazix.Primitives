// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The trigger registry at L1 (utils/popups/popupTriggerMap.test.ts, titles verbatim; PSH-STA-33, PSH-EDGE-07). An element
/// is the registering part plus its element (settled by the orchestrator 2026-10-27), so a fresh owner object stands for a
/// fresh DOM element. The duplicate-id check throws in every build (design point 6 of M4 track 1, settled by the
/// orchestrator 2026-10-27; ADR 02 Amendments 2026-10-27), so the production title is restated.
/// </summary>
public sealed class PopupTriggerRegistryTests
{
    private const string MultipleIds = "Blazix: A trigger element cannot be registered under multiple IDs in `PopupTriggerMap`.";

    private static PopupTriggerElement CreateButton() => new(new object());

    [Fact(DisplayName = "adds and retrieves elements by id")]
    [Trait("Requirement", "PSH-STA-33")]
    public void Adds_and_retrieves_elements_by_id()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("trigger", button);

        Assert.Equal(button, map.GetById("trigger"));
        Assert.True(map.HasElement(button));
        Assert.True(map.HasMatchingElement(element => element == button));
        Assert.Equal(1, map.Count);
    }

    [Fact(DisplayName = "replaces an existing element when the id is reused")]
    [Trait("Requirement", "PSH-STA-33")]
    public void Replaces_an_existing_element_when_the_id_is_reused()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement first = CreateButton();
        PopupTriggerElement second = CreateButton();

        map.Add("trigger", first);
        map.Add("trigger", second);

        Assert.Equal(second, map.GetById("trigger"));
        Assert.False(map.HasElement(first));
        Assert.True(map.HasElement(second));
        Assert.Equal(1, map.Count);
    }

    [Fact(DisplayName = "deletes elements by id")]
    [Trait("Requirement", "PSH-STA-33")]
    public void Deletes_elements_by_id()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("trigger", button);
        Assert.Equal(1, map.Count);
        map.Delete("trigger");

        Assert.Null(map.GetById("trigger"));
        Assert.False(map.HasElement(button));
        Assert.False(map.HasMatchingElement(element => element == button));
        Assert.Equal(0, map.Count);
    }

    [Fact(DisplayName = "does not duplicate when the same element is added twice with the same id")]
    [Trait("Requirement", "PSH-STA-33")]
    public void Does_not_duplicate_when_the_same_element_is_added_twice_with_the_same_id()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("trigger", button);
        map.Add("trigger", button);

        Assert.Equal(button, map.GetById("trigger"));
        Assert.Equal(1, map.Count);
    }

    [Fact(DisplayName = "throws in non-production when the same element is registered under multiple ids")]
    [Trait("Requirement", "PSH-EDGE-07")]
    public void Throws_in_non_production_when_the_same_element_is_registered_under_multiple_ids()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("first", button);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => map.Add("second", button));
        Assert.Equal(MultipleIds, exception.Message);
    }

    [Fact(DisplayName = "allows re-registering an element under a new id after it was deleted")]
    [Trait("Requirement", "PSH-EDGE-07")]
    public void Allows_re_registering_an_element_under_a_new_id_after_it_was_deleted()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("first", button);
        map.Delete("first");
        map.Add("second", button);

        Assert.Equal(button, map.GetById("second"));
        Assert.Equal(1, map.Count);
    }

    [Fact(DisplayName = "keeps an unrelated element claim when another id is deleted")]
    [Trait("Requirement", "PSH-EDGE-07")]
    public void Keeps_an_unrelated_element_claim_when_another_id_is_deleted()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement first = CreateButton();
        PopupTriggerElement second = CreateButton();

        map.Add("first", first);
        map.Add("second", second);
        map.Delete("second");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => map.Add("other", first));
        Assert.Equal(MultipleIds, exception.Message);
    }

    [Fact(DisplayName = "allows an element evicted by id reuse to register under a new id")]
    [Trait("Requirement", "PSH-EDGE-07")]
    public void Allows_an_element_evicted_by_id_reuse_to_register_under_a_new_id()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement first = CreateButton();
        PopupTriggerElement second = CreateButton();

        map.Add("trigger", first);
        // `first` is no longer registered under `trigger`, so it may claim another id.
        map.Add("trigger", second);
        map.Add("other", first);

        Assert.Equal(first, map.GetById("other"));
        Assert.Equal(second, map.GetById("trigger"));
        Assert.Equal(2, map.Count);
    }

    [Fact(DisplayName = "still throws when a re-added element is registered under a second id")]
    [Trait("Requirement", "PSH-EDGE-07")]
    public void Still_throws_when_a_re_added_element_is_registered_under_a_second_id()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("first", button);
        map.Add("first", button);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => map.Add("second", button));
        Assert.Equal(MultipleIds, exception.Message);
    }

    // Base UI's "does not throw in production when the same element is registered under multiple ids" (jsdom only):
    // restated, because the port throws in every build (design point 6, settled by the orchestrator 2026-10-27). The
    // registration that throws leaves the registry as it was.
    [Fact(DisplayName = "port: throws in every build when the same element is registered under multiple ids, and leaves the first registration")]
    [Trait("Requirement", "PSH-EDGE-07")]
    public void Throws_in_every_build_when_the_same_element_is_registered_under_multiple_ids_and_leaves_the_first_registration()
    {
        PopupTriggerRegistry map = new();
        PopupTriggerElement button = CreateButton();

        map.Add("first", button);
        Assert.Throws<InvalidOperationException>(() => map.Add("second", button));

        Assert.Equal(button, map.GetById("first"));
        Assert.Null(map.GetById("second"));
        Assert.True(map.HasElement(button));
        Assert.Equal(1, map.Count);
    }

    [Fact(DisplayName = "port: two registrations are the same element only when the same part registers the same element reference")]
    [Trait("Requirement", "PSH-STA-27")]
    public void Two_registrations_are_the_same_element_only_when_the_same_part_registers_the_same_element_reference()
    {
        object part = new();
        Microsoft.AspNetCore.Components.ElementReference rendered = new("element-1");

        Assert.Equal(new PopupTriggerElement(part, rendered), new PopupTriggerElement(part, new Microsoft.AspNetCore.Components.ElementReference("element-1")));
        Assert.NotEqual(new PopupTriggerElement(part, rendered), new PopupTriggerElement(part));
        Assert.NotEqual(new PopupTriggerElement(part, rendered), new PopupTriggerElement(new object(), rendered));
        Assert.Equal(new PopupTriggerElement(part), new PopupTriggerElement(part));
    }
}
