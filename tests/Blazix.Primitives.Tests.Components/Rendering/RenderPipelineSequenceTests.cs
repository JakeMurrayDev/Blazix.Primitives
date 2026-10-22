using Blazix.Primitives.Tests.Components.Rendering.Consumers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// ADR 16 rule 3 at L2: the sequence numbers of the frames the pipeline emits. A frame the pipeline itself adds carries one
/// of its documented literals (host 0, attributes 1, the part's own capture 2, the consumer's capture 3, the children
/// region 100, a capture after the children 101, the region after the host element 102), and every frame inside the children region carries the number the
/// consumer's own fragment gave it. The frames are read from a <see cref="Renderer"/> of the test's own because bUnit
/// reaches them only through a protected member.
/// </summary>
public sealed class RenderPipelineSequenceTests : IDisposable
{
    // The literals of RenderPipeline, written out here so that changing one of them fails a test.
    private const int HostSequence = 0;
    private const int AttributeSequence = 1;
    private const int PartCaptureSequence = 2;
    private const int ConsumerCaptureSequence = 3;
    private const int ChildrenSequence = 100;
    private const int TrailingCaptureSequence = 101;
    private const int AfterElementSequence = 102;

    private readonly ServiceProvider services = new ServiceCollection().AddBlazixPrimitives().BuildServiceProvider();
    private readonly FrameRenderer renderer;

    public RenderPipelineSequenceTests()
    {
        renderer = new FrameRenderer(services);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        renderer.Dispose();
        services.Dispose();
    }

    [Fact(DisplayName = "the default element carries the literal sequence numbers and its child content keeps its own")]
    [Trait("Requirement", "ADR16-3")]
    public void The_default_element_carries_the_literal_sequence_numbers_and_its_child_content_keeps_its_own()
    {
        ArrayRange<RenderTreeFrame> frames = renderer.RenderSampleRoot(new Dictionary<string, object?>
        {
            ["DefaultOpen"] = true,
            ["data-testid"] = "root",
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.AddContent(40, "child ");
                builder.AddMarkupContent(41, "<i>markup</i>");
            }),
        });

        Assert.Equal($"Element:{HostSequence}", Shape(frames.Array[0]));
        AssertAllAttributes(frames, 0);
        Assert.Equal(
            [$"ElementReferenceCapture:{PartCaptureSequence}", $"Region:{ChildrenSequence}"],
            Children(frames, 0));
        Assert.Equal(["Text:40", "Markup:41"], RegionContent(frames, 0));
    }

    [Fact(DisplayName = "a substitute element with a consumer @ref and @key carries the literal sequence numbers and its children keep their own")]
    [Trait("Requirement", "ADR16-3")]
    public void A_substitute_element_with_a_consumer_ref_and_key_carries_the_literal_sequence_numbers_and_its_children_keep_their_own()
    {
        RenderFragment substitute = new CaptureConsumer().ElementSubstitute();
        ArrayRange<RenderTreeFrame> frames = renderer.RenderSampleRoot(new Dictionary<string, object?>
        {
            ["DefaultOpen"] = true,
            ["Render"] = substitute,
        });

        Assert.Equal($"Element:{HostSequence}", Shape(frames.Array[0]));
        Assert.Equal("span", frames.Array[0].ElementName);
        Assert.Equal("k", frames.Array[0].ElementKey);
        AssertAllAttributes(frames, 0);
        Assert.Equal(
            [$"ElementReferenceCapture:{PartCaptureSequence}", $"ElementReferenceCapture:{ConsumerCaptureSequence}", $"Region:{ChildrenSequence}"],
            Children(frames, 0));
        Assert.Equal(SubstituteChildren(substitute), RegionContent(frames, 0));
    }

    [Fact(DisplayName = "a substitute component with a consumer @ref and @key carries the literal sequence numbers")]
    [Trait("Requirement", "ADR16-3")]
    public void A_substitute_component_with_a_consumer_ref_and_key_carries_the_literal_sequence_numbers()
    {
        ArrayRange<RenderTreeFrame> frames = renderer.RenderSampleRoot(new Dictionary<string, object?>
        {
            ["DefaultOpen"] = true,
            ["Render"] = new CaptureConsumer().ComponentSubstitute(),
        });

        Assert.Equal($"Component:{HostSequence}", Shape(frames.Array[0]));
        Assert.Equal(typeof(CaptureTarget), frames.Array[0].ComponentType);
        Assert.Equal("k", frames.Array[0].ComponentKey);
        AssertAllAttributes(frames, 0);
        Assert.Equal([$"ComponentReferenceCapture:{PartCaptureSequence}"], Children(frames, 0));
    }

    [Fact(DisplayName = "a substitute element with several captures, a named event and children before and after keeps every number a literal")]
    [Trait("Requirement", "ADR16-3")]
    public void A_substitute_element_with_several_captures_a_named_event_and_children_before_and_after_keeps_every_number_a_literal()
    {
        // Razor emits one capture per element, before the children; a hand-written fragment can emit any number anywhere.
        RenderFragment substitute = builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "title", "t");
            builder.AddElementReferenceCapture(2, _ => { });
            builder.AddElementReferenceCapture(3, _ => { });
            builder.AddNamedEvent("onsubmit", "first");
            builder.AddContent(10, "a");
            builder.OpenElement(20, "i");
            builder.AddAttribute(21, "title", "inner");
            builder.AddContent(22, "x");
            builder.CloseElement();
            builder.OpenRegion(30);
            builder.AddContent(0, "r");
            builder.CloseRegion();
            builder.AddElementReferenceCapture(4, _ => { });
            builder.AddElementReferenceCapture(5, _ => { });
            builder.CloseElement();
        };
        ArrayRange<RenderTreeFrame> frames = renderer.RenderSampleRoot(new Dictionary<string, object?>
        {
            ["Render"] = substitute,
        });

        Assert.Equal($"Element:{HostSequence}", Shape(frames.Array[0]));
        AssertAllAttributes(frames, 0);
        List<string> children = Children(frames, 0);
        Assert.Equal(
            [
                $"ElementReferenceCapture:{PartCaptureSequence}",
                $"ElementReferenceCapture:{ConsumerCaptureSequence}",
                $"ElementReferenceCapture:{ConsumerCaptureSequence}",
                children[3],
                $"Region:{ChildrenSequence}",
                $"ElementReferenceCapture:{TrailingCaptureSequence}",
                $"ElementReferenceCapture:{TrailingCaptureSequence}",
            ],
            children);
        Assert.StartsWith("NamedEvent:", children[3]);
        Assert.Equal(SubstituteChildren(substitute), RegionContent(frames, 0));
    }

    [Fact(DisplayName = "a substitute component with several component captures keeps every number a literal")]
    [Trait("Requirement", "ADR16-3")]
    public void A_substitute_component_with_several_component_captures_keeps_every_number_a_literal()
    {
        RenderFragment substitute = builder =>
        {
            builder.OpenComponent<CaptureTarget>(0);
            builder.AddComponentParameter(1, "title", "t");
            builder.AddComponentReferenceCapture(2, _ => { });
            builder.AddComponentReferenceCapture(3, _ => { });
            builder.CloseComponent();
        };
        ArrayRange<RenderTreeFrame> frames = renderer.RenderSampleRoot(new Dictionary<string, object?>
        {
            ["Render"] = substitute,
        });

        Assert.Equal($"Component:{HostSequence}", Shape(frames.Array[0]));
        AssertAllAttributes(frames, 0);
        Assert.Equal(
            [$"ComponentReferenceCapture:{PartCaptureSequence}", $"ComponentReferenceCapture:{PartCaptureSequence}"],
            Children(frames, 0));
    }

    [Fact(DisplayName = "the content after the host element sits in a region at its literal and keeps the part's own numbers")]
    [Trait("Requirement", "ADR16-3")]
    public void The_content_after_the_host_element_sits_in_a_region_at_its_literal_and_keeps_the_parts_own_numbers()
    {
        ArrayRange<RenderTreeFrame> frames = renderer.Render(typeof(AfterElementTests.SiblingPart), new Dictionary<string, object?>
        {
            ["data-testid"] = "root",
        });

        Assert.Equal($"Element:{HostSequence}", Shape(frames.Array[0]));
        int after = SubtreeLength(in frames.Array[0]);
        Assert.Equal($"Region:{AfterElementSequence}", Shape(frames.Array[after]));
        Assert.Equal(after + SubtreeLength(in frames.Array[after]), frames.Count);
        Assert.Equal(
            ["Element:0", "Attribute:1", "Attribute:2"],
            Flatten(frames, after + 1, after + SubtreeLength(in frames.Array[after])));
    }

    [Fact(DisplayName = "a part with nothing after its element still emits the empty region at its literal")]
    [Trait("Requirement", "ADR16-3")]
    public void A_part_with_nothing_after_its_element_still_emits_the_empty_region_at_its_literal()
    {
        ArrayRange<RenderTreeFrame> frames = renderer.RenderSampleRoot(new Dictionary<string, object?>
        {
            ["data-testid"] = "root",
        });

        int after = SubtreeLength(in frames.Array[0]);
        Assert.Equal($"Region:{AfterElementSequence}", Shape(frames.Array[after]));
        Assert.Equal(1, SubtreeLength(in frames.Array[after]));
        Assert.Equal(after + 1, frames.Count);
    }

    private static string Shape(in RenderTreeFrame frame) => $"{frame.FrameType}:{frame.Sequence}";

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

    // Every attribute frame of the element or component at index carries the one attribute literal.
    private static void AssertAllAttributes(ArrayRange<RenderTreeFrame> frames, int index)
    {
        int end = index + SubtreeLength(in frames.Array[index]);
        int count = 0;
        for (int i = index + 1; i < end && frames.Array[i].FrameType == RenderTreeFrameType.Attribute; i++)
        {
            Assert.Equal($"Attribute:{AttributeSequence}", Shape(in frames.Array[i]));
            count++;
        }

        Assert.True(count > 0, "the element or component has no attribute frames, so the check proved nothing");
    }

    // The direct children of the element or component at index that are not attributes, each as "Type:Sequence"; a
    // region is one entry, its content is read with RegionContent.
    private static List<string> Children(ArrayRange<RenderTreeFrame> frames, int index)
    {
        List<string> children = [];
        int end = index + SubtreeLength(in frames.Array[index]);
        for (int i = index + 1; i < end; i += SubtreeLength(in frames.Array[i]))
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Attribute)
            {
                children.Add(Shape(in frames.Array[i]));
            }
        }

        return children;
    }

    // Every frame inside the first children region of the element at index, as "Type:Sequence".
    private static List<string> RegionContent(ArrayRange<RenderTreeFrame> frames, int index)
    {
        int end = index + SubtreeLength(in frames.Array[index]);
        for (int i = index + 1; i < end; i += SubtreeLength(in frames.Array[i]))
        {
            if (frames.Array[i].FrameType == RenderTreeFrameType.Region)
            {
                return Flatten(frames, i + 1, i + SubtreeLength(in frames.Array[i]));
            }
        }

        throw new InvalidOperationException("The element has no children region.");
    }

    // The children of the substitute element the fragment renders, as the consumer numbered them: everything that is
    // not an attribute, a capture or a named event, with its whole subtree.
    private static List<string> SubstituteChildren(RenderFragment substitute)
    {
        RenderTreeBuilder builder = new();
        substitute(builder);
        ArrayRange<RenderTreeFrame> frames = builder.GetFrames();
        List<string> children = [];
        int end = SubtreeLength(in frames.Array[0]);
        for (int i = 1; i < end; i += SubtreeLength(in frames.Array[i]))
        {
            switch (frames.Array[i].FrameType)
            {
                case RenderTreeFrameType.Attribute:
                case RenderTreeFrameType.ElementReferenceCapture:
                case RenderTreeFrameType.NamedEvent:
                    break;
                default:
                    children.AddRange(Flatten(frames, i, i + SubtreeLength(in frames.Array[i])));
                    break;
            }
        }

        return children;
    }

    private static List<string> Flatten(ArrayRange<RenderTreeFrame> frames, int start, int end)
    {
        List<string> shapes = [];
        for (int i = start; i < end; i++)
        {
            shapes.Add(Shape(in frames.Array[i]));
        }

        return shapes;
    }

    // bUnit gets a part's frames from Renderer.GetCurrentRenderTreeFrames, which is protected, so the test renders
    // through a Renderer of its own that exposes it.
    private sealed class FrameRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        /// <inheritdoc />
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public ArrayRange<RenderTreeFrame> RenderSampleRoot(Dictionary<string, object?> parameters) => Render(typeof(SampleRoot), parameters);

        public ArrayRange<RenderTreeFrame> Render(Type componentType, Dictionary<string, object?> parameters)
        {
            int componentId = AssignRootComponentId(InstantiateComponent(componentType));
            Dispatcher.InvokeAsync(() => RenderRootComponentAsync(componentId, ParameterView.FromDictionary(parameters))).GetAwaiter().GetResult();
            return GetCurrentRenderTreeFrames(componentId);
        }

        /// <inheritdoc />
        protected override void HandleException(Exception exception) => throw exception;

        /// <inheritdoc />
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
}
