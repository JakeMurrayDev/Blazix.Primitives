namespace Blazix.Primitives;

// ADR 08 §3 and its Amendments 2026-10-21: the one shared enum for every Base UI prop typed 'horizontal' | 'vertical'.
// The members are the PascalCase of the literals; there is no sentinel member (a nullable Orientation is "not supplied").
/// <summary>Specifies the axis along which an element extends.</summary>
public enum Orientation
{
    /// <summary>The element extends along the horizontal axis.</summary>
    Horizontal,

    /// <summary>The element extends along the vertical axis.</summary>
    Vertical,
}
