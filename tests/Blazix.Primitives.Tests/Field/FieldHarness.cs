using Blazix.Primitives.Interop;
using Microsoft.Extensions.Time.Testing;

namespace Blazix.Primitives.Tests;

/// <summary>
/// The L1 stand-in of a field with one text control: the C# field scope and validation service with no renderer, the
/// browser's native state supplied by the test (the payload the form module reports), a form scope whose submission the
/// test drives, and a <see cref="FakeTimeProvider"/>. <see cref="Change"/> and <see cref="Blur"/> run what an uncontrolled
/// <see cref="FieldControl"/> runs for the module's input and blur reports (FLD-EVT-07, FLD-FOC-02).
/// </summary>
internal sealed class FieldHarness
{
    public FieldHarness(
        ValidationMode? mode = null,
        Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>? validate = null,
        int debounce = 0,
        bool inForm = false,
        string? name = null,
        bool? dirty = null,
        bool invalid = false)
    {
        Field = new FieldRootContext("base-ui-1", Clock, action => { action(); return Task.CompletedTask; }, exception => { Exceptions.Add(exception); return Task.CompletedTask; });
        Input = new FieldValidationInput(input => SentMessages.Add(input.OwnMessage));
        if (inForm)
        {
            Field.SetForm(Form);
        }

        Field.SetParameters(name, false, invalid, dirty, null, validate, mode, debounce);
    }

    public FakeTimeProvider Clock { get; } = new();

    public List<Exception> Exceptions { get; } = [];

    public TestFormScope Form { get; } = new();

    public FieldRootContext Field { get; }

    public FieldValidationInput Input { get; }

    public List<string?> SentMessages { get; } = [];

    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets the verdict the parts render: data-valid, data-invalid or neither.</summary>
    public bool? Valid => Field.State.Valid;

    /// <summary>Gets a value indicating whether the control renders aria-invalid="true".</summary>
    public bool AriaInvalid => Field.State.Valid == false && !Field.Disabled;

    /// <summary>Mounts the control: the field's validation input and its registration (FLD-FRM-01).</summary>
    public FieldHarness Mount(string id = "control", NativeValidity? native = null, string value = "")
    {
        Value = value;
        Input.Report(native ?? NativeValidity.Default);
        Field.Validation.InputRef = Input;
        Field.RegisterControl(this, new FieldControlRegistration(id, null, null, () => Value, id, null));
        return this;
    }

    /// <summary>Unmounts the control.</summary>
    public void Unmount()
    {
        Field.RegisterControl(this, null);
        Field.Validation.InputRef = null;
    }

    /// <summary>Runs the uncontrolled control's reaction to an input report.</summary>
    /// <param name="value">The value the input reports.</param>
    /// <param name="native">The native state, or the valid state.</param>
    /// <param name="ownMessageLost"><see langword="true"/> when someone else replaced the field's own message.</param>
    public void Change(string value, NativeValidity? native = null, bool ownMessageLost = false)
    {
        Value = value;
        Input.Report(Stamp(native ?? NativeValidity.Default, ownMessageLost));
        using FieldRootContext.BatchScope batch = Field.Batch();
        Field.SetDirty(!Equals(value, Field.ValidityData.InitialValue ?? string.Empty));
        Field.SetFilled(value.Length > 0);
        Field.ClearErrors();
        Field.Validation.Change(value);
    }

    /// <summary>Runs the control's reaction to a blur report.</summary>
    public void Blur(NativeValidity? native = null)
    {
        if (native is not null)
        {
            Input.Report(Stamp(native, ownMessageLost: false));
        }

        using FieldRootContext.BatchScope batch = Field.Batch();
        Field.SetTouched(true);
        Field.SetFocused(this, false, false);
        if (Field.ValidationMode == ValidationMode.OnBlur)
        {
            Field.Observe(Field.Validation.CommitAsync(Value));
        }
    }

    // The module reports whether the field's own message is still installed: it is while the field last sent one.
    private NativeValidity Stamp(NativeValidity native, bool ownMessageLost) =>
        native with { OwnMessageInstalled = !ownMessageLost && SentMessages.Count > 0 && SentMessages[^1] is not null };

    /// <summary>Submits the form as Form does: the counter, then every registered field's imperative validation in order.</summary>
    /// <returns><see langword="true"/> when no registered field is invalid afterwards.</returns>
    public bool Submit()
    {
        Form.SubmitCount++;
        foreach (string id in Form.Registry.Ids.ToArray())
        {
            if (Form.Registry.TryGet(id, out FieldRegistryEntry entry))
            {
                Field.Observe(entry.Validate());
            }
        }

        foreach (string id in Form.Registry.Ids)
        {
            if (Form.Registry.TryGet(id, out FieldRegistryEntry entry) && entry.ValidityData.State.Valid == false)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Native states the browser would report for an input.</summary>
internal static class Native
{
    public static NativeValidity Valid { get; } = NativeValidity.Default;

    public static NativeValidity ValueMissing { get; } = new() { ValueMissing = true, ValidationMessage = "Please fill out this field." };

    public static NativeValidity TypeMismatch { get; } = new() { TypeMismatch = true, ValidationMessage = "Please enter an email address." };

    public static NativeValidity ForeignCustom(string message) => new() { CustomError = true, ValidationMessage = message };
}

/// <summary>A form scope whose errors, mode and submissions the test controls.</summary>
internal sealed class TestFormScope : IFormScope
{
    private readonly List<Action> listeners = [];

    public Dictionary<string, string[]> Errors { get; } = new(StringComparer.Ordinal);

    public FieldRegistry Registry { get; } = new();

    public ValidationMode ValidationMode { get; set; } = ValidationMode.OnSubmit;

    public int SubmitCount { get; set; }

    public string? FormToken => "form";

    public bool TryGetErrors(string name, out IReadOnlyList<string> errors)
    {
        if (Errors.TryGetValue(name, out string[]? messages) && messages.Any(message => message.Length > 0))
        {
            errors = messages;
            return true;
        }

        errors = [];
        return false;
    }

    public void ClearErrors(string? name)
    {
        if (!string.IsNullOrEmpty(name) && Errors.Remove(name))
        {
            Notify();
        }
    }

    public void SetErrors(string name, params string[] messages)
    {
        Errors[name] = messages;
        Notify();
    }

    public void AddListener(Action listener) => listeners.Add(listener);

    public void RemoveListener(Action listener) => listeners.Remove(listener);

    private void Notify()
    {
        foreach (Action listener in listeners.ToArray())
        {
            listener();
        }
    }
}
