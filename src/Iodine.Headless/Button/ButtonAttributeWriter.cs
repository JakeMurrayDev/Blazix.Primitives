using System.Globalization;
using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// UBT-ALG-03 (button-behavior.md §5), with UBT-DOM-01..08 and UBT-ARIA-01..05 as its observable rows; ADR 04 §1: the
// `button` module writes no attribute, so every button-like part renders these from C#, also in prerender (UBT-DOM-11).
// Callers: Button (F explicit, composite false, T 0); CollapsibleTrigger (M1 track 6) and Toggle (M3) call it with F
// unspecified (null) and composite as they resolve it; composite items (M2) and ToolbarButton pass composite true;
// ToolbarInput (M3) calls the input item variant. The consumer's own
// `type`, `role`, `tabindex`, `disabled` and `aria-disabled` override these because the pipeline merges the consumer's
// attributes after the part's (UBT-DOM-09).
/// <summary>Writes the attributes of the button mechanism: <c>type</c>, <c>role</c>, <c>tabindex</c>, <c>disabled</c> and <c>aria-disabled</c>.</summary>
internal static class ButtonAttributeWriter
{
    private const string ZeroTabIndex = "0";
    private const string RemovedTabIndex = "-1";

    /// <summary>Adds the button attributes of one render to <paramref name="attributes"/>.</summary>
    /// <param name="attributes">The part-supplied attribute list of the render.</param>
    /// <param name="native">
    /// <see langword="true"/> when the element is declared a native <c>&lt;button&gt;</c>, which adds <c>type="button"</c>;
    /// <see langword="false"/> for any other element, which adds <c>role="button"</c>.
    /// </param>
    /// <param name="disabled"><see langword="true"/> when the element ignores user interaction.</param>
    /// <param name="focusableWhenDisabled">
    /// <see langword="true"/> to keep a disabled element focusable, <see langword="false"/> to remove it from the tab
    /// order, or <see langword="null"/> when the part does not specify it, in which case a composite item counts as
    /// focusable for <c>aria-disabled</c>.
    /// </param>
    /// <param name="composite">
    /// <see langword="true"/> for an item of a composite widget, whose <c>tabindex</c> the widget supplies.
    /// </param>
    /// <param name="tabIndex">The <c>tabindex</c> outside a composite widget; the default is 0.</param>
    public static void Write(AttributeList attributes, bool native, bool disabled, bool? focusableWhenDisabled, bool composite, int tabIndex = 0)
    {
        bool focusableComposite = composite && focusableWhenDisabled != false;
        bool nonFocusableComposite = composite && focusableWhenDisabled == false;

        if (native)
        {
            attributes.Set("type", "button");
        }
        else
        {
            attributes.Set("role", "button");
        }

        if (!composite)
        {
            bool removed = !native && disabled && focusableWhenDisabled != true;
            attributes.Set("tabindex", removed ? RemovedTabIndex : TabIndexText(tabIndex));
        }

        if ((native && (focusableWhenDisabled == true || focusableComposite)) || (!native && disabled))
        {
            attributes.Set("aria-disabled", disabled ? "true" : "false");
        }

        // A false boolean attribute is omitted by the renderer, so only a disabled element carries it.
        if (native && (focusableWhenDisabled != true || nonFocusableComposite) && disabled)
        {
            attributes.Set("disabled", true);
        }
    }

    // UBT-DOM-12, TLB-DOM-07, TLB-ARIA-04, TLB-DOM-11: Toolbar.Input runs UBT-ALG-03 with composite true and isNativeButton
    // false (toolbar/input/ToolbarInput.tsx:40-45), which leaves aria-disabled alone; it merges those props after the
    // consumer's, so the part calls this on the merged attributes and a consumer's aria-disabled loses while disabled.
    /// <summary>Adds the attributes of a toolbar input: <c>aria-disabled="true"</c> while disabled, and nothing else.</summary>
    /// <param name="attributes">The attribute list of the render; the part's merged attributes, so the value wins.</param>
    /// <param name="disabled"><see langword="true"/> when the input ignores user interaction.</param>
    public static void WriteInputItem(AttributeList attributes, bool disabled)
    {
        if (disabled)
        {
            attributes.Set("aria-disabled", "true");
        }
    }

    private static string TabIndexText(int tabIndex)
    {
        return tabIndex switch
        {
            0 => ZeroTabIndex,
            -1 => RemovedTabIndex,
            _ => tabIndex.ToString(CultureInfo.InvariantCulture),
        };
    }
}
