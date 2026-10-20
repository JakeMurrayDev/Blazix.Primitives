using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

/// <summary>A function-form input of <see cref="MergeProps"/>: receives the attributes accumulated so far and returns the attributes that replace them (RND-EVT-11).</summary>
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

/// <summary>
/// The public attribute merge (RND-ALG-02..06, ADR 02 decision 4): inputs merge left to right; a later input's plain
/// attributes win, its class is placed first, its style keys win, and its handler runs first with the veto of
/// <see cref="EventArgsVetoExtensions.PreventPartHandler"/> stopping the handlers after it. A handler is any entry whose
/// name starts with <c>on</c> (any casing) and whose value is a callback or delegate (ADR 02 decision 3). Nothing here
/// is memoised; the pipeline memoises its own class and style merges per part (ADR 16 rule 5).
/// </summary>
public static class MergeProps
{
    /// <summary>Merges any number of inputs (<c>mergePropsN</c>); an empty span yields an empty list.</summary>
    public static AttributeList Merge(params ReadOnlySpan<MergeInput> inputs)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    /// <summary><c>mergeClassNames(ours, theirs)</c> (RND-ALG-05): <paramref name="theirs"/> first, a space, then <paramref name="ours"/>; empty operands are skipped.</summary>
    public static string? MergeClassNames(string? ours, string? theirs)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The style merge of RND-ALG-06 over inline style strings: each is split into declarations (on <c>;</c> outside
    /// parentheses and quotes) and merged by property name, <paramref name="theirs"/> winning, with first-seen order kept
    /// (RND-DOM-16). A single non-empty operand is returned as given.
    /// </summary>
    public static string? MergeStyles(string? ours, string? theirs)
    {
        throw new NotImplementedException();
    }
}
