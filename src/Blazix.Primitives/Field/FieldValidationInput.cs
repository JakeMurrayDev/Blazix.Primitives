using Blazix.Primitives.Interop;

namespace Blazix.Primitives;

// FFI §2 (validation input(s)): `validation.inputRef` for a control with one native input, `registeredInputs` for a group
// (tracks 6 and 8). The browser's ValidityState is the form module's: the input holds the state its last report carried
// (FFI-ALG-02, with the field's own message read out, FFI-FRM-09), and the custom message the field wants installed, which
// its owner sends to the module (the module writes it, FFI-FRM-09/10).
/// <summary>One native input whose validity a field reads: the state the browser last reported for it and the field's own message for it.</summary>
internal sealed class FieldValidationInput
{
    private readonly Action<FieldValidationInput> ownMessageChanged;
    private string? flushedMessage;

    /// <summary>Initializes a new instance of the <see cref="FieldValidationInput"/> class.</summary>
    /// <param name="ownMessageChanged">Sends the field's own message to the browser-side behavior after a commit changed it.</param>
    public FieldValidationInput(Action<FieldValidationInput> ownMessageChanged)
    {
        this.ownMessageChanged = ownMessageChanged;
    }

    /// <summary>Gets the native validity the input last reported.</summary>
    /// <value>The state with the field's own message taken out; <see cref="NativeValidity.Default"/> before the first report.</value>
    public NativeValidity State { get; private set; } = NativeValidity.Default;

    /// <summary>Gets or sets the id of the element focused when a form blocks a submission on this input's field.</summary>
    /// <value>The control element's id, or <see langword="null"/>.</value>
    public string? ControlId { get; set; }

    /// <summary>Gets the custom message the field wants installed on the input.</summary>
    /// <value>The message, or <see langword="null"/> when the field has none installed.</value>
    public string? OwnMessage { get; private set; }

    // findRepresentativeInput reads `input.validity.valid` before the field clears its own message.
    /// <summary>Gets a value indicating whether the input is valid with the field's own message counted.</summary>
    /// <value><see langword="true"/> when the native state is valid and the field installed no message.</value>
    public bool IsCurrentlyValid => State.Valid && string.IsNullOrEmpty(OwnMessage);

    // FFI-FRM-09: a message someone else replaced or withdrew is the field's no longer, so a commit that wants it installed
    // installs it again, as Base UI's commit re-installs over a foreign message.
    /// <summary>Records the native validity an input reported.</summary>
    /// <param name="state">The state with the field's own message taken out.</param>
    public void Report(NativeValidity state)
    {
        State = state;
        if (!state.OwnMessageInstalled)
        {
            flushedMessage = null;
        }
    }

    /// <summary>Records the custom message the field wants installed.</summary>
    /// <param name="message">The message, or <see langword="null"/> to withdraw it.</param>
    public void SetOwnMessage(string? message) => OwnMessage = message;

    /// <summary>Sends the field's own message when it changed since the last flush.</summary>
    public void Flush()
    {
        if (string.Equals(OwnMessage, flushedMessage, StringComparison.Ordinal))
        {
            return;
        }

        flushedMessage = OwnMessage;
        ownMessageChanged(this);
    }
}
