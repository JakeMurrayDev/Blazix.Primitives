namespace Blazix.Primitives;

// PSH §3.4, §4.4; utils/CommonTriggerDataAttributes.ts, CommonPopupDataAttributes.ts, CommonViewportDataAttributes.ts,
// CommonPopupCssVars.ts, FocusGuard.tsx:39, InternalBackdrop.tsx:24, popupStoreUtils.ts:27-30, the click-trigger marker of
// DialogTrigger.tsx:91 and the portal node's FloatingPortal.tsx:131. The families' public <Part>DataAttributes classes
// (ADR 08 §3) take their values from these.

/// <summary>Provides the data attributes every popup trigger shares.</summary>
internal static class PopupTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open and this trigger is active: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = "data-popup-open";

    /// <summary>Represents the attribute a pressable trigger renders with <see cref="PopupOpen"/>: <c>data-pressed</c>.</summary>
    public const string Pressed = "data-pressed";
}

/// <summary>Provides the data attributes the popup, positioner, backdrop and arrow parts share.</summary>
internal static class PopupDataAttributes
{
    /// <summary>Represents the attribute present while open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the enter transition starts: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the exit transition runs: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = "data-anchor-hidden";

    /// <summary>Represents the attribute whose value is the resolved side: <c>data-side</c>.</summary>
    public const string Side = "data-side";

    /// <summary>Represents the attribute whose value is the resolved alignment: <c>data-align</c>.</summary>
    public const string Align = "data-align";
}

/// <summary>Provides the data attributes of a popup viewport and its containers.</summary>
internal static class PopupViewportDataAttributes
{
    /// <summary>Represents the attribute whose value names the direction between the previous and new trigger: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = "data-activation-direction";

    /// <summary>Represents the attribute of the container of the current content: <c>data-current</c>.</summary>
    public const string Current = "data-current";

    /// <summary>Represents the attribute of the container of the previous content: <c>data-previous</c>.</summary>
    public const string Previous = "data-previous";

    /// <summary>Represents the attribute present while a content transition runs: <c>data-transitioning</c>.</summary>
    public const string Transitioning = "data-transitioning";
}

/// <summary>Provides the CSS variables popups share.</summary>
internal static class PopupCssVars
{
    /// <summary>Represents the popup's width during an auto-resize: <c>--popup-width</c>.</summary>
    public const string PopupWidth = "--popup-width";

    /// <summary>Represents the popup's height during an auto-resize: <c>--popup-height</c>.</summary>
    public const string PopupHeight = "--popup-height";
}

/// <summary>Provides the marker attributes the popup layer's browser-side behaviors look for.</summary>
internal static class PopupMarkerAttributes
{
    /// <summary>Represents the marker of a popup element the focus manager may focus: <c>data-base-ui-focusable</c>.</summary>
    public const string Focusable = "data-base-ui-focusable";

    /// <summary>Represents the marker of a trigger that opens on click: <c>data-base-ui-click-trigger</c>.</summary>
    public const string ClickTrigger = "data-base-ui-click-trigger";

    /// <summary>Represents the marker of a portal node: <c>data-base-ui-portal</c>.</summary>
    public const string Portal = "data-base-ui-portal";

    /// <summary>Represents the marker of a focus guard: <c>data-base-ui-focus-guard</c>.</summary>
    public const string FocusGuard = "data-base-ui-focus-guard";

    /// <summary>Represents the marker of the internal backdrop: <c>data-base-ui-inert</c>.</summary>
    public const string Inert = "data-base-ui-inert";
}
