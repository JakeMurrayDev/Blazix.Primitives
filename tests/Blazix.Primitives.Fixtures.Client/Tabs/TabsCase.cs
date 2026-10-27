using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.TabsFixtures;

/// <summary>
/// The record of one case of the Tabs fixtures: every <c>OnValueChange</c> with its value, reason, activation direction and
/// event, the consumer's handlers Blazor or the parts invoked (the List's and a Tab's key presses, a Tab's clicks, focuses
/// and pointer presses), and the order of them, as the specs read them.
/// </summary>
public sealed class TabsCase
{
    private readonly List<string> log = [];

    public int Changes { get; private set; }

    public int ListKeyDowns { get; private set; }

    public int TabKeyDowns { get; private set; }

    public int Clicks { get; private set; }

    public int Focuses { get; private set; }

    public int PointerDowns { get; private set; }

    public string LastChange { get; private set; } = "";

    public string Log => string.Join(' ', log);

    /// <summary>Gets or sets a value indicating whether <see cref="Change{TValue}"/> cancels the request.</summary>
    public bool Cancel { get; set; }

    /// <summary>Gets or sets a value indicating whether the consumer's click and focus handlers call <c>PreventPartHandler()</c>.</summary>
    public bool Veto { get; set; }

    /// <summary>Gets or sets a value indicating whether the List's key handler calls <c>PreventPartHandler()</c>.</summary>
    public bool VetoKeys { get; set; }

    /// <summary>Gets or sets what a controlled case does with a change, after recording it.</summary>
    public Action<object?>? Apply { get; set; }

    public void Change<TValue>(ValueChangeEventArgs<TValue> e)
    {
        Changes++;
        EventSnapshot snapshot = e.Details.Event;
        TabsActivationDirection direction = e.Details is TabsChangeEventDetails tabs ? tabs.ActivationDirection : TabsActivationDirection.None;
        LastChange = $"value={e.Value} reason={e.Details.Reason.ToLiteral()} direction={direction.ToString().ToLowerInvariant()} type={snapshot.Type} kind={snapshot.Kind} trigger={(e.Details.Trigger is null ? "none" : "some")} cancelable={(e.Details.Reason == EventReason.None ? "true" : "false")}";
        log.Add($"change:{e.Value}");
        if (Cancel)
        {
            e.Details.Cancel();
        }

        Apply?.Invoke(e.Value);
    }

    public void ListKeyDown(KeyboardEventArgs e)
    {
        ListKeyDowns++;
        log.Add($"list-keydown:{e.Key}");
        if (VetoKeys)
        {
            e.PreventPartHandler();
        }
    }

    public void TabKeyDown(KeyboardEventArgs e)
    {
        TabKeyDowns++;
        log.Add($"tab-keydown:{e.Key}");
    }

    public void Click(MouseEventArgs e)
    {
        Clicks++;
        log.Add("click");
        if (Veto)
        {
            e.PreventPartHandler();
        }
    }

    public void Focus(FocusEventArgs e)
    {
        Focuses++;
        log.Add("focus");
        if (Veto)
        {
            e.PreventPartHandler();
        }
    }

    public void PointerDown(PointerEventArgs e)
    {
        PointerDowns++;
        log.Add("pointerdown");
    }
}
