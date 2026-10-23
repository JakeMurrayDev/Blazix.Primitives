using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioGroup.spec.tsx and RadioRoot.spec.tsx (RDG §3.2, RAD §3.3): RadioGroupTypeInference.razor must compile; this test
// renders it and reads the type arguments the Razor compiler inferred.
/// <summary>The type parameter of the radio parts, as a Razor consumer infers it.</summary>
public sealed class RadioGroupTypeTests : IDisposable
{
    private readonly RadioTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: TValue is inferred from Value, DefaultValue, a nullable value, @bind-Value or the cascade, or given explicitly")]
    [Trait("Requirement", "RAD-API-01")]
    [Trait("Requirement", "RDG-API-01")]
    [Trait("Requirement", "RDG-API-02")]
    public void Port_TValue_is_inferred()
    {
        IRenderedComponent<RadioGroupTypeInference> cut = test.Context.Render<RadioGroupTypeInference>();

        Assert.Equal(2, cut.FindComponents<RadioGroup<string>>().Count);
        Assert.Equal(2, cut.FindComponents<RadioGroup<RadioGroupTypeInference.Fruit>>().Count);
        Assert.Single(cut.FindComponents<RadioGroup<int?>>());
        Assert.Equal(5, cut.FindComponents<RadioRoot<string>>().Count);
        Assert.Equal(2, cut.FindComponents<RadioRoot<RadioGroupTypeInference.Fruit>>().Count);
        Assert.Equal(2, cut.FindComponents<RadioRoot<int?>>().Count);
        Assert.Single(cut.FindComponents<RadioRoot<int>>());
    }

    // Design point R7 (pending the user): a radio whose TValue differs from its group's cannot join it, which would leave it
    // static and never selectable; the mismatch is an error in every build.
    [Fact(DisplayName = "port: a radio whose TValue differs from its group's throws a descriptive error")]
    [Trait("Requirement", "RAD-STR-03")]
    public void Port_a_radio_whose_TValue_differs_from_its_groups_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<RadioGroup<int?>>(p => p
            .AddChildContent<RadioRoot<int>>(r => r.Add(x => x.Value, 1))));

        Assert.Equal(
            "Blazix: A `RadioRoot` must have the `TValue` of its `RadioGroup`. Set `TValue` on the radio button, or give `Value` the group's type, for example `Value=\"(int?)1\"` in a `RadioGroup` of `int?`.",
            error.Message);
    }
}
