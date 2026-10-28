namespace Blazix.Primitives;

// PSH-FOC-08; utils/closePart.tsx:12-37 (useClosePartCount, useClosePartRegistration). The Popup cascades it fixed (ADR 16
// rule 9); every Close inside the Popup registers once and unregisters when it leaves, and the count never goes below 0.
// Dialog's Popup composes the same count (ADR 04 Amendments 2026-10-27: closePart is track 5's, track 8 does not build it).
/// <summary>Counts the close parts mounted inside a popup, which decides whether the popup traps focus.</summary>
/// <param name="changed">Called after the count crosses zero in either direction.</param>
internal sealed class PopoverClosePartCount(Action changed)
{
    private int count;

    /// <summary>Gets a value indicating whether at least one close part is mounted inside the popup.</summary>
    public bool HasClosePart => count > 0;

    /// <summary>Registers a close part.</summary>
    /// <returns>The registration; disposing it unregisters the part once.</returns>
    public IDisposable Register()
    {
        count++;
        if (count == 1)
        {
            changed();
        }

        return new Registration(this);
    }

    private void Unregister()
    {
        count = Math.Max(0, count - 1);
        if (count == 0)
        {
            changed();
        }
    }

    private sealed class Registration(PopoverClosePartCount owner) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                owner.Unregister();
            }
        }
    }
}
