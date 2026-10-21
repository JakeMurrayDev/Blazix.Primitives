using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// UBT-EDGE-01..03 (button-behavior.md §7), BTN-EDGE-01. The `button` module runs the declared-mode check only while
// `ButtonOptions.DevelopmentChecks` is on (the development warnings switch) and reports a mismatch through
// `ButtonTwin.NativeButtonMismatched`; a part subscribes and calls `ReportNativeButtonMismatch`. Base UI's
// `createLogOnce` keeps one set per page, so the seen set is a scoped service (one per Server circuit, one per
// WebAssembly application), registered by `AddBlazixPrimitives()` like `PartIdGenerator`, never a static, which on
// Server would be one per process. CollapsibleTrigger (M1 track 6) and Toggle (M3) call the same method.
/// <summary>Writes the development messages of button-like parts, each distinct message once per session.</summary>
internal sealed class ButtonDevelopmentLog
{
    // UBT-EDGE-01 in the port's words.
    internal const string NativeMismatchMessage =
        "Blazix: A component that acts as a button expected a native `<button>` because the `NativeButton` parameter is `true`. "
        + "Rendering a non-`<button>` removes native button semantics, which can impact forms and accessibility. "
        + "Use a real `<button>` in the `Render` parameter, or set `NativeButton` to `false`.";

    // UBT-EDGE-02 in the port's words.
    internal const string NonNativeMismatchMessage =
        "Blazix: A component that acts as a button expected a non-`<button>` because the `NativeButton` parameter is `false`. "
        + "Rendering a `<button>` keeps native behavior while the library applies non-native attributes and handlers, which can add "
        + "unintended extra attributes (such as `role` or `aria-disabled`). "
        + "Use a non-`<button>` in the `Render` parameter, or set `NativeButton` to `true`.";

    private readonly HashSet<string> written = new(StringComparer.Ordinal);

    /// <summary>Writes the message for an element that does not match its declared mode, unless this session already has.</summary>
    /// <param name="logger">The logger of the part that rendered the element.</param>
    /// <param name="expectedNative">
    /// <see langword="true"/> when native mode was declared and the element is not a <c>&lt;button&gt;</c>;
    /// <see langword="false"/> when non-native mode was declared and the element is one.
    /// </param>
    public void ReportNativeButtonMismatch(ILogger logger, bool expectedNative)
    {
        string message = expectedNative ? NativeMismatchMessage : NonNativeMismatchMessage;
        if (written.Add(message))
        {
            DevelopmentWarnings.Write(logger, message);
        }
    }
}
