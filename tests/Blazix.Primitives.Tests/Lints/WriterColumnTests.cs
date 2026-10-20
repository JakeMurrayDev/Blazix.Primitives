namespace Blazix.Primitives.Tests.Lints;

// The reader of the Writer column (docs/conventions.md section 3.4): every attribute table of a component document ends
// in a Writer column holding C#, JS or C# → JS. The inputs are shaped like the tables of docs/components.
public sealed class WriterColumnTests
{
    private const string Tabs = """
        # Tabs

        ## 5. Attributes

        ### 5.4 Panel element

        | Attribute | Value / present when | Req | Writer |
        |---|---|---|---|
        | `hidden` | `!mounted` (boolean attribute) | TAB-DOM-14 | C# |
        | `data-starting-style` / `data-ending-style` | transition status `'starting'` / `'ending'` | TAB-DOM-18 | JS |

        - **TAB-DOM-14** — MUST. Text.

        ### 5.5 Indicator element

        | Attribute | Value / present when | Req | Writer |
        |---|---|---|---|
        | `data-side="<side>"`, `data-align="<align>"` | rendered placement | TAB-DOM-15 | C# → JS |
        | inline `--nested-dialogs: <n>` | always | TAB-DOM-19 | C# → JS |
        | `style` | the six `--active-tab-*` variables | TAB-DOM-24 | JS |
        """;

    [Fact]
    public void Reads_each_row_with_its_section_attribute_writer_requirement_and_line()
    {
        DocumentParse document = WriterColumn.Parse("tabs", Tabs);

        Assert.Empty(document.Errors);
        WriterRow first = document.Rows[0];
        Assert.Equal(("tabs", "5.4 Panel element", "hidden", Writer.Csharp, "TAB-DOM-14", 9), (first.Family, first.Section, first.Attribute, first.Writer, first.Requirement, first.Line));
    }

    [Fact]
    public void Splits_a_cell_that_names_several_attributes_into_one_row_each()
    {
        DocumentParse document = WriterColumn.Parse("tabs", Tabs);

        Assert.Equal(
            ["hidden", "data-starting-style", "data-ending-style", "data-side", "data-align", "--nested-dialogs", "style"],
            document.Rows.Select(row => row.Attribute));
    }

    [Fact]
    public void Reads_the_three_writer_values_and_the_section_of_each_table()
    {
        DocumentParse document = WriterColumn.Parse("tabs", Tabs);

        Assert.Equal(
            [Writer.Csharp, Writer.Js, Writer.Js, Writer.CsharpToJs, Writer.CsharpToJs, Writer.CsharpToJs, Writer.Js],
            document.Rows.Select(row => row.Writer));
        Assert.Equal(["5.4 Panel element", "5.4 Panel element", "5.4 Panel element", "5.5 Indicator element", "5.5 Indicator element", "5.5 Indicator element", "5.5 Indicator element"],
            document.Rows.Select(row => row.Section));
    }

    [Fact]
    public void Reads_the_attribute_from_the_Attribute_column_when_another_column_comes_first()
    {
        const string slider = """
            ### 5.2 Thumb

            | Element | Attribute | Value | Req | Writer |
            |---|---|---|---|---|
            | Thumb | `--position` | percent | SLD-DOM-03 | JS |
            """;

        DocumentParse document = WriterColumn.Parse("slider", slider);

        Assert.Equal("--position", Assert.Single(document.Rows).Attribute);
    }

    [Fact]
    public void Keeps_the_section_of_the_numbered_heading_across_the_subheadings_of_a_table()
    {
        const string dialog = """
            ### 3.2 `Drawer.Popup`

            #### Data attributes

            | Attribute | Present when | Req | Writer |
            |---|---|---|---|
            | `data-swiping` | `swiping` | DRW-DOM-26 | JS |
            """;

        DocumentParse document = WriterColumn.Parse("drawer", dialog);

        Assert.Equal("3.2 `Drawer.Popup`", Assert.Single(document.Rows).Section);
    }

    [Fact]
    public void Names_a_row_by_its_cell_text_when_the_cell_has_no_code_span()
    {
        const string aria = """
            ### 5.2 ARIA

            | Element | Attribute | Value | Req | Writer |
            |---|---|---|---|---|
            | outside elements | `aria-hidden="true"` | always while open | ADL-ARIA-03 | JS |
            | (element present) | | `keepMounted \| mounted` | TAB-DOM-12 | C# |
            """;

        DocumentParse document = WriterColumn.Parse("alert-dialog", aria);

        Assert.Equal(["aria-hidden", "(element present)"], document.Rows.Select(row => row.Attribute));
    }

    [Fact]
    public void Does_not_split_a_cell_at_an_escaped_pipe_or_a_pipe_inside_a_code_span()
    {
        const string table = """
            ### 5.1 Root element

            | Attribute | Value | Req | Writer |
            |---|---|---|---|
            | `data-orientation` | `"horizontal" \| "vertical"`, always | TAB-DOM-01 | C# |
            | `data-activation-direction` | `"left" | "right"`, always | TAB-DOM-02 | JS |
            """;

        DocumentParse document = WriterColumn.Parse("tabs", table);

        Assert.Empty(document.Errors);
        Assert.Equal([Writer.Csharp, Writer.Js], document.Rows.Select(row => row.Writer));
    }

    [Fact]
    public void Ignores_tables_without_a_Writer_column()
    {
        const string other = """
            ### 4.1 Props

            | Prop | Type | Req |
            |---|---|---|
            | `value` | string | TAB-API-01 |
            """;

        Assert.Empty(WriterColumn.Parse("tabs", other).Rows);
    }

    [Fact]
    public void Reports_a_Writer_cell_it_does_not_recognise()
    {
        const string table = """
            ### 5.1 Root element

            | Attribute | Value | Req | Writer |
            |---|---|---|---|
            | `data-x` | always | TAB-DOM-01 | both |
            """;

        DocumentParse document = WriterColumn.Parse("tabs", table);

        string error = Assert.Single(document.Errors);
        Assert.Contains("both", error);
        Assert.Contains("tabs.md:5", error);
    }

    [Fact]
    public void Reads_every_component_document_without_an_unrecognised_Writer_value()
    {
        string components = Path.Combine(TestSupport.RepositoryRoot.Find(), "docs", "components");

        DocumentParse[] documents = [.. Directory.GetFiles(components, "*.md").Select(path => WriterColumn.Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))];

        Assert.All(documents, document => Assert.Empty(document.Errors));
        Assert.True(documents.Sum(document => document.Rows.Count(row => row.Writer != Writer.Csharp)) > 100, "the documents tabulate well over a hundred JavaScript-owned attributes");
    }
}
