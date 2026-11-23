using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// How Razor binds a <see cref="MatchValue"/>-typed parameter (ADR 05 §4 and its union checklist, Amendments 2026-10-20:
/// <c>U?</c> binds from Razor, including the literal path), on <see cref="FieldError.Match"/>.
/// </summary>
public sealed class MatchBindingTests : IDisposable
{
    private readonly FieldTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "binds the boolean literals, a flag, a field of either case and null to a MatchValue parameter")]
    [Trait("Requirement", "FLD-API-30")]
    public void Binds_the_boolean_literals_a_flag_a_field_of_either_case_and_null_to_a_MatchValue_parameter()
    {
        IRenderedComponent<MatchConsumer> cut = host.Context.Render<MatchConsumer>();

        string[] bound = [.. cut.FindComponents<FieldError>().Select(error => Describe(error.Instance.Match))];

        Assert.Equal(["bool True", "bool False", "flag ValueMissing", "bool True", "flag TooShort", "null", "null"], bound);
    }

    private static string Describe(MatchValue? match)
    {
        if (match is not { } value)
        {
            return "null";
        }

        if (value.TryGetValue(out bool always))
        {
            return $"bool {always}";
        }

        return value.TryGetValue(out ValidityFlag flag) ? $"flag {flag}" : "none";
    }
}
