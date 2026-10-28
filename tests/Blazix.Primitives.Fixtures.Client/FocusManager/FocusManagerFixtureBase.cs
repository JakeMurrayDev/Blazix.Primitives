using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.FocusManagerFixtures;

/// <summary>
/// The base of the <c>focus-manager-*</c> fixtures (tests/browser/focus-manager.spec.ts). It plays the popup store: a
/// trigger toggles <see cref="IsOpen"/>, a focus-out request or Escape closes, and the close's event snapshot is handed to
/// the popup, whose <see cref="FocusManagerBehavior"/> classifies the close type (design point 6). It renders
/// <c>data-attached</c> once the handle is attached (or at once while no popup is rendered) and the routed focus-out
/// requests as <c>data-focus-out-closes</c>.
/// </summary>
public abstract class FocusManagerFixtureBase : ComponentBase
{
    protected FixtureFocusManaged? Popup { get; set; }

    protected bool IsOpen { get; set; }

    protected int FocusOutCloses { get; private set; }

    protected EventSnapshot? CloseEvent { get; private set; }

    protected InteractionType? OpenType { get; set; } = InteractionType.Unknown;

    protected bool Attached { get; private set; }

    /// <summary>Gets or sets a value indicating whether a focus-out close request is refused, as a consumer's cancel would.</summary>
    protected bool CancelFocusOutCloses { get; set; }

    protected void Toggle(MouseEventArgs e)
    {
        if (IsOpen)
        {
            Close(EventSnapshot.FromEventArgs(e));
        }
        else
        {
            OpenType = e.Detail == 0 ? InteractionType.Keyboard : InteractionType.Mouse;
            IsOpen = true;
        }
    }

    protected void Close(EventSnapshot snapshot)
    {
        CloseEvent = snapshot;
        IsOpen = false;
    }

    protected void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && IsOpen)
        {
            Close(EventSnapshot.FromEventArgs(e));
        }
    }

    protected void OnFocusOutClose(EventSnapshot snapshot)
    {
        FocusOutCloses++;
        if (!CancelFocusOutCloses)
        {
            Close(snapshot);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || Attached)
        {
            return;
        }

        if (Popup is { } popup)
        {
            await popup.TwinAttached;
        }

        Attached = true;
        StateHasChanged();
    }
}
