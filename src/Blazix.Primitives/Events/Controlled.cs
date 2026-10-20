using System.Collections;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

/// <summary>
/// A controlled/uncontrolled value (EVD-ALG-08, EVD-STA-01..06, UTL-STA-01..06; ADR 05 §1/§2; glossary "Controlled /
/// uncontrolled" and "Request"). The value is controlled iff its parameter was supplied at the first parameter set, and
/// stays so for the lifetime of the part (ADR 05 Amendments 2026-10-18, item 1): supplying or removing it later only
/// raises the EVD-EDGE-01 warning, and a removed controlled value falls back to the initial default (EVD-STA-05).
/// A request runs compute → <c>OnValueChange(args)</c> → stop if canceled → <c>ValueChanged(value)</c> → commit when
/// uncontrolled; the caller runs the follow-up effects when <see cref="RequestAsync"/> returns <c>true</c>.
/// <see cref="NotifyAsync"/> and <see cref="CommitAsync"/> are the two halves for a request that continues into a parent.
/// Development warnings go to the logger only while the <c>Blazix.Primitives.DevelopmentWarnings</c> switch is on;
/// each distinct message is logged once per instance.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Controlled<T>
{
    private readonly string name;
    private readonly string state;
    private readonly ILogger? logger;
    private bool initialized;
    private bool isControlled;
    private bool controlledSupplied;
    private T controlledValue = default!;
    private T uncontrolledValue = default!;
    private bool initialDefaultSupplied;
    private T initialDefault = default!;
    private HashSet<string>? loggedWarnings;

    /// <summary>Creates the helper; <paramref name="name"/> and <paramref name="state"/> parameterise the warnings (EVD-EDGE-01/02).</summary>
    public Controlled(string name, string state = "value", ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(state);
        this.name = name;
        this.state = state;
        this.logger = logger;
    }

    /// <summary>Whether the value parameter was supplied at the first parameter set. Fixed for the lifetime.</summary>
    public bool IsControlled => isControlled;

    /// <summary>
    /// The current value: the controlled parameter while controlled and supplied, otherwise the uncontrolled value
    /// (initialised from the default parameter as supplied, never invoked when it is a delegate: EVD-STA-03).
    /// </summary>
    public T Value => isControlled && controlledSupplied ? controlledValue : uncontrolledValue;

    /// <summary>
    /// Reads the value and default parameters from <paramref name="parameters"/> with <see cref="ParameterView.TryGetValue{TValue}(string, out TValue)"/>.
    /// Call it from <c>SetParametersAsync</c> before the base call, on every parameter set.
    /// </summary>
    public void SetParameters(ParameterView parameters, string valueParameterName, string defaultValueParameterName)
    {
        bool valueSupplied = parameters.TryGetValue(valueParameterName, out T? value);
        bool defaultValueSupplied = parameters.TryGetValue(defaultValueParameterName, out T? defaultValue);
        Update(valueSupplied, value!, defaultValueSupplied, defaultValue!);
    }

    /// <summary>
    /// The parameter set in its raw form: whether each parameter was supplied and its value. The first call fixes the mode
    /// and the uncontrolled value; later calls track the controlled value and raise EVD-EDGE-01/02.
    /// </summary>
    public void Update(bool valueSupplied, T value, bool defaultValueSupplied, T defaultValue)
    {
        controlledSupplied = valueSupplied;
        controlledValue = valueSupplied ? value : default!;
        if (!initialized)
        {
            initialized = true;
            isControlled = valueSupplied;
            initialDefaultSupplied = defaultValueSupplied;
            initialDefault = defaultValueSupplied ? defaultValue : default!;
            uncontrolledValue = initialDefault;
            return;
        }

        if (logger is null || !DevelopmentWarnings.IsEnabled)
        {
            return;
        }

        if (isControlled != valueSupplied)
        {
            Warn(string.Join(
                '\n',
                $"A component is changing the {(isControlled ? "" : "un")}controlled {state} state of {name} to be {(isControlled ? "un" : "")}controlled.",
                "Elements should not switch from uncontrolled to controlled (or vice versa).",
                $"Decide between using a controlled or uncontrolled {name} element for the lifetime of the component.",
                "The nature of the state is determined during the first render. It's considered controlled if the value is not `undefined`.",
                "More info: https://fb.me/react-controlled-components"));
        }

        if (!isControlled && !SameForDevelopment(initialDefaultSupplied, initialDefault, defaultValueSupplied, defaultValue))
        {
            Warn($"A component is changing the default {state} state of an uncontrolled {name} after being initialized. To suppress this warning opt to use a controlled {name}.");
        }
    }

    /// <summary>Stores <paramref name="value"/> when uncontrolled; a no-op when controlled (UTL-STA-04, EVD-STA-04).</summary>
    public void SetValueIfUncontrolled(T value)
    {
        if (!isControlled)
        {
            uncontrolledValue = value;
        }
    }

    /// <summary>
    /// The first half of a request: invokes <paramref name="onValueChange"/> and returns <c>true</c> unless the details are
    /// now canceled. A part whose request continues into a parent (EVD-ALG-03 step 2: a toggle into its group) calls this,
    /// then the parent's request with the same details, then <see cref="CommitAsync"/> only if neither was canceled.
    /// </summary>
    public async Task<bool> NotifyAsync(T value, ChangeEventDetails details, EventCallback<ValueChangeEventArgs<T>> onValueChange)
    {
        ArgumentNullException.ThrowIfNull(details);
        await onValueChange.InvokeAsync(new ValueChangeEventArgs<T>(value, details));
        return !details.IsCanceled;
    }

    /// <summary>The second half of a request: invokes <paramref name="valueChanged"/>, then commits when uncontrolled.</summary>
    public async Task CommitAsync(T value, EventCallback<T> valueChanged)
    {
        await valueChanged.InvokeAsync(value);
        SetValueIfUncontrolled(value);
    }

    /// <summary>
    /// Runs one request (EVD-ALG-03 with ADR 05 §2's added step): <paramref name="onValueChange"/> with
    /// <paramref name="value"/> and <paramref name="details"/>; if the details are canceled, stop and return <c>false</c>;
    /// otherwise <paramref name="valueChanged"/>, the commit when uncontrolled, and <c>true</c>.
    /// <para>
    /// <paramref name="nativeTextEdit"/> marks a request whose source is a native text edit on a text control (ADR 05
    /// Amendments 2026-10-18, item 2): when uncontrolled, a canceled request still fires <paramref name="valueChanged"/> and
    /// commits, because the browser already changed the text, and returns <c>false</c> so the follow-up effects stay
    /// suppressed. When controlled the flag changes nothing.
    /// </para>
    /// </summary>
    public async Task<bool> RequestAsync(
        T value,
        ChangeEventDetails details,
        EventCallback<ValueChangeEventArgs<T>> onValueChange,
        EventCallback<T> valueChanged,
        bool nativeTextEdit = false)
    {
        if (await NotifyAsync(value, details, onValueChange))
        {
            await CommitAsync(value, valueChanged);
            return true;
        }

        if (nativeTextEdit && !isControlled)
        {
            await CommitAsync(value, valueChanged);
        }

        return false;
    }

    // Base UI compares JSON serialisations (EVD-EDGE-02/03), which the port cannot produce for an arbitrary T without
    // reflection. Equal by EqualityComparer<T>.Default, by structure for arrays and tuples, or item by item for other
    // sequences counts as equal; a comparison that throws counts as equal, as two "__unserializable__" strings would.
    private static bool SameForDevelopment(bool leftSupplied, T left, bool rightSupplied, T right)
    {
        if (leftSupplied != rightSupplied)
        {
            return false;
        }

        try
        {
            if (EqualityComparer<T>.Default.Equals(left, right))
            {
                return true;
            }

            if (left is IStructuralEquatable structural && right is not null)
            {
                return structural.Equals(right, StructuralComparisons.StructuralEqualityComparer);
            }

            if (left is IEnumerable leftItems and not string && right is IEnumerable rightItems and not string)
            {
                return leftItems.Cast<object?>().SequenceEqual(rightItems.Cast<object?>());
            }

            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void Warn(string message)
    {
        loggedWarnings ??= new HashSet<string>(StringComparer.Ordinal);
        if (loggedWarnings.Add(message))
        {
            DevelopmentWarnings.Write(logger!, message);
        }
    }
}
