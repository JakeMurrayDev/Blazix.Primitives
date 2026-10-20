using System.Runtime.CompilerServices;

namespace Probe;

/// <summary>The named literal case of <see cref="ModalValue"/> (Base UI's <c>modal="trap-focus"</c>).</summary>
public readonly struct TrapFocusLiteral;

/// <summary>
/// Case D: a hand-written union per ADR 05 §4 / ADR 08 §3 ("Boolean-or-literal props").
/// Typed fields, no <c>object</c> storage; <see cref="Value"/> exists only because the proposal makes it mandatory.
/// </summary>
[Union]
public readonly struct ModalValue : IUnion
{
    /// <summary>Counts reads of the boxing <see cref="Value"/> getter so the harness can prove the non-boxing path is taken.</summary>
    public static int ValueReads;

    private readonly bool _bool;
    private readonly bool _isTrapFocus;

    public ModalValue(bool value)
    {
        _bool = value;
        _isTrapFocus = false;
    }

    public ModalValue(TrapFocusLiteral value)
    {
        _bool = true;
        _isTrapFocus = true;
    }

    /// <summary>The named literal case, written as <c>Modal="@ModalValue.TrapFocus"</c>.</summary>
    public static ModalValue TrapFocus => new(default(TrapFocusLiteral));

    /// <summary>Mandatory on a [Union] type (proposal §custom unions). Boxes; the port never reads it.</summary>
    public object Value
    {
        get
        {
            ValueReads++;
            return _isTrapFocus ? (object)default(TrapFocusLiteral) : _bool;
        }
    }

    /// <summary>Optional non-boxing access pattern (proposal): HasValue + TryGetValue(out T) per case.</summary>
    public bool HasValue => true;

    public bool TryGetValue(out bool value)
    {
        value = _bool;
        return !_isTrapFocus;
    }

    public bool TryGetValue(out TrapFocusLiteral value)
    {
        value = default;
        return _isTrapFocus;
    }

    /// <summary>ADR 08 §3 asks for this; the compiler's union conversion from <c>bool</c> would exist without it.</summary>
    public static implicit operator ModalValue(bool value) => new(value);

    public string Describe() => TryGetValue(out bool b) ? $"bool:{b}" : "TrapFocus";
}
