namespace Blazix.Primitives.Fixtures.Client.RadioFixtures;

/// <summary>Renders a form's values as text for a data attribute: <c>name=value</c> pairs, <c>null</c> for a null value.</summary>
public static class RadioFormValues
{
    public static string Text(FormValues values) =>
        string.Join(' ', values.Select(pair => $"{pair.Key}={pair.Value?.ToString() ?? "null"}"));
}
