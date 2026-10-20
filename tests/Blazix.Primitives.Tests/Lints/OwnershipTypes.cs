namespace Blazix.Primitives.Tests.Lints;

/// <summary>The value of a <c>Writer</c> cell in a component document.</summary>
internal enum Writer
{
    /// <summary>Rendered by the part.</summary>
    Csharp,

    /// <summary>Written by a JavaScript module; C# mirrors the value into the state object.</summary>
    Js,

    /// <summary>Rendered once by C#, owned by JavaScript from attach.</summary>
    CsharpToJs,
}

/// <summary>One attribute, data attribute or CSS variable named in a row of a <c>Writer</c> table.</summary>
/// <param name="Family">The document stem, for example <c>tabs</c>.</param>
/// <param name="Section">The heading of the section the table sits in.</param>
/// <param name="Attribute">The attribute, data attribute or variable name, or the cell text when the cell names none.</param>
/// <param name="Writer">The cell's value.</param>
/// <param name="Requirement">The requirement ID in the row, or an empty string.</param>
/// <param name="Line">The 1-based line of the row.</param>
internal sealed record WriterRow(string Family, string Section, string Attribute, Writer Writer, string Requirement, int Line);

/// <summary>The rows of a document and the problems found reading its tables.</summary>
/// <param name="Rows">Every attribute named in a table whose last column is <c>Writer</c>.</param>
/// <param name="Errors">A <c>Writer</c> cell this reader does not recognise, and the like.</param>
internal sealed record DocumentParse(IReadOnlyList<WriterRow> Rows, IReadOnlyList<string> Errors);

/// <summary>One line of the ownership registry: an attribute JavaScript writes, in one part.</summary>
/// <param name="Family">The document stem, for example <c>tabs</c>.</param>
/// <param name="Part">The part, as a document heading names it (<c>Indicator</c>, or free text such as <c>Hidden checkbox input</c>).</param>
/// <param name="Attribute">The attribute, data attribute or variable name.</param>
/// <param name="Writer">Who writes it; never <see cref="Lints.Writer.Csharp"/>.</param>
/// <param name="StateField">For a mirrored value, the <c>Type.Property</c> of the <c>[MirroredField]</c> state property that holds it.</param>
internal sealed record RegistryEntry(string Family, string Part, string Attribute, Writer Writer, string? StateField = null);

/// <summary>What one run of the ownership lint saw.</summary>
/// <param name="Violations">One message per mismatch.</param>
/// <param name="Checked">The families that have a folder under <c>src/Blazix.Primitives/</c>.</param>
/// <param name="Skipped">The families that have none yet.</param>
/// <param name="JsRows">The number of <c>JS</c> and <c>C# → JS</c> rows in the checked families.</param>
internal sealed record OwnershipReport(IReadOnlyList<string> Violations, IReadOnlyList<string> Checked, IReadOnlyList<string> Skipped, int JsRows);
