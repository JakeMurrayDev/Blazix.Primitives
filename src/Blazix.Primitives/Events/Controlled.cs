using System.Collections;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// ADR 05 Amendments 2026-10-18, item 1; EVD-STA-05; EVD-ALG-08, EVD-STA-01..06, UTL-STA-01..06; ADR 05 §1/§2; glossary "Controlled / uncontrolled", "Request"; EVD-EDGE-01
/// <summary>
/// Represents a value that is either controlled by the consumer or held by the part, and runs the requests that change it.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <remarks>
/// <para>
/// The value is controlled if and only if its parameter was supplied at the first parameter set, and it stays so for the
/// lifetime of the part. Supplying or removing the parameter later only raises a development warning that the component
/// is switching between controlled and uncontrolled, and a removed controlled value falls back to the initial default.
/// </para>
/// <para>
/// A request runs <c>OnValueChange(args)</c>, stops if the details were canceled, then runs <c>ValueChanged(value)</c> and
/// commits when uncontrolled; the caller runs the follow-up effects when <see cref="RequestAsync"/> returns
/// <see langword="true"/>. <see cref="NotifyAsync"/> and <see cref="CommitAsync"/> are the two halves for a request that
/// continues into a parent.
/// </para>
/// <para>
/// Development warnings go to the logger only while the <c>Blazix.Primitives.DevelopmentWarnings</c> switch is on; each
/// distinct message is logged once per instance.
/// </para>
/// </remarks>
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

    // EVD-EDGE-01/02
    /// <summary>
    /// Initializes a new instance of the <see cref="Controlled{T}"/> class.
    /// </summary>
    /// <param name="name">The component name used in development warnings, such as <c>SwitchRoot</c>.</param>
    /// <param name="state">The name of the value used in development warnings, such as <c>checked</c>. The default is <c>value</c>.</param>
    /// <param name="logger">The logger that receives development warnings, or <see langword="null"/> to write none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    public Controlled(string name, string state = "value", ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(state);
        this.name = name;
        this.state = state;
        this.logger = logger;
    }

    /// <summary>
    /// Gets a value that indicates whether the value parameter was supplied at the first parameter set.
    /// </summary>
    /// <value><see langword="true"/> if the value is controlled by the consumer; otherwise, <see langword="false"/>. Fixed for the lifetime of the instance.</value>
    public bool IsControlled => isControlled;

    // EVD-STA-03
    /// <summary>
    /// Gets the current value.
    /// </summary>
    /// <value>
    /// The controlled parameter while the value is controlled and the parameter is supplied; otherwise, the uncontrolled
    /// value, initialized from the default parameter as supplied (a delegate is stored, never invoked).
    /// </value>
    public T Value => isControlled && controlledSupplied ? controlledValue : uncontrolledValue;

    /// <summary>
    /// Reads the value and default parameters from a parameter set.
    /// </summary>
    /// <param name="parameters">The parameters passed to <c>SetParametersAsync</c>.</param>
    /// <param name="valueParameterName">The name of the value parameter, such as <c>Checked</c>.</param>
    /// <param name="defaultValueParameterName">The name of the default parameter, such as <c>DefaultChecked</c>.</param>
    /// <remarks>Call it from <c>SetParametersAsync</c> before the base call, on every parameter set. It calls <see cref="Update"/>.</remarks>
    public void SetParameters(ParameterView parameters, string valueParameterName, string defaultValueParameterName)
    {
        bool valueSupplied = parameters.TryGetValue(valueParameterName, out T? value);
        bool defaultValueSupplied = parameters.TryGetValue(defaultValueParameterName, out T? defaultValue);
        Update(valueSupplied, value!, defaultValueSupplied, defaultValue!);
    }

    // EVD-EDGE-01/02
    /// <summary>
    /// Records one parameter set in raw form: whether each parameter was supplied, and its value.
    /// </summary>
    /// <param name="valueSupplied"><see langword="true"/> if the value parameter was supplied; otherwise, <see langword="false"/>.</param>
    /// <param name="value">The value parameter, used only when <paramref name="valueSupplied"/> is <see langword="true"/>.</param>
    /// <param name="defaultValueSupplied"><see langword="true"/> if the default parameter was supplied; otherwise, <see langword="false"/>.</param>
    /// <param name="defaultValue">The default parameter, used only when <paramref name="defaultValueSupplied"/> is <see langword="true"/>.</param>
    /// <remarks>
    /// The first call fixes <see cref="IsControlled"/> and the uncontrolled value. Later calls track the controlled value and
    /// raise the development warnings for a mode switch and for a default that changes after the first parameter set.
    /// </remarks>
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

    // UTL-STA-04, EVD-STA-04
    /// <summary>
    /// Stores a value when the value is uncontrolled.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <remarks>Does nothing when the value is controlled.</remarks>
    public void SetValueIfUncontrolled(T value)
    {
        if (!isControlled)
        {
            uncontrolledValue = value;
        }
    }

    // EVD-ALG-03 step 2
    /// <summary>
    /// Runs the first half of a request: invokes <paramref name="onValueChange"/> with the requested value and its details.
    /// </summary>
    /// <param name="value">The requested value.</param>
    /// <param name="details">The request's details.</param>
    /// <param name="onValueChange">The consumer's change callback.</param>
    /// <returns>A task whose result is <see langword="true"/> unless <paramref name="details"/> is canceled after the callback.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="details"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A part whose request continues into a parent (a toggle into its group) calls this, then the parent's request with the
    /// same details, then <see cref="CommitAsync"/> only if neither was canceled.
    /// </remarks>
    public async Task<bool> NotifyAsync(T value, ChangeEventDetails details, EventCallback<ValueChangeEventArgs<T>> onValueChange)
    {
        ArgumentNullException.ThrowIfNull(details);
        await onValueChange.InvokeAsync(new ValueChangeEventArgs<T>(value, details));
        return !details.IsCanceled;
    }

    /// <summary>
    /// Runs the second half of a request: invokes <paramref name="valueChanged"/>, then stores the value when uncontrolled.
    /// </summary>
    /// <param name="value">The value to commit.</param>
    /// <param name="valueChanged">The binding callback of <c>@bind-Value</c>.</param>
    /// <returns>A task that completes when the callback has run and the value is stored.</returns>
    public async Task CommitAsync(T value, EventCallback<T> valueChanged)
    {
        await valueChanged.InvokeAsync(value);
        SetValueIfUncontrolled(value);
    }

    // EVD-ALG-03 with ADR 05 §2's added step; ADR 05 Amendments 2026-10-18, item 2
    /// <summary>
    /// Runs one request: the change callback, then, unless the details were canceled, the binding callback and the commit.
    /// </summary>
    /// <param name="value">The requested value.</param>
    /// <param name="details">The request's details.</param>
    /// <param name="onValueChange">The consumer's change callback, invoked first.</param>
    /// <param name="valueChanged">The binding callback of <c>@bind-Value</c>, invoked when the request is not canceled.</param>
    /// <param name="nativeTextEdit"><see langword="true"/> if the request comes from a native text edit on a text control; otherwise, <see langword="false"/>.</param>
    /// <returns>A task whose result is <see langword="true"/> if the request was applied, so the caller runs its follow-up effects; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="details"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// When <paramref name="nativeTextEdit"/> is <see langword="true"/> and the value is uncontrolled, a canceled request still
    /// invokes <paramref name="valueChanged"/> and commits, because the browser has already changed the text, and returns
    /// <see langword="false"/> so the follow-up effects stay suppressed. When the value is controlled, the flag changes nothing.
    /// </remarks>
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
