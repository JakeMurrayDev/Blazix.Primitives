namespace Blazix.Primitives.Interop;

// ADR 04 §1 (scroll-lock row) and Amendments 2026-10-27 (the scroll-lock bullet, chosen by the user 2026-10-27): the twin
// takes Enabled and attaches to the reference element (UTL-DOM-11: the locked document is the element's). Enabled is
// update-carried, a round trip after the commit on Server (UTL-DOM-12's dated note; ADR 01's "JS-owned values that
// originate in C# state" row).
/// <summary>Holds the options of the <c>scroll-lock</c> module's handle.</summary>
internal sealed record ScrollLockOptions : TwinOptions
{
    /// <summary>Gets a value indicating whether the handle holds a lock on its element's document.</summary>
    public bool Enabled { get; init; }
}
