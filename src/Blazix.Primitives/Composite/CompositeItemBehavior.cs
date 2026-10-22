using System.Globalization;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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
// - Option (i) of ADR 04's note of 2026-10-22, pending the user: the consumer's onkeydown on the item. Base UI runs it
//   before the root's key handling, on the same synthetic event, so its preventBaseUIHandler() also stops the root's
//   handling (merge-props/mergeProps.ts:210-231). A key the root handles is stopped at the root and never reaches Blazor's
//   dispatcher, so a part that passes its own key handler declines the consumer's (ADR 02 Amendments 2026-10-21) and the
//   root runs it: inside the navigate decision on WebAssembly, from the module's handled-key report on Server. Any other
//   key reaches the part's own handler, which skips a key a decision already ran (the one-shot marker).
/// <summary>The composite item mechanism of one part: its registration with the nearest composite root and its initial <c>tabindex</c>.</summary>
internal sealed class CompositeItemBehavior : IDisposable
{
    private readonly CompositeRootContext root;
    private readonly EventCallback onKeyDown;
    private AttributeValue consumerKeyDown;
    private DecidedKey? decidedKeyDown;
    private string? elementId;
    private bool registered;

    /// <summary>Initializes a new instance of the <see cref="CompositeItemBehavior"/> class.</summary>
    /// <param name="context">The cascaded registry of the nearest composite root, or <see langword="null"/>.</param>
    /// <param name="part">The name of the item's part, for the error of an item outside a root.</param>
    /// <param name="onKeyDown">
    /// The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>; the default when the part leaves
    /// the consumer's key handler on its element.
    /// </param>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public CompositeItemBehavior(CompositeRootContext? context, string part, EventCallback onKeyDown = default)
    {
        root = CompositeRootContext.Require(context, part);
        this.onKeyDown = onKeyDown;
        Registration = new CompositeItemRegistration(string.Empty, null);
    }

    /// <summary>Gets the registration the root sends for this item.</summary>
    /// <value>The item's element id and its consumer <c>tabindex</c>.</value>
    public CompositeItemRegistration Registration { get; private set; }

    /// <summary>Gets a value indicating whether the root runs the consumer's key handler of this item.</summary>
    /// <value><see langword="true"/> when the part declines the consumer's <c>onkeydown</c> and the consumer supplied one.</value>
    internal bool HasKeyHandler => onKeyDown.HasDelegate && consumerKeyDown.IsHandler;

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

    /// <summary>Finds the consumer's key handler, which the part declines when it passed its own.</summary>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void SetConsumerAttributes(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        bool had = HasKeyHandler;
        consumerKeyDown = default;
        if (onKeyDown.HasDelegate && additionalAttributes is not null && additionalAttributes.TryGetValue("onkeydown", out object? value))
        {
            AttributeValue handler = AttributeValue.FromObject(value);
            if (MergeProps.IsHandler("onkeydown", handler))
            {
                consumerKeyDown = handler;
            }
        }

        if (registered && had != HasKeyHandler)
        {
            root.RegistrationChanged();
        }
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the item's element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="false"/> for the key handler when the part runs it itself.</returns>
    public bool ForwardsConsumerHandler(string eventName)
    {
        return !(onKeyDown.HasDelegate && string.Equals(eventName, "onkeydown", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Adds the item's initial <c>tabindex</c>, and the part's key handler when the consumer supplied one, to a render.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("tabindex", "-1");
        if (HasKeyHandler)
        {
            attributes.Set("onkeydown", onKeyDown);
        }
    }

    /// <summary>Runs the consumer's <c>onkeydown</c> handler from the part's own, unless a decision already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        DecidedKey? pending = decidedKeyDown;
        decidedKeyDown = null;
        if (!consumerKeyDown.IsHandler || (pending is { } key && key == DecidedKey.From(e)))
        {
            return Task.CompletedTask;
        }

        return consumerKeyDown.InvokeAsync(e);
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

    /// <summary>Runs the consumer's key handler for a key the root handles; the root observes the task.</summary>
    /// <param name="args">The key event, shared with the root's own handler so a veto in either is seen.</param>
    /// <returns>The consumer handler's task.</returns>
    internal Task InvokeKeyHandler(KeyboardEventArgs args) => consumerKeyDown.InvokeAsync(args);

    /// <summary>Records that a decision ran the consumer's key handler for a key Blazor dispatches afterwards.</summary>
    /// <param name="args">The key event.</param>
    internal void MarkDecided(KeyboardEventArgs args) => decidedKeyDown = DecidedKey.From(args);

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

    /// <summary>The key event a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedKey(string Key, string Code, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedKey From(KeyboardEventArgs e) => new(e.Key, e.Code, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}
