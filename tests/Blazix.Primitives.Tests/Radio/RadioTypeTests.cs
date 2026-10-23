using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Radio;

// RadioRoot.spec.tsx (RAD-API-01, RAD §3.3): the radio is generic over its value, so an explicit type argument constrains
// Value at compile time (`<Radio.Root<'a' | 'b'> value="c" />` is the C# compiler's error); the two state records have
// exactly the documented fields (RAD-STA-03/04); the constant classes hold Base UI's names in its order (RAD §3.3).
/// <summary>The public shape of the Radio family.</summary>
public sealed class RadioTypeTests
{
    [Fact(DisplayName = "port: RadioRoot is generic over its value, and Value is that type")]
    [Trait("Requirement", "RAD-API-01")]
    public void Port_RadioRoot_is_generic_over_its_value()
    {
        // Each closed type's Value has the type argument's type, so a value of another type does not compile.
        Assert.Equal(typeof(string), ValueType<RadioRoot<string>>());
        Assert.Equal(typeof(int), ValueType<RadioRoot<int>>());
        Assert.Equal(typeof(int?), ValueType<RadioRoot<int?>>());
        Assert.Equal(typeof(Fruit), ValueType<RadioRoot<Fruit>>());
        Assert.NotNull(typeof(RadioRoot<>).GetProperty(nameof(RadioRoot<string>.Value))!.GetCustomAttribute<EditorRequiredAttribute>());
        Assert.True(typeof(RadioIndicator).IsSealed);
        Assert.False(typeof(RadioIndicator).IsGenericType);
    }

    [Fact(DisplayName = "port: the state records have exactly the documented fields")]
    [Trait("Requirement", "RAD-STA-03")]
    [Trait("Requirement", "RAD-STA-04")]
    public void Port_the_state_records_have_exactly_the_documented_fields()
    {
        Assert.Equal(
            ["Checked", "Disabled", "ReadOnly", "Required", "Touched", "Dirty", "Valid", "Filled", "Focused"],
            typeof(RadioRootState).GetConstructors().Single().GetParameters().Select(p => p.Name));
        Assert.Equal(
            ["Checked", "Disabled", "ReadOnly", "Required", "Touched", "Dirty", "Valid", "Filled", "Focused", "TransitionStatus"],
            typeof(RadioIndicatorState).GetConstructors().Single().GetParameters().Select(p => p.Name));
        Assert.Equal(typeof(bool?), typeof(RadioRootState).GetProperty(nameof(RadioRootState.Valid))!.PropertyType);
        Assert.Equal(typeof(TransitionStatus?), typeof(RadioIndicatorState).GetProperty(nameof(RadioIndicatorState.TransitionStatus))!.PropertyType);
    }

    [Fact(DisplayName = "port: the data attribute constants are Base UI's, in its order")]
    [Trait("Requirement", "RAD-DOM-03")]
    [Trait("Requirement", "RAD-DOM-15")]
    public void Port_the_data_attribute_constants_are_Base_UIs()
    {
        Assert.Equal(
            ["data-checked", "data-unchecked", "data-disabled", "data-readonly", "data-required", "data-valid", "data-invalid", "data-touched", "data-dirty", "data-filled", "data-focused"],
            Constants(typeof(RadioRootDataAttributes)));
        Assert.Equal(
            ["data-checked", "data-unchecked", "data-disabled", "data-readonly", "data-required", "data-starting-style", "data-ending-style", "data-valid", "data-invalid", "data-touched", "data-dirty", "data-filled", "data-focused"],
            Constants(typeof(RadioIndicatorDataAttributes)));

        // radio §16 Q3: the composite marker is a root attribute, not a listed constant.
        Assert.DoesNotContain("data-composite-item-active", Constants(typeof(RadioRootDataAttributes)));
    }

    private static Type ValueType<TPart>() => typeof(TPart).GetProperty("Value")!.PropertyType;

    private static string[] Constants(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).OrderBy(f => f.MetadataToken).Select(f => (string)f.GetRawConstantValue()!)];

    private enum Fruit
    {
        Apple,
    }
}
