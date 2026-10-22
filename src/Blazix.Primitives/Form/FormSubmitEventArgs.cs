namespace Blazix.Primitives;

// FRM-API-03/04, FRM-EVT-02..04/07 (ADR 08 Amendments 2026-10-22): the argument of Form.OnSubmit and Form.OnFormSubmit.
// Details is a GenericEventDetails (no cancel, no propagation control) whose Event is the module's snapshot of the native
// submit, default prevented (FRM-EVT-03).
/// <summary>Provides data for a form submission that passed validation.</summary>
public sealed class FormSubmitEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="FormSubmitEventArgs"/> class.</summary>
    /// <param name="values">The values of the form's named fields.</param>
    /// <param name="details">The details of the submission.</param>
    public FormSubmitEventArgs(FormValues values, GenericEventDetails details)
    {
        Values = values;
        Details = details;
    }

    /// <summary>Gets the values of the form's named fields.</summary>
    /// <value>
    /// The value of every enabled, mounted field that has a name, by name, in the order the fields registered: a
    /// <see langword="string"/> for a <see cref="FieldControl"/> or an <see cref="Input"/>.
    /// </value>
    public FormValues Values { get; }

    /// <summary>Gets the details of the submission.</summary>
    /// <value>The reason, <see cref="EventReason.None"/>, and the native <c>submit</c> event, whose default is prevented.</value>
    public GenericEventDetails Details { get; }

    // FRM-EVT-02 (design point F4 of M2 track 2, pending the orchestrator): Base UI's preventDefault() in onSubmit.
    /// <summary>Gets a value indicating whether <see cref="PreventNativeSubmit"/> was called.</summary>
    /// <value><see langword="true"/> when the browser will not submit the form; otherwise, <see langword="false"/>.</value>
    public bool NativeSubmitPrevented { get; private set; }

    /// <summary>Prevents the browser from submitting the form after <see cref="Form.OnSubmit"/> returns.</summary>
    /// <remarks>
    /// Call it from <see cref="Form.OnSubmit"/> to handle the submission yourself, for example to send the values with
    /// <see cref="System.Net.Http.HttpClient"/>. Giving <see cref="Form.OnFormSubmit"/> has the same effect.
    /// </remarks>
    public void PreventNativeSubmit() => NativeSubmitPrevented = true;
}
