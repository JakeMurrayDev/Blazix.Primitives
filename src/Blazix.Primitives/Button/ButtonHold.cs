using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// UBT-ALG-07, UBT-EVT-10, UBT-PTR-20, NUM-PTR-02/03; issue 277 deliverable 3, decision 16 (a), chosen by the user 2026-11-18.
// The press-and-hold state of one ButtonBehavior, in an object of its own so the parts without a repeater (Button, Toggle,
// Switch, Checkbox, Radio, the composite items) carry one null field instead of it (Combobox's keystroke budget, ADR 16
// rule 7).
/// <summary>The press-and-hold repeater of a button-like part: its reports, its input focus, its stop and its press veto.</summary>
/// <param name="started">Receives the event that started a sequence, before its first tick.</param>
/// <param name="ticked">Receives the event that started the sequence, on every tick.</param>
/// <param name="stopped">Receives the <c>pointerup</c> that released a started sequence.</param>
/// <param name="clicked">Receives a click that performs a single action (a keyboard click or a tap).</param>
/// <param name="pointerDownHandler">The part's own <c>onpointerdown</c> handler, which calls <see cref="ButtonBehavior.HandlePointerDownAsync"/>.</param>
internal sealed class ButtonHold(
    Action<EventSnapshot> started,
    Action<EventSnapshot> ticked,
    Action<EventSnapshot> stopped,
    Action<EventSnapshot> clicked,
    EventCallback pointerDownHandler)
{
    /// <summary>Gets the receiver of a sequence's start.</summary>
    public Action<EventSnapshot> Started { get; } = started;

    /// <summary>Gets the receiver of each tick.</summary>
    public Action<EventSnapshot> Ticked { get; } = ticked;

    /// <summary>Gets the receiver of the release.</summary>
    public Action<EventSnapshot> Stopped { get; } = stopped;

    /// <summary>Gets the receiver of a single-action click.</summary>
    public Action<EventSnapshot> Clicked { get; } = clicked;

    /// <summary>Gets the part's own <c>onpointerdown</c> handler.</summary>
    public EventCallback PointerDownHandler { get; } = pointerDownHandler;

    /// <summary>Gets or sets the id of the element a mouse press focuses, with the caret at the end, before the first tick.</summary>
    /// <value>The id of NumberField's input, or <see langword="null"/> for none.</value>
    public string? FocusTarget { get; set; }

    /// <summary>Gets or sets the number of stops C# requested.</summary>
    public int Stop { get; set; }

    /// <summary>Gets or sets the consumer's <c>onpointerdown</c>, which the part declines and runs itself.</summary>
    public AttributeValue ConsumerPointerDown { get; set; }

    /// <summary>Gets or sets a value indicating whether the <c>hold-start</c> decision already ran the consumer's handler.</summary>
    public bool DecidedPointerDown { get; set; }
}
