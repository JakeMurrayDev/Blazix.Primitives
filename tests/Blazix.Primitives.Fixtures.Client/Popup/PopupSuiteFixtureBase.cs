using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The base of every fixture the shared popup suites run on (tests/browser/suites/; ADR 06 Amendments 2026-10-27, design
/// point 7 of M4 track 1, settled by the orchestrator 2026-10-27), inherited by the popup families' fixtures. It reads the
/// case and its options from the query (<c>?case=controlled&amp;open=true</c>), records the root's callbacks for the
/// container's data attributes, renders <c>data-attached</c> once every listed handle has attached, and counts the sync
/// button the specs click as a barrier.
/// </summary>
/// <remarks>
/// The container renders <c>data-open-changes</c> (space-separated <c>open:reason</c> entries, such as
/// <c>true:trigger-press false:close-press</c>), <c>data-open-change-completes</c> (space-separated booleans) and
/// <c>data-attached</c>; the contract the helpers read is documented in tests/browser/CONTEXT.md.
/// </remarks>
public abstract class PopupSuiteFixtureBase : ComponentBase
{
    private readonly List<string> openChanges = [];
    private readonly List<string> openChangeCompletes = [];
    private Dictionary<string, string>? query;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>Gets the case the query names, or <c>default</c>.</summary>
    protected string Case => Query("case") ?? "default";

    /// <summary>Gets the recorded open changes as the container renders them.</summary>
    protected string OpenChanges => string.Join(' ', openChanges);

    /// <summary>Gets the recorded completions as the container renders them.</summary>
    protected string OpenChangeCompletes => string.Join(' ', openChangeCompletes);

    /// <summary>Gets the attach tasks to wait for after the first interactive render.</summary>
    protected virtual IEnumerable<Task> Attachments => [];

    /// <summary>Reads a query option.</summary>
    /// <param name="name">The option's name.</param>
    /// <returns>The option's value, or <see langword="null"/> when absent.</returns>
    protected string? Query(string name)
    {
        query ??= ParseQuery(new Uri(Navigation.Uri).Query);
        return query.TryGetValue(name, out string? value) ? value : null;
    }

    /// <summary>Reads a boolean query option: <c>true</c> or <c>1</c>.</summary>
    /// <param name="name">The option's name.</param>
    /// <returns><see langword="true"/> when the option is <c>true</c> or <c>1</c>; <see langword="null"/> when absent.</returns>
    protected bool? QueryFlag(string name) => Query(name) is { } value ? value is "true" or "1" : null;

    // The suites' `pressControl` (tests/browser/suites/popupFixture.ts) presses a control marked `data-key-control` with a
    // dispatched Shift keydown, which no dismissal mode reads (a click on the control is an outside press once a real press
    // happened while open). The control acts on that key only, beside its onclick, so a real Tab on a focused control does
    // nothing (gap 11 of M4 track 5, settled by the orchestrator 2026-10-28).
    /// <summary>Wraps a control's action as its keydown handler, which runs it for the Shift key only.</summary>
    /// <param name="action">The control's action.</param>
    /// <returns>The keydown handler.</returns>
    protected static Func<KeyboardEventArgs, Task> OnShift(Func<Task> action) =>
        e => e.Key == "Shift" ? action() : Task.CompletedTask;

    /// <summary>Wraps a control's action as its keydown handler, which runs it for the Shift key only.</summary>
    /// <param name="action">The control's action.</param>
    /// <returns>The keydown handler.</returns>
    protected static Action<KeyboardEventArgs> OnShift(Action action) => e =>
    {
        if (e.Key == "Shift")
        {
            action();
        }
    };

    /// <summary>
    /// Records the root's open change; cancels an open when the case asks (<c>cancelOpen=true</c>), cancels a close
    /// (<c>cancelClose=true</c>) or lets a close's keydown propagate (<c>allowPropagation=true</c>).
    /// </summary>
    /// <param name="args">The root's callback arguments.</param>
    protected void RecordOpenChange(ValueChangeEventArgs<bool> args)
    {
        openChanges.Add(string.Create(CultureInfo.InvariantCulture, $"{(args.Value ? "true" : "false")}:{args.Details.Reason.ToLiteral()}"));
        if (args.Value && QueryFlag("cancelOpen") == true)
        {
            args.Details.Cancel();
        }

        // The Escape titles of tests/browser/interactions.spec.ts (M4 track 3): a close canceled, or one that lets the
        // keydown propagate.
        if (!args.Value && QueryFlag("cancelClose") == true)
        {
            args.Details.Cancel();
        }

        if (!args.Value && QueryFlag("allowPropagation") == true)
        {
            args.Details.AllowPropagation();
        }

        StateHasChanged();
    }

    /// <summary>Records the root's completion.</summary>
    /// <param name="open">The completed phase.</param>
    protected void RecordOpenChangeComplete(bool open)
    {
        openChangeCompletes.Add(open ? "true" : "false");
        StateHasChanged();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Attachments);
        Attached = true;
        StateHasChanged();
    }

    private static Dictionary<string, string> ParseQuery(string search)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string pair in search.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = Uri.UnescapeDataString(equals < 0 ? pair : pair[..equals]);
            values[key] = equals < 0 ? "true" : Uri.UnescapeDataString(pair[(equals + 1)..]);
        }

        return values;
    }
}
