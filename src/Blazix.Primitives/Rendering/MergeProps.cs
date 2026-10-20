using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

// RND-EVT-11
/// <summary>A function-form input of <see cref="MergeProps"/>: receives the attributes accumulated so far and returns the attributes that replace them.</summary>
public delegate AttributeList AttributeGetter(AttributeList accumulated);

/// <summary>
/// One input of <see cref="MergeProps.Merge(ReadOnlySpan{MergeInput})"/>: an attribute list, a function-form input, or absent.
/// </summary>
public readonly struct MergeInput
{
    private MergeInput(AttributeList? list, AttributeGetter? getter)
    {
        List = list;
        Getter = getter;
    }

    public AttributeList? List { get; }

    public AttributeGetter? Getter { get; }

    public bool IsAbsent => List is null && Getter is null;

    /// <summary>The absent input (Base UI's <c>undefined</c>).</summary>
    public static MergeInput Absent => default;

    public static implicit operator MergeInput(AttributeList? list) => new(list, null);

    public static implicit operator MergeInput(AttributeGetter? getter) => new(null, getter);
}

// RND-ALG-02..06, ADR 02 decision 4; ADR 02 decision 3; ADR 16 rule 5
/// <summary>
/// The public attribute merge: inputs merge left to right; a later input's plain
/// attributes win, its class is placed first, its style keys win, and its handler runs first with the veto of
/// <see cref="EventArgsVetoExtensions.PreventPartHandler"/> stopping the handlers after it. A handler is any entry whose
/// name starts with <c>on</c> (any casing) and whose value is a callback or delegate; it is stored
/// under the lowercase name Blazor's event dispatch expects. Nothing here is memoised; the pipeline memoises its own
/// class and style merges per part.
/// </summary>
public static class MergeProps
{
    /// <summary>The attribute the merge treats as Base UI's <c>className</c>.</summary>
    public const string ClassAttribute = "class";

    /// <summary>The attribute the merge treats as Base UI's <c>style</c>.</summary>
    public const string StyleAttribute = "style";

    /// <summary>Merges any number of inputs (<c>mergePropsN</c>); an empty span yields an empty list.</summary>
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

    /// <summary>Merges two inputs (<c>mergeProps(a, b)</c>).</summary>
    public static AttributeList Merge(MergeInput a, MergeInput b) => Merge([a, b]);

    public static AttributeList Merge(MergeInput a, MergeInput b, MergeInput c) => Merge([a, b, c]);

    public static AttributeList Merge(MergeInput a, MergeInput b, MergeInput c, MergeInput d) => Merge([a, b, c, d]);

    public static AttributeList Merge(MergeInput a, MergeInput b, MergeInput c, MergeInput d, MergeInput e) => Merge([a, b, c, d, e]);

    /// <summary>
    /// The dictionary form for consumers of <see cref="RenderElement{TState}"/>: values are the case values of
    /// <see cref="AttributeValue"/> boxed (a boxed <see cref="EventCallback{TValue}"/> is accepted and unpacked), never unions.
    /// </summary>
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
    /// <summary><c>mergeClassNames(ours, theirs)</c>: <paramref name="theirs"/> first, a space, then <paramref name="ours"/>; empty operands are skipped.</summary>
    public static string? MergeClassNames(string? ours, string? theirs)
    {
        if (!string.IsNullOrEmpty(theirs))
        {
            return string.IsNullOrEmpty(ours) ? theirs : string.Concat(theirs, " ", ours);
        }

        return ours;
    }

    // RND-DOM-16; RND-ALG-06
    /// <summary>
    /// Merges inline style strings: each is split into declarations (on <c>;</c> outside
    /// parentheses and quotes) and merged by property name, <paramref name="theirs"/> winning, with first-seen order kept.
    /// A single non-empty operand is returned as given.
    /// </summary>
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
    /// <summary>Whether an entry is a handler: an <c>on</c>-prefixed name (any casing) with a callback or delegate value.</summary>
    public static bool IsHandler(string name, in AttributeValue value)
    {
        return value.IsHandler && name.Length > 2 && (name[0] is 'o' or 'O') && (name[1] is 'n' or 'N');
    }

    // RND-ALG-02
    /// <summary>Merges one entry into <paramref name="merged"/> under the rules above (Base UI's <c>mergeInto</c> loop body).</summary>
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

    /// <summary>The name a handler is emitted under: lowercase, as Razor emits event attributes and Blazor's dispatch expects.</summary>
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
