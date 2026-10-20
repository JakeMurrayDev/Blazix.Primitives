using System.Reflection;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.TestSupport;

namespace Blazix.Primitives.Tests.Lints;

// ADR 11 section 5: the documents' Writer column against the code's ownership registry. A JS or C# → JS row without a
// registry entry fails; a registry entry whose row says C#, disagrees with its row, or has no row fails. A family with
// no folder under src/Blazix.Primitives/ is skipped and counted, so the lint is green while the family set is empty.
public sealed class OwnershipLintTests
{
    private static WriterRow Row(string family, string section, string attribute, Writer writer, int line = 1) =>
        new(family, section, attribute, writer, "TAB-DOM-01", line);

    private static Dictionary<string, DocumentParse> Documents(params WriterRow[] rows) =>
        rows.GroupBy(row => row.Family).ToDictionary(group => group.Key, group => new DocumentParse([.. group], []));

    private static OwnershipReport Check(IEnumerable<WriterRow> rows, IReadOnlyList<RegistryEntry> registry, IReadOnlyCollection<string>? mirrored = null, Func<string, bool>? hasFolder = null) =>
        OwnershipLint.Check(Documents([.. rows]), registry, hasFolder ?? (_ => true), mirrored ?? []);

    [Fact]
    public void A_JS_row_without_a_registry_entry_fails()
    {
        OwnershipReport report = Check([Row("tabs", "5.5 Indicator element", "hidden", Writer.Js, line: 450)], []);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("tabs.md:450", violation);
        Assert.Contains("hidden", violation);
        Assert.Contains("no registry entry", violation);
    }

    [Fact]
    public void A_C_sharp_to_JS_row_without_a_registry_entry_fails()
    {
        OwnershipReport report = Check([Row("combobox", "5.9 Positioner element", "data-side", Writer.CsharpToJs)], []);

        Assert.Single(report.Violations);
    }

    [Fact]
    public void A_C_sharp_row_needs_no_entry()
    {
        OwnershipReport report = Check([Row("tabs", "5.4 Panel element", "hidden", Writer.Csharp)], []);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public void An_entry_that_matches_a_JS_row_by_family_part_and_attribute_passes()
    {
        OwnershipReport report = Check(
            [Row("tabs", "5.5 Indicator element", "hidden", Writer.Js)],
            [new RegistryEntry("tabs", "Indicator", "hidden", Writer.Js)]);

        Assert.Empty(report.Violations);
        Assert.Equal(1, report.JsRows);
    }

    [Fact]
    public void An_entry_for_the_same_attribute_in_another_part_does_not_cover_the_row()
    {
        // Tabs.Panel's hidden is C#; Tabs.Indicator's is JS. The registry names the Indicator only.
        OwnershipReport report = Check(
            [Row("tabs", "5.4 Panel element", "hidden", Writer.Csharp), Row("tabs", "5.5 Indicator element", "hidden", Writer.Js)],
            [new RegistryEntry("tabs", "Indicator", "hidden", Writer.Js)]);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public void A_registry_entry_whose_row_says_C_sharp_fails()
    {
        OwnershipReport report = Check(
            [Row("tabs", "5.4 Panel element", "hidden", Writer.Csharp, line: 421)],
            [new RegistryEntry("tabs", "Panel", "hidden", Writer.Js)]);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("tabs.md:421", violation);
        Assert.Contains("C#", violation);
    }

    [Fact]
    public void A_registry_entry_that_disagrees_with_its_row_on_JS_against_C_sharp_to_JS_fails()
    {
        OwnershipReport report = Check(
            [Row("combobox", "5.9 Positioner element", "data-side", Writer.CsharpToJs)],
            [new RegistryEntry("combobox", "Positioner", "data-side", Writer.Js)]);

        Assert.Single(report.Violations);
    }

    [Fact]
    public void A_registry_entry_with_no_row_in_the_document_fails()
    {
        OwnershipReport report = Check(
            [Row("tabs", "5.5 Indicator element", "hidden", Writer.Js)],
            [new RegistryEntry("tabs", "Indicator", "hidden", Writer.Js), new RegistryEntry("tabs", "Indicator", "data-gone", Writer.Js)]);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("data-gone", violation);
    }

    [Fact]
    public void A_part_name_matches_a_whole_word_of_the_heading_so_Indent_does_not_cover_IndentBackground()
    {
        OwnershipReport report = Check(
            [Row("drawer", "3.2 `Drawer.IndentBackground`", "data-swiping", Writer.Js)],
            [new RegistryEntry("drawer", "Indent", "data-swiping", Writer.Js)]);

        Assert.Equal(2, report.Violations.Count);
    }

    [Fact]
    public void A_part_name_given_as_the_heading_text_matches_a_heading_that_does_not_use_the_part_name()
    {
        OwnershipReport report = Check(
            [Row("checkbox", "5.2 Hidden checkbox input", "indeterminate", Writer.Js)],
            [new RegistryEntry("checkbox", "Hidden checkbox input", "indeterminate", Writer.Js)]);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public void A_family_with_no_folder_is_skipped_and_counted_and_its_rows_and_entries_are_not_checked()
    {
        OwnershipReport report = Check(
            [Row("tabs", "5.5 Indicator element", "hidden", Writer.Js), Row("avatar", "5.2 Image", "hidden", Writer.Js)],
            [new RegistryEntry("tabs", "Indicator", "nothing", Writer.Js)],
            hasFolder: family => family == "avatar");

        Assert.Equal(["avatar"], report.Checked);
        Assert.Equal(["tabs"], report.Skipped);
        Assert.Single(report.Violations);
    }

    [Fact]
    public void An_entry_for_a_family_without_a_document_fails()
    {
        OwnershipReport report = Check([Row("tabs", "5.4 Panel element", "hidden", Writer.Csharp)], [new RegistryEntry("tabz", "Root", "data-x", Writer.Js)]);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("tabz", violation);
    }

    [Fact]
    public void A_parse_error_in_a_document_is_a_violation()
    {
        var documents = new Dictionary<string, DocumentParse> { ["tabs"] = new([], ["tabs.md:5: unrecognised Writer value 'both'"]) };

        OwnershipReport report = OwnershipLint.Check(documents, [], _ => true, []);

        Assert.Contains("both", Assert.Single(report.Violations));
    }

    [Fact]
    public void A_mirrored_field_that_no_entry_names_fails()
    {
        OwnershipReport report = Check([], [], mirrored: ["TabsIndicatorState.Hidden"]);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("TabsIndicatorState.Hidden", violation);
    }

    [Fact]
    public void An_entry_that_names_a_state_field_that_is_not_mirrored_fails()
    {
        OwnershipReport report = Check(
            [Row("tabs", "5.5 Indicator element", "hidden", Writer.Js)],
            [new RegistryEntry("tabs", "Indicator", "hidden", Writer.Js, StateField: "TabsIndicatorState.Hidden")]);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("TabsIndicatorState.Hidden", violation);
    }

    [Fact]
    public void An_entry_that_names_a_mirrored_state_field_passes()
    {
        OwnershipReport report = Check(
            [Row("tabs", "5.5 Indicator element", "hidden", Writer.Js)],
            [new RegistryEntry("tabs", "Indicator", "hidden", Writer.Js, StateField: "TabsIndicatorState.Hidden")],
            mirrored: ["TabsIndicatorState.Hidden"]);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Mirrored_fields_are_the_MirroredField_properties_of_PartState_records()
    {
        IReadOnlyCollection<string> fields = OwnershipLint.MirroredFields(typeof(OwnershipLintTests).Assembly);

        Assert.Equal(["TabsIndicatorState.Hidden"], fields);
    }

    // ---- against the real documents -------------------------------------------------------------------------------

    private static Dictionary<string, DocumentParse> RealDocuments()
    {
        string components = Path.Combine(RepositoryRoot.Find(), "docs", "components");
        return Directory.GetFiles(components, "*.md").ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => WriterColumn.Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)));
    }

    [Fact]
    public void A_deliberately_wrong_entry_is_reported_against_the_real_tabs_document()
    {
        // The Tabs Panel's hidden attribute is rendered by C# (tabs.md, section 5.4); only the Indicator's is JS.
        var wrong = new RegistryEntry("tabs", "Panel", "hidden", Writer.Js);

        OwnershipReport report = OwnershipLint.Check(RealDocuments(), [wrong], family => family == "tabs", []);

        Assert.Contains(report.Violations, violation => violation.Contains("Panel") && violation.Contains("hidden") && violation.Contains("C#"));
    }

    [Fact]
    public void A_family_that_has_a_folder_and_no_registry_entries_is_reported_for_each_JS_row()
    {
        OwnershipReport report = OwnershipLint.Check(RealDocuments(), [], family => family == "tabs", []);

        Assert.Contains(report.Violations, violation => violation.Contains("tabs.md") && violation.Contains("Indicator") && violation.Contains("no registry entry"));
        Assert.True(report.JsRows > 0);
    }

    [Fact]
    public void The_documents_and_the_ownership_registry_agree()
    {
        string src = Path.Combine(RepositoryRoot.Find(), "src", "Blazix.Primitives");
        Dictionary<string, DocumentParse> documents = RealDocuments();

        OwnershipReport report = OwnershipLint.Check(
            documents,
            OwnershipRegistry.Declared,
            family => Directory.Exists(Path.Combine(src, string.Concat(family.Split('-').Select(word => char.ToUpperInvariant(word[0]) + word[1..])))),
            OwnershipLint.MirroredFields(typeof(PartStateAttribute).Assembly));

        TestContext.Current.SendDiagnosticMessage(
            $"ownership lint: {report.Checked.Count} of {documents.Count} families have a folder and were checked ({report.JsRows} JS-owned rows); {report.Skipped.Count} skipped: no folder under src/Blazix.Primitives/ yet");
        Assert.Empty(report.Violations);
        Assert.Equal(documents.Count, report.Checked.Count + report.Skipped.Count);
    }
}

/// <summary>A state record shaped like a part's: one value JavaScript writes, mirrored into the state object.</summary>
[PartState]
public sealed partial record TabsIndicatorState(bool Active, [property: MirroredField] bool Hidden);
