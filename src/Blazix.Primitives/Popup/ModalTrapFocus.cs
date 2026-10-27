namespace Blazix.Primitives;

// ADR 08 §3 (boolean-or-literal props, Amendments 2026-10-27): Base UI's `modal="trap-focus"` literal, the named case of
// ModalValue.
/// <summary>Represents the <c>trap-focus</c> case of <see cref="ModalValue"/>: focus is trapped while page scroll and outside pointer interaction stay free.</summary>
/// <remarks>Write it as <see cref="ModalValue.TrapFocus"/>, for example <c>Modal="@ModalValue.TrapFocus"</c>.</remarks>
public readonly struct ModalTrapFocus;
