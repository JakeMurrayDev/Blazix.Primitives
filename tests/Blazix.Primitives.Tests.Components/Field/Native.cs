using Blazix.Primitives.Interop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>Native states the browser would report for an input, as the form module's payload carries them.</summary>
internal static class Native
{
    public static NativeValidity Valid { get; } = NativeValidity.Default;

    public static NativeValidity ValueMissing { get; } = new() { ValueMissing = true, ValidationMessage = "Please fill out this field." };

    public static NativeValidity TypeMismatch { get; } = new() { TypeMismatch = true, ValidationMessage = "Please enter an email address." };
}
