using System.Globalization;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// The composite item as a part uses it (CMP §6.4 and §6.5; useCompositeItem.ts, useCompositeListItem.ts). Composed into
// the part (RadioRoot in a group in M2; the M3 items), never a nested component (ADR 16 rule 10).
// - It registers the id its part renders (the generated id, or the consumer's) with the consumer's tabindex, in the batch
//   that creates the part, and unregisters on dispose (CMP-STR-01, CMP-STA-10).
// - It renders tabindex="-1" on every render (CMP-STA-15): the value under the handover at attach, unchanged afterwards, so
//   Blazor never writes the attribute the module owns from attach (ADR 04 Amendments 2026-10-22, CMP-DOM-01). The
//   pipeline merges the consumer after the part, so a consumer tabindex wins (RAD-DOM-16) and travels in the registration,
//   which the module never writes over.
// - After every render it asks the root to send what changed; a new element under the same id (a substitute that changed
//   its root) is reported, so the module resolves the id again. Under a substitute callback the part has no element
//   reference and a replaced element is found again only on the root's next update.
/// <summary>The composite item mechanism of one part: its registration with the nearest composite root and its initial <c>tabindex</c>.</summary>
internal sealed class CompositeItemBehavior : IDisposable
{
    private readonly CompositeRootContext root;
    private string? elementId;
    private bool registered;

    /// <summary>Initializes a new instance of the <see cref="CompositeItemBehavior"/> class.</summary>
    /// <param name="context">The cascaded registry of the nearest composite root, or <see langword="null"/>.</param>
    /// <param name="part">The name of the item's part, for the error of an item outside a root.</param>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public CompositeItemBehavior(CompositeRootContext? context, string part)
    {
        root = CompositeRootContext.Require(context, part);
        Registration = new CompositeItemRegistration(string.Empty, null);
    }

    /// <summary>Gets the registration the root sends for this item.</summary>
    /// <value>The item's element id and its consumer <c>tabindex</c>.</value>
    public CompositeItemRegistration Registration { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!registered)
        {
            return;
        }

        registered = false;
        root.Unregister(this);
    }

    /// <summary>Registers the item, or records a change of its id or consumer <c>tabindex</c>; called on every parameter set.</summary>
    /// <param name="id">The id the part renders on its element.</param>
    /// <param name="additionalAttributes">The part's unmatched attributes, which may hold the consumer's <c>tabindex</c>.</param>
    public void SetRegistration(string? id, IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        string elementIdValue = id ?? string.Empty;
        string? tabIndex = ConsumerTabIndex(additionalAttributes);
        if (!registered)
        {
            Registration = new CompositeItemRegistration(elementIdValue, tabIndex);
            registered = true;
            root.Register(this);
            return;
        }

        if (!string.Equals(Registration.Id, elementIdValue, StringComparison.Ordinal)
            || !string.Equals(Registration.TabIndex, tabIndex, StringComparison.Ordinal))
        {
            Registration = new CompositeItemRegistration(elementIdValue, tabIndex);
            root.RegistrationChanged();
        }
    }

    /// <summary>Adds the item's initial <c>tabindex</c> to a render.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("tabindex", "-1");
    }

    /// <summary>Reports a replaced element and asks the root to send what changed.</summary>
    /// <param name="element">The part's element, or <see langword="null"/> when it has no element reference.</param>
    /// <returns>A task that completes when the root's browser-side behavior has the change.</returns>
    public Task AfterRenderAsync(ElementReference? element)
    {
        string? current = element?.Id;
        if (current is not null)
        {
            if (elementId is not null && !string.Equals(elementId, current, StringComparison.Ordinal))
            {
                root.ElementReplaced();
            }

            elementId = current;
        }

        return root.FlushAsync();
    }

    private static string? ConsumerTabIndex(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        // Blazor's unmatched-attribute dictionary compares names ignoring case, so `tabIndex` is found too; a lookup
        // allocates nothing on the render path, where enumerating the dictionary through its interface would.
        if (additionalAttributes is null || !additionalAttributes.TryGetValue("tabindex", out object? value))
        {
            return null;
        }

        return value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }
}
