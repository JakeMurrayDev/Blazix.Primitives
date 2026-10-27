using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.FocusManagerFixtures;

/// <summary>
/// The base of the <c>focus-manager-*</c> fixtures (tests/browser/focus-manager.spec.ts). It plays the popup store: a
/// trigger toggles <see cref="IsOpen"/>, a focus-out request or Escape closes, and the close type is the interaction type
/// the module last reported (the classification is C#'s, design point 6). It renders <c>data-attached</c> once the
/// handle is attached (or at once while no popup is rendered), and the reports as <c>data-focus-out-closes</c> and
/// <c>data-interaction-type</c>.
/// </summary>
public abstract class FocusManagerFixtureBase : ComponentBase
{
    protected FixtureFocusManaged? Popup { get; set; }

    protected bool IsOpen { get; set; }

    protected int FocusOutCloses { get; private set; }

    protected string InteractionType { get; private set; } = "";

    protected string? CloseType { get; private set; }

    protected string? OpenType { get; set; } = "";

    protected bool Attached { get; private set; }

    protected void Toggle(MouseEventArgs e)
    {
        if (IsOpen)
        {
            Close(e.Detail == 0 ? "keyboard" : "mouse");
        }
        else
        {
            OpenType = e.Detail == 0 ? "keyboard" : "mouse";
            IsOpen = true;
        }
    }

    protected void Close(string? closeType)
    {
        CloseType = closeType;
        IsOpen = false;
    }

    protected void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && IsOpen)
        {
            Close("keyboard");
        }
    }

    protected void OnFocusOutClose(EventSnapshot snapshot)
    {
        FocusOutCloses++;
        Close(InteractionType);
    }

    protected void OnInteractionTypeChange(string type)
    {
        InteractionType = type;
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
