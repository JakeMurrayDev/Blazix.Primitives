using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// How Razor binds a <see cref="Duration"/>-typed parameter (ADR 05 §4 and its union checklist, Amendments 2026-10-20:
/// <c>U?</c> binds from Razor, including the literal path), on <see cref="AvatarFallback.Delay"/>.
/// </summary>
public sealed class DurationBindingTests
{
    [Fact(DisplayName = "binds a millisecond literal, a TimeSpan, a field of either case and null to a Duration parameter")]
    [Trait("Requirement", "AVA-API-07")]
    public void Binds_a_millisecond_literal_a_TimeSpan_a_field_of_either_case_and_null_to_a_Duration_parameter()
    {
        using AvatarInterop interop = new();
        IRenderedComponent<DurationConsumer> cut = interop.Context.Render<DurationConsumer>();

        string[] bound = [.. cut.FindComponents<AvatarFallback>().Select(fallback => Describe(fallback.Instance.Delay))];

        Assert.Equal(["int 300", "TimeSpan 00:00:00.3000000", "int 0", "int -1", "int 300", "TimeSpan 00:00:01", "null", "null"], bound);
    }

    private static string Describe(Duration? delay)
    {
        if (delay is not { } value)
        {
            return "null";
        }

        if (value.TryGetValue(out int milliseconds))
        {
            return $"int {milliseconds}";
        }

        return value.TryGetValue(out TimeSpan span) ? $"TimeSpan {span:c}" : "none";
    }
}
