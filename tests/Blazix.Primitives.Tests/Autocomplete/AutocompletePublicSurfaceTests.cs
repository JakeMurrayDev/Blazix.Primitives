using System.Reflection;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

// AUT-API-21, STA-01..03, STR-01 (autocomplete/index.ts:1-90, index.parts.ts; autocomplete/item/AutocompleteItemDataAttributes.ts;
// the brief's L1 restatements, M5 track 11): Autocomplete's own records and attribute classes, the AlertDialog precedent, so a
// consumer's ClassValue sees only Autocomplete types. The records carry Combobox's fields less Placeholder and Selected.
/// <summary>Autocomplete's public surface at L1.</summary>
public sealed class AutocompletePublicSurfaceTests
{
    private static readonly string[] s_parts =
    [
        "AutocompleteTrigger", "AutocompleteInputGroup", "AutocompleteInput", "AutocompleteItem`1", "AutocompleteSeparator", "AutocompleteClear",
        "AutocompletePortal", "AutocompleteBackdrop", "AutocompletePositioner", "AutocompletePopup", "AutocompleteArrow", "AutocompleteList`1",
        "AutocompleteCollection`1", "AutocompleteGroup`1", "AutocompleteGroupLabel", "AutocompleteRow", "AutocompleteIcon", "AutocompleteStatus",
        "AutocompleteEmpty",
    ];

    [Fact(DisplayName = "port: the nineteen shells are public and sealed, and each element part's state is an Autocomplete record or the empty state")]
    [Trait("Requirement", "AUT-STR-01")]
    [Trait("Requirement", "AUT-API-21")]
    public void The_shells_are_public_and_sealed_with_their_own_records()
    {
        Assembly assembly = typeof(AutocompleteFilter).Assembly;
        Type[] parts = [.. s_parts.Select(name => assembly.GetType("Blazix.Primitives." + name, throwOnError: true)!)];

        Assert.All(parts, part => Assert.True(part.IsPublic && part.IsSealed, part.Name));
        Assert.All(
            parts.Select(PartStateOf).Where(state => state is not null),
            state => Assert.True(state == typeof(EmptyState) || (state!.IsPublic && state.Name.StartsWith("Autocomplete", StringComparison.Ordinal)), state!.Name));
        Assert.DoesNotContain(
            assembly.GetExportedTypes(),
            type => type.Name is "AutocompleteLabel" or "AutocompleteChips" or "AutocompleteChip" or "AutocompleteChipRemove" or "AutocompleteItemIndicator");
    }

    [Fact(DisplayName = "port: the trigger and input group records have no placeholder, and the item record has no selected field")]
    [Trait("Requirement", "AUT-STA-01")]
    [Trait("Requirement", "AUT-STA-03")]
    [Trait("Requirement", "AUT-API-21")]
    public void The_records_have_no_placeholder_or_selected_field()
    {
        Assert.Equal(
            ["Disabled", "Touched", "Dirty", "Valid", "Filled", "Focused", "Open", "ReadOnly", "PopupSide", "ListEmpty"],
            RecordFields(typeof(AutocompleteTriggerState)));
        Assert.Equal(RecordFields(typeof(AutocompleteTriggerState)), RecordFields(typeof(AutocompleteInputGroupState)));
        Assert.Equal(["Disabled", "Highlighted"], RecordFields(typeof(AutocompleteItemState)));
        Assert.Contains("Placeholder", RecordFields(typeof(ComboboxTriggerState)));
    }

    [Fact(DisplayName = "port: the attribute classes are Autocomplete's, the item's being highlighted and disabled only, and none renders data-placeholder")]
    [Trait("Requirement", "AUT-API-21")]
    [Trait("Requirement", "AUT-DOM-03")]
    public void The_attribute_classes_are_autocompletes()
    {
        Type[] classes =
        [
            typeof(AutocompleteTriggerDataAttributes), typeof(AutocompleteInputDataAttributes), typeof(AutocompleteInputGroupDataAttributes),
            typeof(AutocompleteClearDataAttributes), typeof(AutocompleteBackdropDataAttributes), typeof(AutocompletePositionerCssVars),
            typeof(AutocompletePositionerDataAttributes), typeof(AutocompletePopupDataAttributes), typeof(AutocompleteArrowDataAttributes),
            typeof(AutocompleteItemDataAttributes), typeof(AutocompleteSeparatorDataAttributes),
        ];

        Assert.All(classes, type => Assert.True(type.IsPublic && type.IsAbstract && type.IsSealed, type.Name));
        Assert.Equal(["data-highlighted", "data-disabled"], Constants(typeof(AutocompleteItemDataAttributes)));
        Assert.DoesNotContain("data-placeholder", Constants(typeof(AutocompleteTriggerDataAttributes)));
        Assert.DoesNotContain("data-placeholder", Constants(typeof(AutocompleteInputGroupDataAttributes)));
        Assert.Contains("data-popup-open", Constants(typeof(AutocompleteTriggerDataAttributes)));
        Assert.Equal(Constants(typeof(ComboboxPositionerCssVars)), Constants(typeof(AutocompletePositionerCssVars)));
    }

    [Fact(DisplayName = "port: isItemPress is only emitted by multiple-selection Combobox, so OnValueChange carries plain ChangeEventDetails")]
    [Trait("Requirement", "AUT-API-21")]
    [Trait("Requirement", "AUT-API-03")]
    public void On_value_change_carries_plain_change_event_details()
    {
        Assert.Equal(typeof(EventCallback<ValueChangeEventArgs<string>>), typeof(AutocompleteRoot<int>).GetProperty(nameof(AutocompleteRoot<int>.OnValueChange))!.PropertyType);
        Assert.Equal(typeof(ChangeEventDetails), typeof(ValueChangeEventArgs<string>).GetProperty(nameof(ValueChangeEventArgs<string>.Details))!.PropertyType);
        Assert.Equal(typeof(string), typeof(AutocompleteRoot<int>).GetProperty(nameof(AutocompleteRoot<int>.Value))!.PropertyType);
        Assert.DoesNotContain(
            typeof(AutocompleteRoot<int>).GetProperties().Select(property => property.Name),
            name => name is "Multiple" or "Values" or "DefaultValues" or "OnValuesChange" or "InputValue" or "DefaultInputValue" or "OnInputValueChange"
                or "ItemToStringLabel" or "IsItemEqualToValue" or "AutoComplete");
    }

    [Fact(DisplayName = "port: Autocomplete has no item type parameter for collections, so Items is a plain list of values")]
    [Trait("Requirement", "AUT-API-11")]
    public void Items_is_a_plain_list_of_values()
    {
        Assert.Equal(typeof(IReadOnlyList<Tag>), typeof(AutocompleteRoot<Tag>).GetProperty(nameof(AutocompleteRoot<Tag>.Items))!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<Tag>), typeof(AutocompleteRoot<Tag>).GetProperty(nameof(AutocompleteRoot<Tag>.FilteredItems))!.PropertyType);
        Assert.Single(typeof(AutocompleteRoot<>).GetGenericArguments());
    }

    [Fact(DisplayName = "port: hover can report a mouse event without pointer-specific fields through OnItemHighlighted's details")]
    [Trait("Requirement", "AUT-API-21")]
    [Trait("Requirement", "AUT-API-15")]
    public void Hover_reports_its_event_through_the_highlight_details()
    {
        Assert.Equal(
            typeof(EventCallback<AutocompleteItemHighlightedEventArgs<Tag>>),
            typeof(AutocompleteRoot<Tag>).GetProperty(nameof(AutocompleteRoot<Tag>.OnItemHighlighted))!.PropertyType);
        Assert.Equal(typeof(EventSnapshot), typeof(HighlightEventDetails).GetProperty(nameof(HighlightEventDetails.Event))!.PropertyType);
        AutocompleteItemHighlightedEventArgs<Tag> cleared = new(null, new HighlightEventDetails(EventReason.Pointer, -1));
        Assert.False(cleared.Highlighted);
        Assert.True(new AutocompleteItemHighlightedEventArgs<Tag>(new Tag("t"), new HighlightEventDetails(EventReason.Pointer, 0)).Highlighted);
    }

    private static string[] RecordFields(Type record) =>
        [.. record.GetConstructors().Single(constructor => constructor.GetParameters().Length > 0 && constructor.GetParameters()[0].ParameterType != record)
            .GetParameters().Select(parameter => parameter.Name!)];

    private static string[] Constants(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!)];

    private static Type? PartStateOf(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(PartBase<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private sealed record Tag(string Value);
}
