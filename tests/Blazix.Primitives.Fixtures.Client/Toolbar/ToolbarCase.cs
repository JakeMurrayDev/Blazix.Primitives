using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.ToolbarFixtures;

/// <summary>
/// The record of one case of the Toolbar fixtures, as the specs read it: the consumer's clicks, key presses, pointer and
/// mouse presses and mouse moves Blazor invoked, a substituted switch's <c>OnCheckedChange</c>, a substituted toggle's <c>OnPressedChange</c> and a toggle group's <c>OnValueChange</c>, and the order of them; and
/// the keys for which a consumer's <c>onkeydown</c> calls <c>PreventPartHandler()</c>.
/// </summary>
public sealed class ToolbarCase
{
    private readonly List<string> log = [];

    public int Clicks { get; private set; }

    public int KeyDowns { get; private set; }

    public int PointerDowns { get; private set; }

    public int MouseDowns { get; private set; }

    public int MouseMoves { get; private set; }

    public int Changes { get; private set; }

    public string Checked { get; private set; } = "";

    public string Log => string.Join(' ', log);

    /// <summary>Gets or sets the keys for which the consumer's <c>onkeydown</c> calls <c>PreventPartHandler()</c>.</summary>
    public IReadOnlyList<string> VetoKeys { get; set; } = [];

    /// <summary>Records a click of the consumer's <c>onclick</c> on the element named <paramref name="source"/>.</summary>
    public void Click(string source)
    {
        Clicks++;
        log.Add($"{source}:click");
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

    public void PointerDown(string source)
    {
        PointerDowns++;
        log.Add($"{source}:pointerdown");
    }

    public void MouseDown(string source)
    {
        MouseDowns++;
        log.Add($"{source}:mousedown");
    }

    // Not logged: a hover dispatches many.
    public void MouseMove() => MouseMoves++;

    /// <summary>Records a substituted toggle's <c>OnPressedChange</c>.</summary>
    public void Press(ValueChangeEventArgs<bool> e)
    {
        Changes++;
        log.Add($"press:{(e.Value ? "true" : "false")}");
    }

    /// <summary>Records a toggle group's <c>OnValueChange</c>, with the proposed value.</summary>
    public void GroupChange(ValueChangeEventArgs<IReadOnlyList<string>> e)
    {
        Changes++;
        log.Add($"value:{string.Join(',', e.Value)}");
    }

    public void CheckedChange(ValueChangeEventArgs<bool> e)
    {
        Changes++;
        Checked = e.Value ? "true" : "false";
        log.Add($"checked:{Checked}");
    }
}
