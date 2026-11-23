using System.Text;
using System.Text.RegularExpressions;

namespace Blazix.Primitives.Tests.Lints;

/// <summary>
/// Reads the <c>Writer</c> column of a component document: every table whose last column is headed <c>Writer</c>
/// (docs/conventions.md section 3.4) yields one <see cref="WriterRow"/> per attribute, data attribute or variable its
/// attribute cell names.
/// </summary>
internal static partial class WriterColumn
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpan();

    // A name is a data attribute, an ARIA attribute, a CSS variable or a property: it ends at the first character that
    // cannot be part of one (`data-side="<side>"` names data-side, `--nested-dialogs: <n>` names --nested-dialogs).
    [GeneratedRegex("^(-{0,2}[A-Za-z][A-Za-z0-9-]*)")]
    private static partial Regex Name();

    [GeneratedRegex(@"\b[A-Z]{2,4}-[A-Z0-9]+-\d+\b")]
    private static partial Regex RequirementId();

    /// <summary>Reads one document.</summary>
    /// <param name="family">The document stem, for example <c>tabs</c>.</param>
    /// <param name="markdown">The document text.</param>
    /// <returns>The rows of every <c>Writer</c> table, and what could not be read.</returns>
    public static DocumentParse Parse(string family, string markdown)
    {
        List<WriterRow> rows = [];
        List<string> errors = [];
        string[] lines = markdown.Split('\n');
        string section = string.Empty;
        bool inFence = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            Match heading = Heading().Match(line);
            if (heading.Success)
            {
                // A sub-heading of a table ("Data attributes", "CSS variables") keeps the part's numbered heading.
                if (heading.Groups[1].Length <= 3)
                {
                    section = heading.Groups[2].Value;
                }

                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            List<string> header = SplitRow(line);
            int next = i + 1;
            if (header.Count == 0 || header[^1].Trim() != "Writer")
            {
                while (next < lines.Length && lines[next].StartsWith('|'))
                {
                    next++;
                }

                i = next - 1;
                continue;
            }

            int attributeColumn = AttributeColumn(header);
            for (next = i + 2; next < lines.Length && lines[next].StartsWith('|'); next++)
            {
                ReadRow(family, section, lines[next].TrimEnd('\r'), next + 1, attributeColumn, rows, errors);
            }

            i = next - 1;
        }

        return new DocumentParse(rows, errors);
    }

    private static void ReadRow(string family, string section, string line, int lineNumber, int attributeColumn, List<WriterRow> rows, List<string> errors)
    {
        List<string> cells = SplitRow(line);
        if (cells.Count < 2)
        {
            return;
        }

        string writerCell = cells[^1].Trim();
        List<(Writer Writer, string Note)>? writers = ReadWriters(writerCell);
        if (writers is null)
        {
            errors.Add($"{family}.md:{lineNumber}: unrecognised Writer value '{writerCell}' (expected C#, JS or C# → JS, optionally followed by a note)");
            return;
        }

        string requirement = RequirementId().Match(string.Join(' ', cells)).Value;
        List<string> names = NamesIn(cells[Math.Min(attributeColumn, cells.Count - 2)]);
        if (names.Count == 0)
        {
            names = NamesIn(cells[0]);
        }

        foreach (string name in names)
        {
            rows.Add(new WriterRow(family, section, name, WriterOf(name, writers), requirement, lineNumber));
        }
    }

    // The cell is C#, JS or C# → JS, optionally followed by a note ("C# → JS (convergent write at load)"). A row that
    // mixes writers says so in segments: "C# (boolean); JS (`"until-found"`)".
    private static List<(Writer Writer, string Note)>? ReadWriters(string cell)
    {
        List<(Writer, string)> writers = [];
        foreach (string segment in SplitSegments(cell))
        {
            string text = segment.Trim();
            (Writer Writer, string Prefix)? kind = text switch
            {
                _ when StartsWithToken(text, "C# → JS") => (Writer.CsharpToJs, "C# → JS"),
                _ when StartsWithToken(text, "C# -> JS") => (Writer.CsharpToJs, "C# -> JS"),
                _ when StartsWithToken(text, "C#") => (Writer.Csharp, "C#"),
                _ when StartsWithToken(text, "JS") => (Writer.Js, "JS"),
                _ => null,
            };
            if (kind is null)
            {
                return null;
            }

            writers.Add((kind.Value.Writer, text[kind.Value.Prefix.Length..]));
        }

        return writers.Count == 0 ? null : writers;
    }

    private static bool StartsWithToken(string text, string token) =>
        text.StartsWith(token, StringComparison.Ordinal) && (text.Length == token.Length || !char.IsLetterOrDigit(text[token.Length]));

    private static IEnumerable<string> SplitSegments(string cell)
    {
        StringBuilder segment = new();
        bool inCode = false;
        foreach (char c in cell)
        {
            if (c == '`')
            {
                inCode = !inCode;
            }

            if (c == ';' && !inCode)
            {
                yield return segment.ToString();
                segment.Clear();
            }
            else
            {
                segment.Append(c);
            }
        }

        yield return segment.ToString();
    }

    // With one writer, it is the answer. With several, the segment whose note names the attribute decides; an attribute
    // no note names (or several do) is treated as the most JavaScript-owned of them: JS, then C# → JS, then C#.
    private static Writer WriterOf(string name, List<(Writer Writer, string Note)> writers)
    {
        if (writers.Count == 1)
        {
            return writers[0].Writer;
        }

        Writer[] mentioning = [.. writers.Where(writer => writer.Note.Contains(name, StringComparison.OrdinalIgnoreCase)).Select(writer => writer.Writer).Distinct()];
        Writer[] candidates = mentioning.Length > 0 ? mentioning : [.. writers.Select(writer => writer.Writer)];
        return candidates.Contains(Writer.Js) ? Writer.Js : candidates.Contains(Writer.CsharpToJs) ? Writer.CsharpToJs : Writer.Csharp;
    }

    // "Attribute", "Attribute / style", "Style / attribute", "Variable": the column that names the attribute. Tables that
    // lead with an Element or Status column name theirs in a later one.
    private static int AttributeColumn(List<string> header)
    {
        for (int i = 0; i < header.Count; i++)
        {
            string cell = header[i].Trim().Trim('`').ToLowerInvariant();
            if (cell.StartsWith("attribute", StringComparison.Ordinal) || cell.StartsWith("style", StringComparison.Ordinal) || cell.StartsWith("variable", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    private static List<string> NamesIn(string cell)
    {
        List<string> names = [];
        foreach (Match span in CodeSpan().Matches(cell))
        {
            Match name = Name().Match(span.Groups[1].Value.Trim());

            // "`--drawer-swipe-movement-x` / `-y`": a single leading dash is a shorthand for the previous name, not a name.
            if (name.Success && !(name.Value.StartsWith('-') && !name.Value.StartsWith("--", StringComparison.Ordinal)))
            {
                names.Add(name.Value);
            }
        }

        if (names.Count == 0)
        {
            string text = cell.Replace("`", string.Empty, StringComparison.Ordinal).Trim();
            if (text.Length > 0)
            {
                names.Add(text);
            }
        }

        return names;
    }

    // Cells end at a pipe that is neither escaped (\|) nor inside a code span: the documents hold both forms.
    private static List<string> SplitRow(string line)
    {
        string body = line.Trim();
        if (body.StartsWith('|'))
        {
            body = body[1..];
        }

        List<string> cells = [];
        StringBuilder cell = new();
        bool inCode = false;
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '\\' && i + 1 < body.Length && body[i + 1] == '|')
            {
                cell.Append('|');
                i++;
            }
            else if (c == '`')
            {
                inCode = !inCode;
                cell.Append(c);
            }
            else if (c == '|' && !inCode)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.ToString().Trim().Length > 0)
        {
            cells.Add(cell.ToString());
        }

        return cells;
    }
}
