using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.ToggleFixtures;

/// <summary>
/// The record of one case of the Toggle and ToggleGroup fixtures, as the specs read it: every toggle's
/// <c>OnPressedChange</c> and the group's <c>OnValueChange</c> with their values and events, the consumer's clicks and key
/// presses Blazor invoked, and the order of them; and the switches that make a consumer handler call
/// <c>PreventPartHandler()</c>.
/// </summary>
public sealed class ToggleCase
{
    private readonly List<string> log = [];

    public int Presses { get; private set; }

    public int Changes { get; private set; }

    public int Clicks { get; private set; }

    public int KeyDowns { get; private set; }

    public int PointerDowns { get; private set; }

    public string LastPress { get; private set; } = "";

    public string LastChange { get; private set; } = "";

    public string Log => string.Join(' ', log);

    /// <summary>Gets or sets a value indicating whether the consumer's <c>onclick</c> calls <c>PreventPartHandler()</c>.</summary>
    public bool VetoClick { get; set; }

    /// <summary>Gets or sets the keys for which the consumer's <c>onkeydown</c> calls <c>PreventPartHandler()</c>.</summary>
    public IReadOnlyList<string> VetoKeys { get; set; } = [];

    public void Press(ValueChangeEventArgs<bool> e)
    {
        Presses++;
        EventSnapshot snapshot = e.Details.Event;
        LastPress = $"pressed={(e.Value ? "true" : "false")} type={snapshot.Type} detail={snapshot.Detail} reason={e.Details.Reason.ToLiteral()} trigger={(e.Details.Trigger is null ? "none" : "some")}";
        log.Add($"press:{(e.Value ? "true" : "false")}");
    }

    public void Change(ValueChangeEventArgs<IReadOnlyList<string>> e)
    {
        Changes++;
        LastChange = string.Join(',', e.Value);
        log.Add($"change:{LastChange}");
    }

    public void Click(MouseEventArgs e)
    {
        Clicks++;
        log.Add("click");
        if (VetoClick)
        {
            e.PreventPartHandler();
        }
    }

    public void PointerDown(PointerEventArgs e)
    {
        PointerDowns++;
        log.Add("pointerdown");
    }

    /// <summary>Records a key press of the consumer's <c>onkeydown</c> on the element named <paramref name="source"/>.</summary>
    public void KeyDown(string source, KeyboardEventArgs e)
    {
        KeyDowns++;
        log.Add($"{source}:{e.Key}");
        if (VetoKeys.Contains(e.Key))
        {
            e.PreventPartHandler();
        }
    }
}
