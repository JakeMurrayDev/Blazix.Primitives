using System.Globalization;
using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.ComboboxFixtures;

/// <summary>
/// The base of the fixtures that port the Base UI titles of Combobox's [L4] rows (tests/browser/combobox.*.spec.ts, M5
/// track 10, issue 174). It adds to <see cref="PopupSuiteFixtureBase"/> (the query, <c>data-open-changes</c>,
/// <c>data-open-change-completes</c>, <c>data-attached</c>, <c>data-sync</c>) a recorder per callback the titles read, which
/// the container renders through <see cref="Records"/> as <c>data-&lt;name&gt;</c> attributes of space-separated entries.
/// </summary>
/// <remarks>
/// An entry is <c>value:reason</c> (a space in a value is <c>_</c>, a <see langword="null"/> value is <c>null</c>); a
/// highlight is <c>value:reason:index</c>; a multiple value is its values joined by <c>,</c>.
/// </remarks>
public abstract class ComboboxL4FixtureBase : PopupSuiteFixtureBase
{
    private readonly Dictionary<string, List<string>> records = new(StringComparer.Ordinal);

    /// <summary>Gets the recorded lists as the container's attributes (<c>data-&lt;name&gt;</c>).</summary>
    protected IReadOnlyDictionary<string, object> Records
    {
        get
        {
            Dictionary<string, object> attributes = new(StringComparer.Ordinal);
            foreach ((string name, List<string> entries) in records)
            {
                attributes["data-" + name] = string.Join(' ', entries);
            }

            return attributes;
        }
    }

    /// <summary>Gets the sync button <c>settle(fixture)</c> presses (a Shift keydown, so it is never an outside press).</summary>
    protected RenderFragment SyncButton => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "data-sync-button", "");
        builder.AddAttribute(3, "data-key-control", "");
        builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => Sync++));
        builder.AddAttribute(5, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, OnShift(() => Sync++)));
        builder.AddContent(6, "Sync");
        builder.CloseElement();
    };

    /// <summary>Appends an entry to a recorded list and renders it.</summary>
    /// <param name="name">The list's name; the container renders it as <c>data-&lt;name&gt;</c>.</param>
    /// <param name="entry">The entry.</param>
    protected void Record(string name, string entry)
    {
        if (!records.TryGetValue(name, out List<string>? entries))
        {
            entries = [];
            records[name] = entries;
        }

        entries.Add(entry);
        StateHasChanged();
    }

    /// <summary>Formats a recorded value: <c>null</c>, or the value with its spaces as <c>_</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The entry's value part.</returns>
    protected static string Format(string? value) => value is null ? "null" : value.Replace(' ', '_');

    /// <summary>Formats a <c>value:reason</c> entry.</summary>
    /// <param name="value">The value.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>The entry.</returns>
    protected static string Entry(string? value, EventReason reason) => Format(value) + ":" + reason.ToLiteral();

    /// <summary>Records a single value change as <c>value-changes</c>.</summary>
    /// <param name="args">The root's callback arguments.</param>
    protected void RecordValueChange(ValueChangeEventArgs<string> args) => Record("value-changes", Entry(args.Value, args.Details.Reason));

    /// <summary>Records a multiple value change as <c>value-changes</c> (the values joined by <c>,</c>).</summary>
    /// <param name="args">The root's callback arguments.</param>
    protected void RecordValuesChange(ValueChangeEventArgs<IReadOnlyList<string>> args) =>
        Record("value-changes", Entry(string.Join(',', args.Value ?? []), args.Details.Reason));

    /// <summary>Records an input value change as <c>input-changes</c>.</summary>
    /// <param name="args">The root's callback arguments.</param>
    protected void RecordInputChange(ValueChangeEventArgs<string> args) => Record("input-changes", Entry(args.Value, args.Details.Reason));

    /// <summary>Records a highlight as <c>highlights</c> (<c>value:reason:index</c>).</summary>
    /// <param name="args">The root's callback arguments.</param>
    protected void RecordHighlight(ComboboxItemHighlightedEventArgs<string> args) =>
        Record("highlights", string.Create(CultureInfo.InvariantCulture, $"{Format(args.Value)}:{args.Details.Reason.ToLiteral()}:{args.Details.Index}"));

    /// <summary>Records a highlight's event type as <c>highlight-events</c> (<c>type</c>, or <c>none</c> for a placeholder).</summary>
    /// <param name="args">The root's callback arguments.</param>
    protected void RecordHighlightEvent(ComboboxItemHighlightedEventArgs<string> args) =>
        Record("highlight-events", args.Details.Event.Type == EventSnapshot.PlaceholderType ? "none" : args.Details.Event.Type);
}
