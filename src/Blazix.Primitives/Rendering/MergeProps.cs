using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

// RND-EVT-11
/// <summary>Represents a function-form input of <see cref="MergeProps"/>.</summary>
/// <param name="accumulated">The attributes accumulated so far from the inputs before this one.</param>
/// <returns>The attributes that replace the accumulated ones.</returns>
public delegate AttributeList AttributeGetter(AttributeList accumulated);

/// <summary>
/// Represents one input of <see cref="MergeProps.Merge(ReadOnlySpan{MergeInput})"/>: an attribute list, a function-form
/// input, or absent.
/// </summary>
/// <remarks>
/// An <see cref="AttributeList"/> or an <see cref="AttributeGetter"/> converts implicitly to a <see cref="MergeInput"/>.
/// </remarks>
public readonly struct MergeInput
{
    private MergeInput(AttributeList? list, AttributeGetter? getter)
    {
        List = list;
        Getter = getter;
    }

    /// <summary>Gets the attribute list of a list input.</summary>
    /// <value>The attribute list, or <see langword="null"/> when this input is a function-form input or absent.</value>
    public AttributeList? List { get; }

    /// <summary>Gets the function of a function-form input.</summary>
    /// <value>The function, or <see langword="null"/> when this input is a list input or absent.</value>
    public AttributeGetter? Getter { get; }

    /// <summary>Gets a value indicating whether this input is absent.</summary>
    /// <value>
    /// <see langword="true"/> if this input holds neither an attribute list nor a function; otherwise,
    /// <see langword="false"/>.
    /// </value>
    public bool IsAbsent => List is null && Getter is null;

    /// <summary>Gets the absent input, Base UI's <c>undefined</c>.</summary>
    /// <value>The default <see cref="MergeInput"/>, which holds neither an attribute list nor a function.</value>
    public static MergeInput Absent => default;

    /// <summary>Converts an attribute list to a list input.</summary>
    /// <param name="list">The attribute list, or <see langword="null"/> for the absent input.</param>
    /// <returns>A <see cref="MergeInput"/> that holds <paramref name="list"/>.</returns>
    public static implicit operator MergeInput(AttributeList? list) => new(list, null);

    /// <summary>Converts a function to a function-form input.</summary>
    /// <param name="getter">The function, or <see langword="null"/> for the absent input.</param>
    /// <returns>A <see cref="MergeInput"/> that holds <paramref name="getter"/>.</returns>
    public static implicit operator MergeInput(AttributeGetter? getter) => new(null, getter);
}

// RND-ALG-02..06, ADR 02 decision 4; ADR 02 decision 3; ADR 16 rule 5
/// <summary>Provides the public attribute merge, Base UI's <c>mergeProps</c>.</summary>
/// <remarks>
/// <para>
/// Inputs merge left to right. A later input's plain attributes win, its class is placed first, its style keys win, and
/// its handler runs first, with the veto of <see cref="EventArgsVetoExtensions.PreventPartHandler"/> stopping the handlers
/// after it.
/// </para>
/// <para>
/// A handler is any entry whose name starts with <c>on</c> (any casing) and whose value is a callback or delegate; it is
/// stored under the lowercase name Blazor's event dispatch expects.
/// </para>
/// <para>Nothing here is memoized; the render pipeline memoizes its own class and style merges per part.</para>
/// </remarks>
public static class MergeProps
{
    /// <summary>Represents the name of the attribute that the merge treats as Base UI's <c>className</c>.</summary>
    public const string ClassAttribute = "class";

    /// <summary>Represents the name of the attribute that the merge treats as Base UI's <c>style</c>.</summary>
    public const string StyleAttribute = "style";

    /// <summary>Merges any number of inputs, as Base UI's <c>mergePropsN</c> does.</summary>
    /// <param name="inputs">The inputs, merged left to right.</param>
    /// <returns>The merged attribute list; an empty list when <paramref name="inputs"/> is empty.</returns>
    /// <remarks>
    /// The first input is copied, so it is never modified; its handlers are not wrapped. A function-form input in first
    /// position receives an empty list and its result is copied. A function-form input in a later position receives the
    /// accumulated list and its result replaces the accumulation without a copy; the inputs after it merge into that result.
    /// </remarks>
    public static AttributeList Merge(params ReadOnlySpan<MergeInput> inputs)
    {
        if (inputs.Length == 0)
        {
            return new AttributeList();
        }

        AttributeList merged = Initial(inputs[0]);
        for (int i = 1; i < inputs.Length; i++)
        {
            merged = MergeInto(merged, inputs[i]);
        }

        return merged;
    }

    /// <summary>Merges two inputs, as Base UI's <c>mergeProps(a, b)</c> does.</summary>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input, which takes precedence over <paramref name="a"/>.</param>
    /// <returns>The merged attribute list.</returns>
    /// <remarks>This overload behaves as <see cref="Merge(ReadOnlySpan{MergeInput})"/> with the same inputs.</remarks>
    public static AttributeList Merge(MergeInput a, MergeInput b) => Merge([a, b]);

    /// <summary>Merges three inputs, as Base UI's <c>mergeProps(a, b, c)</c> does.</summary>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input, which takes precedence over <paramref name="a"/>.</param>
    /// <param name="c">The third input, which takes precedence over the inputs before it.</param>
    /// <returns>The merged attribute list.</returns>
    /// <remarks>This overload behaves as <see cref="Merge(ReadOnlySpan{MergeInput})"/> with the same inputs.</remarks>
    public static AttributeList Merge(MergeInput a, MergeInput b, MergeInput c) => Merge([a, b, c]);

    /// <summary>Merges four inputs, as Base UI's <c>mergeProps(a, b, c, d)</c> does.</summary>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input, which takes precedence over <paramref name="a"/>.</param>
    /// <param name="c">The third input, which takes precedence over the inputs before it.</param>
    /// <param name="d">The fourth input, which takes precedence over the inputs before it.</param>
    /// <returns>The merged attribute list.</returns>
    /// <remarks>This overload behaves as <see cref="Merge(ReadOnlySpan{MergeInput})"/> with the same inputs.</remarks>
    public static AttributeList Merge(MergeInput a, MergeInput b, MergeInput c, MergeInput d) => Merge([a, b, c, d]);

    /// <summary>Merges five inputs, as Base UI's <c>mergeProps(a, b, c, d, e)</c> does.</summary>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input, which takes precedence over <paramref name="a"/>.</param>
    /// <param name="c">The third input, which takes precedence over the inputs before it.</param>
    /// <param name="d">The fourth input, which takes precedence over the inputs before it.</param>
    /// <param name="e">The fifth input, which takes precedence over the inputs before it.</param>
    /// <returns>The merged attribute list.</returns>
    /// <remarks>This overload behaves as <see cref="Merge(ReadOnlySpan{MergeInput})"/> with the same inputs.</remarks>
    public static AttributeList Merge(MergeInput a, MergeInput b, MergeInput c, MergeInput d, MergeInput e) => Merge([a, b, c, d, e]);

    /// <summary>Merges attribute dictionaries left to right under the same rules as the list merge.</summary>
    /// <param name="inputs">The dictionaries to merge, left to right; a <see langword="null"/> entry is skipped.</param>
    /// <returns>A new dictionary of the merged attributes, whose values are boxed case values.</returns>
    /// <remarks>
    /// This is the dictionary form for consumers of <see cref="RenderElement{TState}"/>. Values are the case values of
    /// <see cref="AttributeValue"/> boxed, never unions; a boxed <see cref="EventCallback{TValue}"/> is accepted and unpacked.
    /// </remarks>
    public static Dictionary<string, object> MergeDictionaries(params ReadOnlySpan<IReadOnlyDictionary<string, object>?> inputs)
    {
        AttributeList merged = new();
        for (int i = 0; i < inputs.Length; i++)
        {
            IReadOnlyDictionary<string, object>? input = inputs[i];
            if (input is null)
            {
                continue;
            }

            foreach (KeyValuePair<string, object> pair in input)
            {
                MergeEntry(merged, pair.Key, AttributeValue.FromObject(pair.Value));
            }
        }

        return merged.ToDictionary();
    }

    // RND-ALG-05
    /// <summary>Joins two class strings with the later one first, as Base UI's <c>mergeClassNames(ours, theirs)</c> does.</summary>
    /// <param name="ours">The earlier class string.</param>
    /// <param name="theirs">The later class string, which is placed first.</param>
    /// <returns>
    /// <paramref name="theirs"/>, a space, then <paramref name="ours"/>. Empty operands are skipped: when either operand is
    /// <see langword="null"/> or empty, the other is returned as given.
    /// </returns>
    public static string? MergeClassNames(string? ours, string? theirs)
    {
        if (!string.IsNullOrEmpty(theirs))
        {
            return string.IsNullOrEmpty(ours) ? theirs : string.Concat(theirs, " ", ours);
        }

        return ours;
    }

    // RND-DOM-16; RND-ALG-06
    /// <summary>Merges two inline style strings by property name, with <paramref name="theirs"/> winning.</summary>
    /// <param name="ours">The earlier style string.</param>
    /// <param name="theirs">The later style string, whose declarations win.</param>
    /// <returns>
    /// The merged declarations serialized as <c>name: value; name: value;</c>; the single non-empty operand as given when
    /// only one is non-empty; or <see langword="null"/> when both are <see langword="null"/>, empty or white space.
    /// </returns>
    /// <remarks>
    /// Each string is split into declarations on <c>;</c> outside parentheses and quotes, and the declarations are merged
    /// by property name with first-seen order kept.
    /// </remarks>
    public static string? MergeStyles(string? ours, string? theirs)
    {
        bool hasOurs = !string.IsNullOrWhiteSpace(ours);
        bool hasTheirs = !string.IsNullOrWhiteSpace(theirs);
        if (!hasTheirs)
        {
            return hasOurs ? ours : null;
        }

        return hasOurs ? StyleDeclarations.Merge(ours!, theirs!) : theirs;
    }

    // ADR 02 decision 3
    /// <summary>Determines whether an entry is a handler.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="name"/> is longer than two characters and starts with <c>on</c> in any
    /// casing, and <paramref name="value"/> is a callback or delegate; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsHandler(string name, in AttributeValue value)
    {
        return value.IsHandler && name.Length > 2 && (name[0] is 'o' or 'O') && (name[1] is 'n' or 'N');
    }

    // RND-ALG-02
    /// <summary>
    /// Merges one entry into <paramref name="merged"/> under the merge rules, as the loop body of Base UI's
    /// <c>mergeInto</c> does.
    /// </summary>
    /// <param name="merged">The accumulated attribute list, modified in place.</param>
    /// <param name="name">The name of the incoming entry.</param>
    /// <param name="value">The value of the incoming entry, which takes precedence over the accumulated one.</param>
    internal static void MergeEntry(AttributeList merged, string name, AttributeValue value)
    {
        if (string.Equals(name, ClassAttribute, StringComparison.OrdinalIgnoreCase))
        {
            string? ours = merged.TryGetValue(ClassAttribute, out AttributeValue existing) && existing.TryGetValue(out string text) ? text : null;
            string? theirs = value.TryGetValue(out string incoming) ? incoming : null;
            merged.Set(ClassAttribute, MergeClassNames(ours, theirs));
        }
        else if (string.Equals(name, StyleAttribute, StringComparison.OrdinalIgnoreCase))
        {
            string? ours = merged.TryGetValue(StyleAttribute, out AttributeValue existing) && existing.TryGetValue(out string text) ? text : null;
            string? theirs = value.TryGetValue(out string incoming) ? incoming : null;
            merged.Set(StyleAttribute, MergeStyles(ours, theirs));
        }
        else if (IsHandler(name, value))
        {
            string handlerName = HandlerName(name);
            merged.TryGetValue(handlerName, out AttributeValue ours);
            merged.Set(handlerName, HandlerChain.Chain(value, ours));
        }
        else
        {
            merged.Set(name, value);
        }
    }

    /// <summary>Returns the name a handler is emitted under.</summary>
    /// <param name="name">The handler's attribute name.</param>
    /// <returns>
    /// <paramref name="name"/> in lowercase, as Razor emits event attributes and Blazor's dispatch expects; the same
    /// instance when it has no uppercase characters.
    /// </returns>
    internal static string HandlerName(string name)
    {
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                return name.ToLowerInvariant();
            }
        }

        return name;
    }

    private static AttributeList Initial(in MergeInput input)
    {
        AttributeList copy = new();
        if (input.Getter is not null)
        {
            // RND-EVT-12: the first-position getter's object is copied; its handlers are not wrapped (the veto is always available).
            copy.SetRange(input.Getter(new AttributeList()));
            return copy;
        }

        if (input.List is not null)
        {
            foreach (AttributeEntry entry in input.List)
            {
                copy.Set(IsHandler(entry.Name, entry.Value) ? HandlerName(entry.Name) : entry.Name, entry.Value);
            }
        }

        return copy;
    }

    private static AttributeList MergeInto(AttributeList merged, in MergeInput input)
    {
        if (input.Getter is not null)
        {
            // The getter's result replaces the accumulation and is not copied (RND-ALG-02; later inputs merge into it).
            return input.Getter(merged);
        }

        if (input.List is null)
        {
            return merged;
        }

        foreach (AttributeEntry entry in input.List)
        {
            MergeEntry(merged, entry.Name, entry.Value);
        }

        return merged;
    }
}
