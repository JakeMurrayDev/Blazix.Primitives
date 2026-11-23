namespace Blazix.Primitives;

// DLG-EDGE-02/03 (dialog/root/DialogRootContext.ts:13-17, dialog/portal/DialogPortalContext.ts:8-12): Base UI's wording with
// the C# names, built from the family name each part passes (plan §Settled, the M3 form). Thrown in every build.
/// <summary>Provides the messages of a dialog-family part placed outside its root or its portal.</summary>
internal static class DialogParts
{
    /// <summary>Creates the exception of a part rendered outside any root of its family.</summary>
    /// <param name="familyName">The family's name, such as <c>Dialog</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingRoot(string familyName) =>
        new($"Blazix: The `{familyName}Root` context is missing. {familyName} parts must be placed within `<{familyName}Root>`.");

    /// <summary>Creates the exception of a portaled part rendered outside its family's portal.</summary>
    /// <param name="familyName">The family's name, such as <c>Dialog</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingPortal(string familyName) =>
        new($"Blazix: `<{familyName}Portal>` is missing.");
}
