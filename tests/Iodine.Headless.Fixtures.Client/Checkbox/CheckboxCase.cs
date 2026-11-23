using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.CheckboxFixtures;

/// <summary>
/// The record of one case of the Checkbox fixtures: every <c>OnCheckedChange</c> with its value and modifier keys, the
/// consumer's clicks Blazor invoked, and the order of the two, as the specs read them.
/// </summary>
public sealed class CheckboxCase
{
    private readonly List<string> log = [];

    public int Changes { get; private set; }

    public int Clicks { get; private set; }

    public string LastChange { get; private set; } = "";

    public string Log => string.Join(' ', log);

    /// <summary>Gets or sets a value indicating whether <see cref="Change"/> cancels the request.</summary>
    public bool Cancel { get; set; }

    public void Change(ValueChangeEventArgs<bool> e)
    {
        Changes++;
        EventSnapshot snapshot = e.Details.Event;
        LastChange = $"value={(e.Value ? "true" : "false")} type={snapshot.Type} detail={snapshot.Detail} shift={(snapshot.ShiftKey ? "true" : "false")} reason={e.Details.Reason.ToLiteral()}";
        log.Add("change");
        if (Cancel)
        {
            e.Details.Cancel();
        }
    }

    public void Click(MouseEventArgs e)
    {
        Clicks++;
        log.Add("click");
    }
}
