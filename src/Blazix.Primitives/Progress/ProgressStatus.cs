namespace Blazix.Primitives;

// PRG §3.6 (Base UI's `Progress.Status`, alias ProgressStatus; progress/root/ProgressRoot.tsx:106). ADR 08 §3 Enums row:
// family-prefixed because it is one family's, members are the PascalCase of the literals, no sentinel. The generator's
// default projection would write `data-status`; PRG-DOM-02 maps the field to one presence attribute instead
// (ProgressStatusMapping).
/// <summary>Specifies the status of a progress bar, which every part of the family exposes as a data attribute.</summary>
public enum ProgressStatus
{
    /// <summary>The progress is unknown: the value is <see langword="null"/> or not a finite number.</summary>
    Indeterminate,

    /// <summary>The value is within the range and below the maximum.</summary>
    Progressing,

    /// <summary>The value, limited to the range, has reached the maximum.</summary>
    Complete,
}
