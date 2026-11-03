using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The registry's M5 half in C# (M5 track 1, issue 159, Deliverable 2; ADR 04 Amendments 2026-11-03 (1), chosen by the user):
/// an item's registration carries its explicit label, its text element's id and its explicit index, each only when set, so
/// labels are resolved in JavaScript and values stay in C#. The byte titles measure the <c>update</c> a registry change
/// sends and the order report it answers with, at 100 and 500 items, with and without labels (design point 5).
/// </summary>
public sealed class CompositeRegistrationFieldsTests : IDisposable
{
    // JSON.stringify escapes a quote as \" (the report's direction); Blazor's interop serializer, like System.Text.Json's
    // default encoder, writes it as \u0022 (the update's direction).
    private static readonly JsonSerializerOptions s_javaScriptStringify = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions s_interop = new();

    private readonly CompositeTestContext host = new();
    private readonly ITestOutputHelper output;

    public CompositeRegistrationFieldsTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "an item registers its label, text element and explicit index only when set, and a label change sends one update")]
    [Trait("Requirement", "CMP-STA-04")]
    [Trait("Requirement", "CMP-STA-06")]
    [Trait("Requirement", "CMP-STA-07")]
    [Trait("Requirement", "CMP-STA-17")]
    public void An_item_registers_its_label_text_and_index_only_when_set()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Fields("Alpha")));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;
        Assert.Contains($$"""
            "items":[{"id":"{{ids[0]}}","label":"Alpha"},{"id":"{{ids[1]}}"},{"id":"{{ids[2]}}","index":0},{"id":"{{ids[3]}}","label":""},{"id":"{{ids[4]}}","textId":"text-e"}]
            """.Trim(), attached);

        cut.Render(p => p.AddChildContent(Fields("Alpha")));
        Assert.Empty(host.UpdateOptions);

        cut.Render(p => p.AddChildContent(Fields("Apple")));
        string updated = Assert.Single(host.UpdateOptions);
        Assert.Contains($$"""{"id":"{{ids[0]}}","label":"Apple"}""", updated);
    }

    [Theory(DisplayName = "the update of a registry change and its order report stay within their measured sizes")]
    [Trait("Requirement", "CMP-STA-10")]
    [Trait("Requirement", "CMP-EVT-03")]
    // Measured 2026-11-04 (M5 track 1; arm64, Release), each budget about 5% above: 100 items 2265 B (4407 B on the wire),
    // 4055 B (8197 B) with labels, order report 1301 B (1505 B); 500 items 10665 B (20807 B), 20055 B (40197 B) with
    // labels, order report 6901 B (7905 B). Only the report's direction is bounded by Server's receive limit (32 KB).
    [InlineData(100, false, 2_400, 4_650, 1_400, 1_600)]
    [InlineData(100, true, 4_300, 8_650, 1_400, 1_600)]
    [InlineData(500, false, 11_200, 21_900, 7_250, 8_300)]
    [InlineData(500, true, 21_100, 42_300, 7_250, 8_300)]
    public void The_update_and_the_order_report_stay_within_their_measured_sizes(
        int count,
        bool labelled,
        int updateBudget,
        int updateWireBudget,
        int reportBudget,
        int reportWireBudget)
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Many(count, labelled, removeLast: false)));
        cut.Render(p => p.AddChildContent(Many(count, labelled, removeLast: true)));
        cut.Render(p => p.AddChildContent(Many(count, labelled, removeLast: false)));
        string update = host.UpdateOptions[^1];
        string[] ids = CompositeTestContext.ItemIds(update);
        Assert.Equal(count, ids.Length);

        // The options travel as a JSON string inside the interop call's JSON, so the wire adds one level of escaping; the
        // module reports the ids the same way (`twin.report` passes `JSON.stringify(payload)` as the argument). On Server
        // the circuit's MessagePack protocol carries that JSON as raw bytes.
        string report = JsonSerializer.Serialize(new CompositeItemsChange(ids), BlazixJsonContext.Default.CompositeItemsChange);
        int updateBytes = Encoding.UTF8.GetByteCount(update);
        int updateWire = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(update, s_interop));
        int reportBytes = Encoding.UTF8.GetByteCount(report);
        int reportWire = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(report, s_javaScriptStringify));
        output.WriteLine($"{count} items, labels {labelled}: update {updateBytes} B ({updateWire} B on the wire), order report {reportBytes} B ({reportWire} B on the wire)");

        Assert.InRange(updateBytes, 1, updateBudget);
        Assert.InRange(updateWire, 1, updateWireBudget);
        Assert.InRange(reportBytes, 1, reportBudget);
        Assert.InRange(reportWire, 1, reportWireBudget);
    }

    // Design point 5 (b), chosen by the user 2026-11-04: at 5,000 items the order report would exceed Server's default
    // 32 KB receive limit whole; the module sends it in parts of at most 8,192 characters of JSON (the rule of
    // `composite.ts`'s `reportItemOrder`, replayed here), each within the limit on the wire, and the root joins them in order.
    [Fact(DisplayName = "at 5,000 items the order report arrives in parts within the receive limit and the root joins them in order")]
    [Trait("Requirement", "CMP-EVT-03")]
    [Trait("Requirement", "CMP-STA-10")]
    public void At_5000_items_the_order_report_arrives_in_parts_within_the_limit_and_is_joined_in_order()
    {
        const int receiveLimit = 32 * 1024;
        const int chunkChars = 8192;
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(Many(5000, labelled: false, removeLast: false)));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        string whole = JsonSerializer.Serialize(new CompositeItemsChange(ids), BlazixJsonContext.Default.CompositeItemsChange);
        Assert.True(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(whole, s_javaScriptStringify)) > receiveLimit);

        List<string> parts = [];
        List<string> chunk = [];
        foreach (string id in ids)
        {
            chunk.Add(id);
            if (JsonSerializer.Serialize(new { ids = chunk, publication = 1, part = 99, parts = 99 }, s_javaScriptStringify).Length > chunkChars)
            {
                chunk.RemoveAt(chunk.Count - 1);
                parts.Add(JsonSerializer.Serialize(chunk, s_javaScriptStringify));
                chunk = [id];
            }
        }

        parts.Add(JsonSerializer.Serialize(chunk, s_javaScriptStringify));
        CompositeTwin twin = host.Twin;
        for (int i = 0; i < parts.Count; i++)
        {
            string payload = $$"""{"ids":{{parts[i]}},"publication":1,"part":{{i}},"parts":{{parts.Count}}}""";
            Assert.InRange(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(payload, s_javaScriptStringify)), 1, receiveLimit);
            cut.InvokeAsync(() => twin.OnItemsChange(payload));
        }

        output.WriteLine($"5000 items: whole report {Encoding.UTF8.GetByteCount(whole)} B, {parts.Count} parts");
        Assert.True(parts.Count > 1);
        Assert.Equal(ids, cut.Instance.Behavior.OrderedItems.Select(item => item.Registration.Id));
    }

    private static RenderFragment Fields(string label) => builder =>
    {
        builder.OpenComponent<TestCompositeItem>(0);
        builder.AddAttribute(1, "data-item", "a");
        builder.AddAttribute(2, nameof(TestCompositeItem.Label), label);
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(3);
        builder.AddAttribute(4, "data-item", "b");
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(5);
        builder.AddAttribute(6, "data-item", "c");
        builder.AddAttribute(7, nameof(TestCompositeItem.Index), 0);
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(8);
        builder.AddAttribute(9, "data-item", "d");
        builder.AddAttribute(10, nameof(TestCompositeItem.Label), string.Empty);
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(11);
        builder.AddAttribute(12, "data-item", "e");
        builder.AddAttribute(13, nameof(TestCompositeItem.TextId), "text-e");
        builder.CloseComponent();
    };

    // The last item removed and added back, as a filtered list's keystroke changes its membership.
    private static RenderFragment Many(int count, bool labelled, bool removeLast) => builder =>
    {
        int rendered = removeLast ? count - 1 : count;
        for (int i = 0; i < rendered; i++)
        {
            builder.OpenComponent<TestCompositeItem>(0);
            builder.SetKey(i);
            builder.AddAttribute(1, "data-item", i);
            if (labelled)
            {
                builder.AddAttribute(2, nameof(TestCompositeItem.Label), "Item " + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            builder.CloseComponent();
        }
    };
}
