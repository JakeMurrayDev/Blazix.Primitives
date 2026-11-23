namespace Blazix.Primitives;

// FLT-FOC-10 (FloatingFocusManager.tsx:50-77, getEventType): the close interaction type, classified in C# from the close
// event's snapshot and the type the `focus-manager` module last recorded (design point 6 of the focus-manager track,
// settled by the orchestrator 2026-10-27). Base UI's empty literal is InteractionType.Unknown, and an Unknown recorded
// type counts as none. A snapshot carries no `isTrusted` and no platform flag, so a pointer event without a pointer type
// is a virtual click only when its detail is 0 (FLT-FOC-10's note).
/// <summary>Classifies how the user closed a focus-managed popup.</summary>
internal static class CloseTypeClassifier
{
    /// <summary>Classifies the interaction type of a close.</summary>
    /// <param name="closeEvent">The snapshot of the event that caused the close; <see langword="null"/> or the placeholder for none.</param>
    /// <param name="recorded">The interaction type the module last recorded while open; <see langword="null"/> for none.</param>
    /// <returns>
    /// <see cref="InteractionType.Keyboard"/> for a keyboard event; for a focus event, the recorded type, else
    /// <see cref="InteractionType.Keyboard"/>; for a pointer event, its pointer type, else <see cref="InteractionType.Keyboard"/>
    /// for a virtual click, else the recorded type, else <see cref="InteractionType.Mouse"/>; <see cref="InteractionType.Touch"/>
    /// for a touch event; for another mouse event, the recorded type, else <see cref="InteractionType.Keyboard"/> when its
    /// detail is 0, else <see cref="InteractionType.Mouse"/>; otherwise <see cref="InteractionType.Unknown"/>, the empty
    /// interaction type (never <see langword="null"/>).
    /// </returns>
    public static InteractionType Classify(EventSnapshot? closeEvent, InteractionType? recorded)
    {
        InteractionType? last = recorded is InteractionType.Unknown ? null : recorded;
        if (closeEvent is null)
        {
            return InteractionType.Unknown;
        }

        switch (closeEvent.Kind)
        {
            case EventKind.KeyboardEvent:
                return InteractionType.Keyboard;
            case EventKind.FocusEvent:
                return last ?? InteractionType.Keyboard;
            case EventKind.PointerEvent:
                if (!string.IsNullOrEmpty(closeEvent.PointerType))
                {
                    return InteractionTypeLiterals.FromLiteral(closeEvent.PointerType);
                }

                return closeEvent.Detail == 0 ? InteractionType.Keyboard : last ?? InteractionType.Mouse;
            case EventKind.TouchEvent:
                return InteractionType.Touch;
            case EventKind.MouseEvent or EventKind.WheelEvent:
                return last ?? (closeEvent.Detail == 0 ? InteractionType.Keyboard : InteractionType.Mouse);
            default:
                return InteractionType.Unknown;
        }
    }
}
