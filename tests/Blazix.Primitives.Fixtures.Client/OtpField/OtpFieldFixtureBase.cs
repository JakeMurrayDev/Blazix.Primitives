using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.OtpFieldFixtures;

/// <summary>
/// The base of the OtpField fixtures (tests/browser/otp-field*.spec.ts): the readiness marker once every root's
/// <c>otp-field</c> module is attached, the recorders of OnValueChange (<c>value:reason</c>), OnValueInvalid
/// (<c>raw:reason</c>) and OnValueComplete (<c>value:reason</c>), and the query reader.
/// </summary>
public abstract class OtpFieldFixtureBase : ComponentBase
{
    private readonly List<Func<Task>> attached = [];
    private Dictionary<string, string>? query;

    /// <summary>Gets a value indicating whether every root's module is attached.</summary>
    protected bool Attached { get; private set; }

    /// <summary>Gets the recorded changes, <c>value:reason</c>.</summary>
    protected List<string> Changes { get; } = [];

    /// <summary>Gets the recorded rejected inputs, <c>raw:reason</c>.</summary>
    protected List<string> Invalids { get; } = [];

    /// <summary>Gets the recorded completions, <c>value:reason</c>.</summary>
    protected List<string> Completes { get; } = [];

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Sets a root whose attach the readiness marker waits for.</summary>
    protected OtpFieldRoot? Root
    {
        set
        {
            if (value is not null && !Attached)
            {
                attached.Add(() => value.TwinAttached);
            }
        }
    }

    /// <summary>Records a change.</summary>
    /// <param name="args">The change.</param>
    protected void RecordChange(ValueChangeEventArgs<string> args) => Changes.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}");

    /// <summary>Records a rejected input.</summary>
    /// <param name="args">The rejected input.</param>
    protected void RecordInvalid(OtpFieldInvalidEventArgs args) => Invalids.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}");

    /// <summary>Records a completion.</summary>
    /// <param name="args">The completion.</param>
    protected void RecordComplete(OtpFieldCompleteEventArgs args) => Completes.Add($"{Show(args.Value)}:{args.Details.Reason.ToLiteral()}");

    /// <summary>Reads a query parameter.</summary>
    /// <param name="name">The name.</param>
    /// <param name="fallback">The value when absent.</param>
    /// <returns>The value.</returns>
    protected string Query(string name, string fallback)
    {
        query ??= ParseQuery(new Uri(Navigation.Uri).Query);
        return query.TryGetValue(name, out string? value) ? value : fallback;
    }

    /// <summary>Reads a flag query parameter.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Whether it is present and not <c>false</c>.</returns>
    protected bool Flag(string name) => Query(name, "false") != "false";

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

    // An empty value shows as `_` so the space-separated recorders stay readable.
    private static string Show(string value) => value.Length == 0 ? "_" : value;

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
