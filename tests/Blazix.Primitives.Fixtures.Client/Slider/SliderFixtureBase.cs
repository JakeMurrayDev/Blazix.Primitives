using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.SliderFixtures;

/// <summary>
/// The base of the Slider fixtures: renders <c>data-attached</c> once every tracked root has its <c>slider</c> twin attached
/// (the specs wait for it before they press or drag, never on a settle alone), records the callbacks the specs read, and
/// reads the page's query for the cases a spec chooses. A root is tracked by assigning it with <c>@ref</c> to
/// <see cref="Root"/>; a label and a form likewise through <see cref="Label"/> and <see cref="FormPart"/>.
/// </summary>
public abstract class SliderFixtureBase : ComponentBase
{
    private readonly List<Func<Task>> attached = [];
    private Dictionary<string, string>? query;

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>Gets the recorded <c>OnValueChange</c> calls: <c>value:reason:activeThumbIndex</c>, space-separated.</summary>
    protected List<string> Changes { get; } = [];

    /// <summary>Gets the recorded <c>OnValueCommitted</c> calls: <c>value:reason</c>, space-separated.</summary>
    protected List<string> Commits { get; } = [];

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Sets a root to track.</summary>
    protected SliderRoot? Root
    {
        set
        {
            if (value is not null && !Attached)
            {
                attached.Add(() => value.TwinAttached);
            }
        }
    }

    /// <summary>Sets a label to track: readiness also waits for its <c>form</c> handle (its click-to-focus listener).</summary>
    protected SliderLabel? Label
    {
        set
        {
            if (value is not null && !Attached)
            {
                attached.Add(() => value.TwinAttached);
            }
        }
    }

    /// <summary>Sets a form to track: readiness also waits for its <c>form</c> handle (its submit interception).</summary>
    protected Form? FormPart
    {
        set
        {
            if (value is not null && !Attached)
            {
                attached.Add(() => value.TwinAttached);
            }
        }
    }

    /// <summary>Formats a number as the recorders write it.</summary>
    protected static string Show(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Formats a list as the recorders write it: the numbers joined with commas in brackets.</summary>
    protected static string Show(IReadOnlyList<double> values) => "[" + string.Join(",", values.Select(Show)) + "]";

    /// <summary>Records an <c>OnValueChange</c> call.</summary>
    protected void RecordChange(ValueChangeEventArgs<double> args) =>
        Changes.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}:{((SliderChangeEventDetails)args.Details).ActiveThumbIndex}");

    /// <summary>Records an <c>OnValuesChange</c> call.</summary>
    protected void RecordChanges(ValueChangeEventArgs<IReadOnlyList<double>> args) =>
        Changes.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}:{((SliderChangeEventDetails)args.Details).ActiveThumbIndex}");

    /// <summary>Records an <c>OnValueCommitted</c> call.</summary>
    protected void RecordCommit(ValueCommittedEventArgs<double> args) =>
        Commits.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}");

    /// <summary>Records an <c>OnValuesCommitted</c> call.</summary>
    protected void RecordCommits(ValueCommittedEventArgs<IReadOnlyList<double>> args) =>
        Commits.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}");

    /// <summary>Reads a query parameter of the page.</summary>
    protected string Query(string name, string fallback)
    {
        query ??= ParseQuery(new Uri(Navigation.Uri).Query);
        return query.TryGetValue(name, out string? value) ? value : fallback;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(attached.Select(task => task()));
        Attached = true;
        StateHasChanged();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = Uri.UnescapeDataString((equals < 0 ? pair : pair[..equals]).Replace('+', ' '));
            values[key] = equals < 0 ? "" : Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
        }

        return values;
    }
}
