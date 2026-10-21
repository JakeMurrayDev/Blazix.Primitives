namespace Blazix.Primitives.Utilities;

// FFI-ARIA-08, MTR-STA-03 (useRegisteredLabelId): a label publishes its id to its scope and withdraws it when it goes away
// only if the scope still holds that same id, so an older label's cleanup never clears a newer label's id. The root renders
// its `aria-labelledby` from Id and re-renders when Changed runs.
/// <summary>Holds the id of the label a root is labelled by, published by the label part and withdrawn when it goes away.</summary>
internal sealed class LabelIdSlot
{
    /// <summary>Gets the id of the most recently published label.</summary>
    /// <value>The id, or <see langword="null"/> while no label is published.</value>
    public string? Id { get; private set; }

    /// <summary>Gets or sets the action that runs after <see cref="Id"/> changed.</summary>
    /// <value>The action, or <see langword="null"/>.</value>
    public Action? Changed { get; set; }

    /// <summary>Publishes a label's id, replacing the id of an older label.</summary>
    /// <param name="id">The label's id.</param>
    public void Publish(string id)
    {
        if (string.Equals(Id, id, StringComparison.Ordinal))
        {
            return;
        }

        Id = id;
        Changed?.Invoke();
    }

    /// <summary>Withdraws a label's id when it is still the tracked one.</summary>
    /// <param name="id">The id the label published.</param>
    public void Withdraw(string id)
    {
        if (Id is null || !string.Equals(Id, id, StringComparison.Ordinal))
        {
            return;
        }

        Id = null;
        Changed?.Invoke();
    }
}
