using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.RadioFixtures;

/// <summary>
/// The record of one case of the Radio and RadioGroup fixtures: every <c>OnValueChange</c> with its value, event and
/// modifier keys, the consumer's clicks and key presses Blazor invoked, and the order of them, as the specs read them.
/// </summary>
public sealed class RadioCase
{
    private readonly List<string> log = [];

    public int Changes { get; private set; }

    public int Clicks { get; private set; }

    public int KeyDowns { get; private set; }

    public string LastChange { get; private set; } = "";

    public string Log => string.Join(' ', log);

    /// <summary>Gets or sets a value indicating whether <see cref="Change{TValue}"/> cancels the request.</summary>
    public bool Cancel { get; set; }

    public void Change<TValue>(ValueChangeEventArgs<TValue> e)
    {
        Changes++;
        EventSnapshot snapshot = e.Details.Event;
        LastChange = $"value={e.Value} type={snapshot.Type} detail={snapshot.Detail} shift={(snapshot.ShiftKey ? "true" : "false")} reason={e.Details.Reason.ToLiteral()} trigger={(e.Details.Trigger is null ? "none" : "some")}";
        log.Add($"change:{e.Value}");
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

    public void KeyDown(KeyboardEventArgs e)
    {
        KeyDowns++;
        log.Add($"keydown:{e.Key}");
    }
}
