namespace Probe.Parts;

// The shape of the library's ValueChangeEventArgs<T> (ADR 05): the probe asks only about binding and inference, so the
// details are left out.
public readonly record struct ValueChangeEventArgs<T>(T Value);
