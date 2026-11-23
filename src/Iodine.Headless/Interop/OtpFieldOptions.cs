namespace Iodine.Headless.Interop;

// ADR 04 §1 (the `otp-field` component module), Amendments 2026-11-18 (the scope, `edit`, variant (ii)) and 2026-11-19 (the
// design points of M6 track 7, chosen by the user 2026-11-19); issue 278. One handle on the Root element (design point 6 (a)):
// the slots are found by their registered ids, in index order.
/// <summary>The options of the <c>otp-field</c> module attached to a one-time code field's root.</summary>
internal sealed record OtpFieldOptions : TwinOptions
{
    // OTP-STR-03, design point 6 (a): the slots' ids in index order, as the composite registry published them.
    /// <summary>Gets the ids of the slots, in index order.</summary>
    /// <value>The ids; the module finds each slot by its id.</value>
    public IReadOnlyList<string> Slots { get; init; } = [];

    // OTP-API-01, OTP-STA-12 (`last` and the end target).
    /// <summary>Gets the number of slots, the root's <c>Length</c>.</summary>
    public int Length { get; init; }

    // OTP-DOM-08: the module owns the slots' `value` from attach and writes slot i as the value's code unit i (C# → JS).
    /// <summary>Gets the committed, normalized value.</summary>
    public string Value { get; init; } = string.Empty;

    // The revision rule (ADR 04 Amendments 2026-11-18, decision 6): the revision of the last edit report C# applied.
    /// <summary>Gets the revision of the last edit report C# applied, echoed so the module ignores an older update.</summary>
    /// <value>The revision; 0 before any report.</value>
    public int Revision { get; init; }

    // Design point 4 (a): a rejected optimistic act; the module applies the update even when its revision is older.
    /// <summary>Gets a value indicating whether the update rolls back an optimistic act.</summary>
    /// <value><see langword="true"/> for a rollback; <see langword="null"/> otherwise, which is not sent.</value>
    public bool? Rollback { get; init; }

    // OTP-FOC-01, OTP-FOC-02, design point 5 (a): the module writes the roving tabindex after attach.
    /// <summary>Gets the index of the slot in the tab order.</summary>
    public int ActiveIndex { get; init; }

    // OTP-FOC-10: a focus move C# queued after a committed value, performed once per revision.
    /// <summary>Gets the queued focus move, or <see langword="null"/> when none is pending.</summary>
    public OtpFieldFocusRequest? Focus { get; init; }

    /// <summary>Gets a value indicating whether the field is disabled; <see langword="null"/> when not, which is not sent.</summary>
    public bool? Disabled { get; init; }

    /// <summary>Gets a value indicating whether the field is read-only; <see langword="null"/> when not, which is not sent.</summary>
    public bool? ReadOnly { get; init; }

    // OTP-I18N-01
    /// <summary>Gets a value indicating whether the reading direction is right to left; <see langword="null"/> when not.</summary>
    public bool? Rtl { get; init; }

    // Variant (ii) (decision 6 (a)); design point 3 (a): built-in validation, no custom normalization, the field's own value.
    /// <summary>Gets a value indicating whether a typed character the slot pattern accepts is written and advanced at once.</summary>
    /// <value><see langword="true"/> while the optimistic advance applies; <see langword="null"/> otherwise, which is not sent.</value>
    public bool? Optimistic { get; init; }

    // Design point 3 (a): OTP-STA-05's single-character pattern; the module tests one character, C# re-validates.
    /// <summary>Gets the single-character pattern of the validation type, or <see langword="null"/> for none.</summary>
    public string? SlotPattern { get; init; }

    // OTP-EVT-13, design point 7 (a): an increase submits the owning form once.
    /// <summary>Gets the submit revision; each increase asks the module to submit the owning form.</summary>
    public int SubmitRevision { get; init; }

    // OTP-API-13, OTP-EVT-13: the form id the submit resolves first.
    /// <summary>Gets the id of the form that owns the field, or <see langword="null"/>.</summary>
    public string? Form { get; init; }

    // Design point 1 (a): the points beside `edit` whose C# handler chains exist, so the module asks them on WebAssembly.
    /// <summary>Gets the names of the decision points C# answers besides <c>edit</c>, or <see langword="null"/> for none.</summary>
    public IReadOnlyList<string>? Points { get; init; }

    // OTP-EDGE-05: the unreadable-paste report is sent only while the development warnings are on.
    /// <summary>Gets a value indicating whether the module reports what the development warnings need.</summary>
    public bool? DevelopmentChecks { get; init; }
}

// OTP-FOC-10 (OTPFieldRoot.tsx:106-109,207-215): the queued focus, applied when the rendered value equals the queued one.
/// <summary>A focus move C# asks the module to perform: focus and select a slot.</summary>
/// <param name="Index">The slot to focus.</param>
/// <param name="Revision">The request's revision; the module performs each revision once.</param>
internal sealed record OtpFieldFocusRequest(int Index, int Revision);
