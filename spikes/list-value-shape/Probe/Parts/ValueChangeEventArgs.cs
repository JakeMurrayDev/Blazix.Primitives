namespace Probe.Parts;

// The shape of the library's ValueChangeEventArgs<TValue> (ADR 05): the probe asks only about inference, so the details
// are left out.
public sealed record ValueChangeEventArgs<TValue>(TValue Value);
