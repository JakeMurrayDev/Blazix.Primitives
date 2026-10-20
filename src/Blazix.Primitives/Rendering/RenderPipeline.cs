using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

// BL0006: the frame rewrite reads the consumer fragment's frames through the public GetFrames() (ADR 02 decision 1).
// ASP0006: re-emitted frames keep their own literal sequence numbers inside their own region (ADR 16 rule 3); the
// pipeline's own frames use literals.
#pragma warning disable BL0006
#pragma warning disable ASP0006

namespace Blazix.Primitives.Rendering;

/// <summary>The inputs of one render of a part or <see cref="RenderElement{TState}"/> (RND §6.1 in Blazor terms).</summary>
internal readonly struct RenderInputs<TState>
    where TState : class, IPartState<TState>
{
    public TState? State { get; init; }

    public StateAttributesMapping<TState>? Mapping { get; init; }

    public AttributeList? PartAttributes { get; init; }

    public IReadOnlyDictionary<string, object>? ConsumerAttributes { get; init; }

    public Func<TState, string?>? ClassValue { get; init; }

    public Func<TState, string?>? StyleValue { get; init; }

    public RenderFragment? Render { get; init; }

    public RenderFragment<RenderProps<TState>>? RenderFunction { get; init; }

    public RenderFragment? ChildContent { get; init; }

    public string DefaultTagName { get; init; }

    public bool Enabled { get; init; }
}

/// <summary>
/// The one static function every part renders through (ADR 02 decision 4; RND-ALG-01/02/05/06/07/08): state
/// projection, the merge of part-supplied and consumer-supplied attributes with class, style and handlers treated
/// specially, the class and style functions, and then the default element, the substitute callback, or the frame
/// rewrite of the substitute element (ADR 02 decision 1).
/// </summary>
internal static class RenderPipeline
{
    private const int HostSequence = 0;
    private const int AttributeSequence = 1;
    private const int CaptureSequence = 2;
    private const int ConsumerCaptureSequence = 3;
    private const int ChildrenSequence = 100;
    private const int TrailingCaptureSequence = 101;
    private const string DirectivePrefix = "__internal_";
    private const string PreventDefaultPrefix = "__internal_preventDefault_";
    private const string StopPropagationPrefix = "__internal_stopPropagation_";

    public static void Render<TState>(RenderTreeBuilder builder, PartRenderState state, IPartHost host, in RenderInputs<TState> inputs)
        where TState : class, IPartState<TState>
    {
        if (inputs.Render is not null && inputs.RenderFunction is not null)
        {
            throw new InvalidOperationException(
                "Blazix: `Render` and `RenderFunction` are mutually exclusive. Supply the substitute element in `Render` or the substitute callback in `RenderFunction`, not both (ADR 02 decision 1).");
        }

        if (!inputs.Enabled)
        {
            // RND-DOM-25: nothing rendered and no work done; the element handle is released.
            host.SetElement(null);
            return;
        }

        Pass<TState> pass = new(builder, state, host, inputs);
        pass.Run();
    }

    /// <summary>The locals of one render; a struct so the pipeline allocates nothing of its own per render.</summary>
    private ref struct Pass<TState>(RenderTreeBuilder builder, PartRenderState state, IPartHost host, in RenderInputs<TState> inputs)
        where TState : class, IPartState<TState>
    {
        private readonly RenderTreeBuilder builder = builder;
        private readonly PartRenderState state = state;
        private readonly IPartHost host = host;
        private readonly RenderInputs<TState> inputs = inputs;
        private readonly AttributeList merged = state.Merged;
        private string? partClass;
        private string? partStyle;
        private string? consumerClass;
        private string? consumerStyle;
        private string? substituteClass;
        private string? substituteStyle;

        public void Run()
        {
            state.ResetSlots();
            merged.Clear();
            state.ComponentParameters.Clear();

            // RND §2.1 rows 1–2: state data attributes, then part-supplied, then consumer-supplied attributes.
            inputs.State?.ProjectStateAttributes(inputs.Mapping, new StateAttributeWriter(merged));
            MergePartAttributes();
            MergeConsumerAttributes();

            // Rows 3–4: the class and style functions of state.
            string? classFunction = inputs.State is not null ? inputs.ClassValue?.Invoke(inputs.State) : null;
            string? styleFunction = inputs.State is not null ? inputs.StyleValue?.Invoke(inputs.State) : null;

            if (inputs.RenderFunction is not null)
            {
                Finish(classFunction, styleFunction);
                EmitCallback();
                return;
            }

            if (inputs.Render is null)
            {
                Finish(classFunction, styleFunction);
                EmitDefaultElement();
                return;
            }

            // Row 5: the substitute element's own attributes win; its children replace the part's.
            RenderTreeBuilder scratch = state.Scratch;
            scratch.Clear();
            inputs.Render(scratch);
            ArrayRange<RenderTreeFrame> frames = scratch.GetFrames();
            int rootIndex = FindRoot(frames);
            ref RenderTreeFrame root = ref frames.Array[rootIndex];
            switch (root.FrameType)
            {
                case RenderTreeFrameType.Element:
                    int childrenStart = MergeSubstituteElementAttributes(frames, rootIndex);
                    Finish(classFunction, styleFunction);
                    EmitSubstituteElement(frames, rootIndex, childrenStart);
                    break;
                case RenderTreeFrameType.Component:
                    int captureStart = MergeSubstituteComponentParameters(frames, rootIndex);
                    Finish(classFunction, styleFunction);
                    EmitSubstituteComponent(frames, rootIndex, captureStart);
                    break;
                case RenderTreeFrameType.Markup:
                    if (!MarkupRootParser.TryParse(root.MarkupContent, out MarkupRootParser.Root parsed, out string reason))
                    {
                        throw InvalidRoot($"a markup block that could not be read as one element ({reason})");
                    }

                    MergeSubstituteMarkupAttributes(parsed);
                    Finish(classFunction, styleFunction);
                    EmitSubstituteMarkup(parsed);
                    break;
                default:
                    throw InvalidRoot($"a {root.FrameType} frame");
            }
        }

        private void MergePartAttributes()
        {
            if (inputs.PartAttributes is null)
            {
                return;
            }

            foreach (AttributeEntry entry in inputs.PartAttributes)
            {
                if (IsClass(entry.Name))
                {
                    partClass = AsString(entry.Value);
                }
                else if (IsStyle(entry.Name))
                {
                    partStyle = AsString(entry.Value);
                }
                else if (MergeProps.IsHandler(entry.Name, entry.Value))
                {
                    HandlerSlot slot = state.GetOrAddSlot(MergeProps.HandlerName(entry.Name));
                    if (!(entry.Value.TryGetValue(out EventCallback callback) && callback.Equals(slot.Typed)))
                    {
                        slot.Part = entry.Value;
                    }
                }
                else
                {
                    merged.Set(entry.Name, entry.Value);
                }
            }
        }

        private void MergeConsumerAttributes()
        {
            if (inputs.ConsumerAttributes is null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in inputs.ConsumerAttributes)
            {
                ThrowIfDirective(pair.Key, "the part");
                if (IsClass(pair.Key))
                {
                    consumerClass = pair.Value as string ?? pair.Value?.ToString();
                }
                else if (IsStyle(pair.Key))
                {
                    consumerStyle = pair.Value as string ?? pair.Value?.ToString();
                }
                else
                {
                    AttributeValue value = AttributeValue.FromObject(pair.Value);
                    if (!value.HasValue)
                    {
                        continue;
                    }

                    if (MergeProps.IsHandler(pair.Key, value))
                    {
                        state.GetOrAddSlot(MergeProps.HandlerName(pair.Key)).Consumer = value;
                    }
                    else
                    {
                        merged.Set(pair.Key, value);
                    }
                }
            }
        }

        // Returns the index of the first child frame of the root element.
        private int MergeSubstituteElementAttributes(ArrayRange<RenderTreeFrame> frames, int rootIndex)
        {
            int end = rootIndex + frames.Array[rootIndex].ElementSubtreeLength;
            int i = rootIndex + 1;
            for (; i < end && frames.Array[i].FrameType == RenderTreeFrameType.Attribute; i++)
            {
                ref RenderTreeFrame frame = ref frames.Array[i];
                ThrowIfDirective(frame.AttributeName, "a substitute element");
                MergeSubstituteAttribute(frame.AttributeName, AttributeValue.FromFrameValue(frame.AttributeValue));
            }

            return i;
        }

        private void MergeSubstituteMarkupAttributes(in MarkupRootParser.Root parsed)
        {
            foreach (KeyValuePair<string, string> attribute in parsed.Attributes)
            {
                MergeSubstituteAttribute(attribute.Key, new AttributeValue(attribute.Value));
            }
        }

        private void MergeSubstituteAttribute(string name, AttributeValue value)
        {
            if (IsClass(name))
            {
                substituteClass = AsString(value);
            }
            else if (IsStyle(name))
            {
                substituteStyle = AsString(value);
            }
            else if (MergeProps.IsHandler(name, value))
            {
                state.GetOrAddSlot(MergeProps.HandlerName(name)).Substitute = value;
            }
            else
            {
                merged.Set(name, value);
            }
        }

        // A component root's parameters are kept as the objects Razor passed (a typed parameter must survive); only
        // class, style and handlers take part in the merge. Returns the index of the first non-parameter child frame.
        private int MergeSubstituteComponentParameters(ArrayRange<RenderTreeFrame> frames, int rootIndex)
        {
            int end = rootIndex + frames.Array[rootIndex].ComponentSubtreeLength;
            int i = rootIndex + 1;
            for (; i < end && frames.Array[i].FrameType == RenderTreeFrameType.Attribute; i++)
            {
                ref RenderTreeFrame frame = ref frames.Array[i];
                string name = frame.AttributeName;
                ThrowIfDirective(name, "a substitute component");
                if (IsClass(name))
                {
                    substituteClass = frame.AttributeValue as string;
                }
                else if (IsStyle(name))
                {
                    substituteStyle = frame.AttributeValue as string;
                }
                else
                {
                    AttributeValue value = AttributeValue.FromObject(frame.AttributeValue);
                    if (MergeProps.IsHandler(name, value))
                    {
                        state.GetOrAddSlot(MergeProps.HandlerName(name)).Substitute = value;
                    }
                    else
                    {
                        state.ComponentParameters.Add(new KeyValuePair<string, object?>(name, frame.AttributeValue));
                    }
                }
            }

            return i;
        }

        // Rows 3–5 for class and style (memoised, ADR 16 rule 5), then one real handler per event (ADR 02 decision 3).
        private void Finish(string? classFunction, string? styleFunction)
        {
            string? cls = state.ClassMemo.Merge(substituteClass, consumerClass, classFunction, partClass);
            if (cls is not null)
            {
                merged.Set(MergeProps.ClassAttribute, cls);
            }

            string? style = state.StyleMemo.Merge(partStyle, consumerStyle, styleFunction, substituteStyle);
            if (style is not null)
            {
                merged.Set(MergeProps.StyleAttribute, style);
            }

            List<HandlerSlot> slots = state.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                HandlerSlot slot = slots[i];
                if (slot.Typed.HasDelegate)
                {
                    // The part's own handler: its callback is the real one and runs the chain itself.
                    merged.Set(slot.Name, slot.Typed);
                    continue;
                }

                int count = (slot.Substitute.IsHandler ? 1 : 0) + (slot.Consumer.IsHandler ? 1 : 0) + (slot.Part.IsHandler ? 1 : 0);
                if (count == 0)
                {
                    merged.Remove(slot.Name);
                }
                else if (count == 1)
                {
                    merged.Set(slot.Name, slot.Substitute.IsHandler ? slot.Substitute : slot.Consumer.IsHandler ? slot.Consumer : slot.Part);
                }
                else
                {
                    merged.Set(slot.Name, state.OuterChain(slot));
                }
            }
        }

        private readonly void EmitDefaultElement()
        {
            string tag = inputs.DefaultTagName;
            builder.OpenElement(HostSequence, tag);
            if (tag == "button" && !merged.Contains("type"))
            {
                builder.AddAttribute(AttributeSequence, "type", "button");
            }
            else if (tag == "img" && !merged.Contains("alt"))
            {
                builder.AddAttribute(AttributeSequence, "alt", string.Empty);
            }

            EmitMergedAttributes();
            builder.AddElementReferenceCapture(CaptureSequence, state.CaptureAction);
            builder.AddContent(ChildrenSequence, inputs.ChildContent);
            builder.CloseElement();
        }

        private readonly void EmitCallback()
        {
            host.SetElement(null);
            RenderProps<TState> props = new(merged.ToDictionary(), inputs.State!, inputs.ChildContent);
            builder.AddContent(HostSequence, inputs.RenderFunction, props);
        }

        private readonly void EmitSubstituteElement(ArrayRange<RenderTreeFrame> frames, int rootIndex, int childrenStart)
        {
            ref RenderTreeFrame root = ref frames.Array[rootIndex];
            int end = rootIndex + root.ElementSubtreeLength;
            builder.OpenElement(HostSequence, root.ElementName);
            builder.SetKey(root.ElementKey);
            EmitMergedAttributes();
            builder.AddElementReferenceCapture(CaptureSequence, state.CaptureAction);

            bool regionOpen = false;
            bool hasChildren = false;
            int captureSequence = ConsumerCaptureSequence;
            for (int i = childrenStart; i < end; i += SubtreeLength(in frames.Array[i]))
            {
                ref RenderTreeFrame frame = ref frames.Array[i];
                switch (frame.FrameType)
                {
                    case RenderTreeFrameType.ElementReferenceCapture:
                        CloseChildrenRegion(ref regionOpen, ref captureSequence);
                        builder.AddElementReferenceCapture(captureSequence++, frame.ElementReferenceCaptureAction);
                        break;
                    case RenderTreeFrameType.NamedEvent:
                        CloseChildrenRegion(ref regionOpen, ref captureSequence);
                        builder.AddNamedEvent(frame.NamedEventType, frame.NamedEventAssignedName);
                        break;
                    case RenderTreeFrameType.Attribute:
                        break;
                    default:
                        if (!regionOpen)
                        {
                            builder.OpenRegion(ChildrenSequence);
                            regionOpen = true;
                        }

                        hasChildren = true;
                        Reemit(frames, i);
                        break;
                }
            }

            if (regionOpen)
            {
                builder.CloseRegion();
            }

            if (!hasChildren)
            {
                builder.AddContent(ChildrenSequence, inputs.ChildContent);
            }

            builder.CloseElement();
        }

        private readonly void CloseChildrenRegion(ref bool regionOpen, ref int captureSequence)
        {
            if (regionOpen)
            {
                builder.CloseRegion();
                regionOpen = false;
                captureSequence = Math.Max(captureSequence, TrailingCaptureSequence);
            }
        }

        private readonly void EmitSubstituteMarkup(in MarkupRootParser.Root parsed)
        {
            builder.OpenElement(HostSequence, parsed.TagName);
            EmitMergedAttributes();
            builder.AddElementReferenceCapture(CaptureSequence, state.CaptureAction);
            if (parsed.InnerMarkup is null)
            {
                // A void or self-closed element takes no children.
            }
            else if (parsed.InnerMarkup.Length > 0)
            {
                builder.OpenRegion(ChildrenSequence);
                builder.AddMarkupContent(0, parsed.InnerMarkup);
                builder.CloseRegion();
            }
            else
            {
                builder.AddContent(ChildrenSequence, inputs.ChildContent);
            }

            builder.CloseElement();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The component type comes from the consumer's own OpenComponent<T> call in the scratch render, which already preserved its members.")]
        private readonly void EmitSubstituteComponent(ArrayRange<RenderTreeFrame> frames, int rootIndex, int captureStart)
        {
            ref RenderTreeFrame root = ref frames.Array[rootIndex];
            int end = rootIndex + root.ComponentSubtreeLength;
            host.SetElement(null);
            builder.OpenComponent(HostSequence, root.ComponentType);
            builder.SetKey(root.ComponentKey);

            List<KeyValuePair<string, object?>> own = state.ComponentParameters;
            bool hasOwnChildContent = false;
            foreach (AttributeEntry entry in merged)
            {
                if (!ContainsName(own, entry.Name))
                {
                    builder.AddComponentParameter(AttributeSequence, entry.Name, entry.Value.ToObject());
                }
            }

            for (int i = 0; i < own.Count; i++)
            {
                builder.AddComponentParameter(AttributeSequence, own[i].Key, own[i].Value);
                hasOwnChildContent |= string.Equals(own[i].Key, "ChildContent", StringComparison.Ordinal);
            }

            if (!hasOwnChildContent && inputs.ChildContent is not null)
            {
                builder.AddComponentParameter(AttributeSequence, "ChildContent", inputs.ChildContent);
            }

            int captureSequence = CaptureSequence;
            for (int i = captureStart; i < end; i += SubtreeLength(in frames.Array[i]))
            {
                ref RenderTreeFrame frame = ref frames.Array[i];
                switch (frame.FrameType)
                {
                    case RenderTreeFrameType.ComponentReferenceCapture:
                        builder.AddComponentReferenceCapture(captureSequence++, frame.ComponentReferenceCaptureAction);
                        break;
                    case RenderTreeFrameType.ComponentRenderMode:
                        builder.AddComponentRenderMode(frame.ComponentRenderMode);
                        break;
                }
            }

            builder.CloseComponent();
        }

        private readonly void EmitMergedAttributes()
        {
            foreach (AttributeEntry entry in merged)
            {
                entry.Value.EmitTo(builder, AttributeSequence, entry.Name);
            }
        }

        // Re-emits one frame and its subtree verbatim, with its original sequence numbers (ADR 16 rule 3).
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The component type comes from the consumer's own OpenComponent<T> call in the scratch render, which already preserved its members.")]
        private readonly void Reemit(ArrayRange<RenderTreeFrame> frames, int index)
        {
            ref RenderTreeFrame frame = ref frames.Array[index];
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Text:
                    builder.AddContent(frame.Sequence, frame.TextContent);
                    break;
                case RenderTreeFrameType.Markup:
                    builder.AddMarkupContent(frame.Sequence, frame.MarkupContent);
                    break;
                case RenderTreeFrameType.Region:
                    {
                        int end = index + frame.RegionSubtreeLength;
                        builder.OpenRegion(frame.Sequence);
                        for (int i = index + 1; i < end; i += SubtreeLength(in frames.Array[i]))
                        {
                            Reemit(frames, i);
                        }

                        builder.CloseRegion();
                        break;
                    }

                case RenderTreeFrameType.Element:
                    {
                        int end = index + frame.ElementSubtreeLength;
                        builder.OpenElement(frame.Sequence, frame.ElementName);
                        builder.SetKey(frame.ElementKey);
                        int i = index + 1;
                        for (; i < end && frames.Array[i].FrameType == RenderTreeFrameType.Attribute; i++)
                        {
                            builder.AddAttribute(frames.Array[i].Sequence, frames.Array[i]);
                        }

                        for (; i < end; i += SubtreeLength(in frames.Array[i]))
                        {
                            Reemit(frames, i);
                        }

                        builder.CloseElement();
                        break;
                    }

                case RenderTreeFrameType.Component:
                    {
                        int end = index + frame.ComponentSubtreeLength;
                        builder.OpenComponent(frame.Sequence, frame.ComponentType);
                        builder.SetKey(frame.ComponentKey);
                        for (int i = index + 1; i < end; i += SubtreeLength(in frames.Array[i]))
                        {
                            ref RenderTreeFrame child = ref frames.Array[i];
                            switch (child.FrameType)
                            {
                                case RenderTreeFrameType.Attribute:
                                    builder.AddAttribute(child.Sequence, child);
                                    break;
                                case RenderTreeFrameType.ComponentReferenceCapture:
                                    builder.AddComponentReferenceCapture(child.Sequence, child.ComponentReferenceCaptureAction);
                                    break;
                                case RenderTreeFrameType.ComponentRenderMode:
                                    builder.AddComponentRenderMode(child.ComponentRenderMode);
                                    break;
                            }
                        }

                        builder.CloseComponent();
                        break;
                    }

                case RenderTreeFrameType.ElementReferenceCapture:
                    builder.AddElementReferenceCapture(frame.Sequence, frame.ElementReferenceCaptureAction);
                    break;
                case RenderTreeFrameType.NamedEvent:
                    builder.AddNamedEvent(frame.NamedEventType, frame.NamedEventAssignedName);
                    break;
            }
        }

        // The single root of the fragment: whitespace-only text is skipped and a lone region is looked into (RND-EDGE-03).
        private static int FindRoot(ArrayRange<RenderTreeFrame> frames)
        {
            int start = 0;
            int end = frames.Count;
            while (true)
            {
                int rootIndex = -1;
                for (int i = start; i < end; i += SubtreeLength(in frames.Array[i]))
                {
                    ref RenderTreeFrame frame = ref frames.Array[i];
                    if ((frame.FrameType == RenderTreeFrameType.Text && string.IsNullOrWhiteSpace(frame.TextContent))
                        || (frame.FrameType == RenderTreeFrameType.Markup && string.IsNullOrWhiteSpace(frame.MarkupContent)))
                    {
                        continue;
                    }

                    if (rootIndex >= 0)
                    {
                        throw InvalidRoot("more than one root");
                    }

                    rootIndex = i;
                }

                if (rootIndex < 0)
                {
                    throw InvalidRoot("nothing");
                }

                if (frames.Array[rootIndex].FrameType != RenderTreeFrameType.Region)
                {
                    return rootIndex;
                }

                start = rootIndex + 1;
                end = rootIndex + frames.Array[rootIndex].RegionSubtreeLength;
            }
        }

        private static int SubtreeLength(in RenderTreeFrame frame)
        {
            return frame.FrameType switch
            {
                RenderTreeFrameType.Element => frame.ElementSubtreeLength,
                RenderTreeFrameType.Component => frame.ComponentSubtreeLength,
                RenderTreeFrameType.Region => frame.RegionSubtreeLength,
                _ => 1,
            };
        }

        private static bool ContainsName(List<KeyValuePair<string, object?>> parameters, string name)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (string.Equals(parameters[i].Key, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsClass(string name) => string.Equals(name, MergeProps.ClassAttribute, StringComparison.OrdinalIgnoreCase);

        private static bool IsStyle(string name) => string.Equals(name, MergeProps.StyleAttribute, StringComparison.OrdinalIgnoreCase);

        private static string? AsString(in AttributeValue value) => value.TryGetValue(out string text) ? text : null;

        private static InvalidOperationException InvalidRoot(string what)
        {
            return new InvalidOperationException(
                $"Blazix: the `Render` fragment must have exactly one root that is an element or a component, because that root is re-emitted with the part's merged attributes in place of the default element (RND-EDGE-03). It rendered {what}.");
        }

        // ADR 04 Amendments 2026-10-18 item 3: Blazor's event directives are applied after the library's JavaScript
        // listeners have run and cannot be seen by them, so they are rejected on parts and on substitute elements.
        private static void ThrowIfDirective(string name, string where)
        {
            if (!name.StartsWith(DirectivePrefix, StringComparison.Ordinal))
            {
                return;
            }

            string directive = name.StartsWith(PreventDefaultPrefix, StringComparison.Ordinal)
                ? $"@{name[PreventDefaultPrefix.Length..]}:preventDefault"
                : name.StartsWith(StopPropagationPrefix, StringComparison.Ordinal)
                    ? $"@{name[StopPropagationPrefix.Length..]}:stopPropagation"
                    : name;
            throw new InvalidOperationException(
                $"Blazix: `{directive}` is not supported on {where}. Blazor applies event directives after the library's JavaScript listeners have run, so they cannot take part in its dispatch. Call `e.PreventPartHandler()` from a C# handler to stop the part's handler, or call `preventDefault()` from a JavaScript listener on the element to cancel the browser default (ADR 04).");
        }
    }
}
