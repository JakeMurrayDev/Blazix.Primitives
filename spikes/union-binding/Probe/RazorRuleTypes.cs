using System.Runtime.CompilerServices;

namespace Probe;

/// <summary>R2: a hand-written union with a string case (what a string-or-literal prop could look like).</summary>
[Union]
public readonly struct StringOrLiteral : IUnion
{
    private readonly string? _text;
    private readonly bool _isLiteral;

    public StringOrLiteral(string value) { _text = value; _isLiteral = false; }
    public StringOrLiteral(TrapFocusLiteral value) { _text = null; _isLiteral = true; }

    public object? Value => _isLiteral ? default(TrapFocusLiteral) : _text;
    public bool HasValue => true;
    public bool TryGetValue(out string value) { value = _text ?? ""; return !_isLiteral; }
    public bool TryGetValue(out TrapFocusLiteral value) { value = default; return _isLiteral; }

    public string Describe() => _isLiteral ? "TrapFocusLiteral" : $"string:{_text}";
}

/// <summary>R3: a plain struct (no [Union]) with a user-defined implicit conversion from string.</summary>
public readonly struct PlainFromString
{
    public PlainFromString(string text) => Text = text;
    public string Text { get; }
    public static implicit operator PlainFromString(string text) => new(text);
}

/// <summary>R4: a plain struct with a user-defined implicit conversion from bool only.</summary>
public readonly struct PlainFromBool
{
    public PlainFromBool(bool flag) => Flag = flag;
    public bool Flag { get; }
    public static implicit operator PlainFromBool(bool flag) => new(flag);
}
