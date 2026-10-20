using System.Reflection;
using System.Text.RegularExpressions;
using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Tests.Lints;

/// <summary>
/// The ownership lint (ADR 11 section 5): the <c>Writer</c> column of the component documents against the code's
/// ownership registry. A <c>JS</c> or <c>C# → JS</c> row needs a registry entry for the same family, part and attribute;
/// an entry needs a row that says the same. A family with no folder under <c>src/Blazix.Primitives/</c> is skipped.
/// </summary>
internal static partial class OwnershipLint
{
    [GeneratedRegex("[A-Za-z0-9]+")]
    private static partial Regex Word();

    /// <summary>Compares the documents with the registry.</summary>
    /// <param name="documents">The reader's result for each document, by family (the document stem).</param>
    /// <param name="registry">The registry entries.</param>
    /// <param name="hasFolder">Whether a family has a folder under <c>src/Blazix.Primitives/</c>.</param>
    /// <param name="mirroredFields">The <c>Type.Property</c> of every <c>[MirroredField]</c> state property in the library.</param>
    /// <returns>The families checked and skipped, and one message per mismatch.</returns>
    public static OwnershipReport Check(
        IReadOnlyDictionary<string, DocumentParse> documents,
        IReadOnlyList<RegistryEntry> registry,
        Func<string, bool> hasFolder,
        IReadOnlyCollection<string> mirroredFields)
    {
        List<string> violations = [];
        List<string> checkedFamilies = [];
        List<string> skipped = [];
        int jsRows = 0;

        foreach ((string family, DocumentParse document) in documents.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            violations.AddRange(document.Errors);
            if (!hasFolder(family))
            {
                skipped.Add(family);
                continue;
            }

            checkedFamilies.Add(family);
            foreach (WriterRow row in document.Rows.Where(row => row.Writer != Writer.Csharp))
            {
                jsRows++;
                if (!registry.Any(entry => Covers(entry, row)))
                {
                    violations.Add($"{family}.md:{row.Line} {row.Requirement}: '{row.Attribute}' under '{row.Section}' is {Name(row.Writer)} in the document but there is no registry entry for it (add one to OwnershipRegistry.Declared, or correct the Writer cell)");
                }
            }
        }

        foreach (RegistryEntry entry in registry)
        {
            string label = $"registry entry ({entry.Family}, {entry.Part}, {entry.Attribute}, {Name(entry.Writer)})";
            if (!documents.TryGetValue(entry.Family, out DocumentParse? document))
            {
                violations.Add($"{label}: there is no document docs/components/{entry.Family}.md");
                continue;
            }

            if (!checkedFamilies.Contains(entry.Family, StringComparer.Ordinal))
            {
                continue;
            }

            WriterRow[] rows = [.. document.Rows.Where(row => Covers(entry, row))];
            if (rows.Length == 0)
            {
                violations.Add($"{label}: {entry.Family}.md has no row for that attribute under a heading naming '{entry.Part}'");
            }
            else if (rows.All(row => row.Writer == Writer.Csharp))
            {
                violations.Add($"{label}: {entry.Family}.md:{rows[0].Line} says C#, so JavaScript does not own it");
            }
            else if (!rows.Any(row => row.Writer == entry.Writer))
            {
                violations.Add($"{label}: {entry.Family}.md:{rows[0].Line} says {Name(rows[0].Writer)}");
            }
        }

        string[] named = [.. registry.Select(entry => entry.StateField).OfType<string>()];
        foreach (string field in mirroredFields.Where(field => !named.Contains(field, StringComparer.Ordinal)))
        {
            violations.Add($"mirrored field {field} has no registry entry naming it (set RegistryEntry.StateField)");
        }

        foreach (RegistryEntry entry in registry.Where(entry => entry.StateField is not null && !mirroredFields.Contains(entry.StateField)))
        {
            violations.Add($"registry entry ({entry.Family}, {entry.Part}, {entry.Attribute}) names the state field {entry.StateField}, which is not a [MirroredField] property of a [PartState] record");
        }

        return new OwnershipReport(violations, checkedFamilies, skipped, jsRows);
    }

    /// <summary>Finds the state properties C# keeps for values JavaScript writes.</summary>
    /// <param name="assembly">The library assembly.</param>
    /// <returns>The <c>Type.Property</c> of each <c>[MirroredField]</c> property of a <c>[PartState]</c> record.</returns>
    public static IReadOnlyCollection<string> MirroredFields(Assembly assembly) =>
    [
        .. assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<PartStateAttribute>() is not null)
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<MirroredFieldAttribute>() is not null)
                .Select(property => $"{type.Name}.{property.Name}"))
            .Order(StringComparer.Ordinal),
    ];

    private static bool Covers(RegistryEntry entry, WriterRow row) =>
        entry.Family == row.Family
        && string.Equals(entry.Attribute, row.Attribute, StringComparison.OrdinalIgnoreCase)
        && NamesPart(row.Section, entry.Part);

    // The part's words appear, in order and whole, among the words of the heading: "Indent" is not in "IndentBackground".
    private static bool NamesPart(string heading, string part)
    {
        string[] words = [.. Word().Matches(heading).Select(match => match.Value.ToLowerInvariant())];
        string[] wanted = [.. Word().Matches(part).Select(match => match.Value.ToLowerInvariant())];
        if (wanted.Length == 0)
        {
            return false;
        }

        for (int start = 0; start + wanted.Length <= words.Length; start++)
        {
            if (words.AsSpan(start, wanted.Length).SequenceEqual(wanted))
            {
                return true;
            }
        }

        return false;
    }

    private static string Name(Writer writer) => writer switch
    {
        Writer.Csharp => "C#",
        Writer.Js => "JS",
        _ => "C# → JS",
    };
}
