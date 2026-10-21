using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.ButtonFixtures;

/// <summary>
/// The counts of one case of the Button fixtures: each consumer handler Blazor invoked, the substitute element's and an
/// ancestor's clicks, and the detail and Shift state of the last consumer click.
/// </summary>
public sealed class ButtonCase
{
    public int Clicks { get; private set; }

    public int MouseDowns { get; private set; }

    public int PointerDowns { get; private set; }

    public int KeyDowns { get; private set; }

    public int KeyUps { get; private set; }

    public int MouseMoves { get; private set; }

    public int SubstituteClicks { get; private set; }

    public int AncestorClicks { get; private set; }

    public string LastClick { get; private set; } = "";

    public void Click(MouseEventArgs e)
    {
        Clicks++;
        LastClick = $"detail={e.Detail} shift={(e.ShiftKey ? "true" : "false")}";
    }

    public void MouseDown(MouseEventArgs e) => MouseDowns++;

    public void PointerDown(PointerEventArgs e) => PointerDowns++;

    public void KeyDown(KeyboardEventArgs e) => KeyDowns++;

    public void KeyUp(KeyboardEventArgs e) => KeyUps++;

    public void MouseMove(MouseEventArgs e) => MouseMoves++;

    public void SubstituteClick(MouseEventArgs e) => SubstituteClicks++;

    public void AncestorClick(MouseEventArgs e) => AncestorClicks++;
}
