namespace Blazix.Primitives.Tests;

// FRM-API-02, FFI-API-04: the errors map a consumer gives Form, a string or an array of strings per field name.
/// <summary>The form's errors map and its per-key messages.</summary>
public sealed class FormErrorsTests
{
    [Fact(DisplayName = "accepts a string or an array of strings per key, in insertion order")]
    [Trait("Requirement", "FRM-API-02")]
    public void Accepts_a_string_or_an_array_of_strings_per_key()
    {
        FormErrors errors = new()
        {
            ["url"] = "This is not a valid URL",
            ["tags"] = new[] { "Too many tags", "Duplicate tag" },
        };

        Assert.Equal(["url", "tags"], errors.Keys);
        Assert.Equal(["This is not a valid URL"], errors["url"]);
        Assert.Equal(["Too many tags", "Duplicate tag"], errors["tags"]);
        Assert.Equal(2, errors.Count);
    }

    [Fact(DisplayName = "counts a key as an error only with a non-empty string or a non-empty array")]
    [Trait("Requirement", "FFI-API-04")]
    [Trait("Requirement", "FRM-API-02")]
    public void Counts_a_key_as_an_error_only_with_a_non_empty_string_or_array()
    {
        FormErrors errors = new()
        {
            ["empty"] = string.Empty,
            ["none"] = Array.Empty<string>(),
            ["missing"] = (string?)null,
            ["blank"] = new[] { string.Empty },
            ["set"] = "message",
        };

        Assert.False(errors.HasError("empty"));
        Assert.False(errors.HasError("none"));
        Assert.False(errors.HasError("missing"));
        Assert.True(errors.HasError("blank"));
        Assert.True(errors.HasError("set"));
        Assert.False(errors.HasError("absent"));
    }

    [Fact(DisplayName = "replaces a key's messages and removes a key")]
    [Trait("Requirement", "FRM-API-02")]
    public void Replaces_and_removes_a_key()
    {
        FormErrors errors = new() { ["url"] = "first" };

        errors["url"] = new[] { "second", "third" };
        Assert.Equal(["second", "third"], errors["url"]);
        Assert.True(errors.Remove("url"));
        Assert.False(errors.ContainsKey("url"));
        Assert.False(errors.Remove("url"));
    }

    [Fact(DisplayName = "reads as a read-only dictionary of message lists")]
    [Trait("Requirement", "FRM-API-02")]
    public void Reads_as_a_read_only_dictionary_of_message_lists()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> errors = new FormErrors { ["url"] = "message" };

        Assert.True(errors.TryGetValue("url", out IReadOnlyList<string>? messages));
        Assert.Equal(["message"], messages);
        Assert.Equal(["message"], errors["url"]);
        Assert.Throws<KeyNotFoundException>(() => errors["absent"]);
    }
}
