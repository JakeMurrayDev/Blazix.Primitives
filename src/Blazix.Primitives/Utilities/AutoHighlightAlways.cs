namespace Blazix.Primitives;

// ADR 08 §3 (boolean-or-literal props): Base UI's `autoHighlight="always"` literal, the named case of AutoHighlightValue.
/// <summary>Represents the <c>always</c> case of <see cref="AutoHighlightValue"/>: the first item is highlighted even when the query is empty.</summary>
/// <remarks>Write it as <see cref="AutoHighlightValue.Always"/>, for example <c>AutoHighlight="@AutoHighlightValue.Always"</c>.</remarks>
public readonly struct AutoHighlightAlways;
