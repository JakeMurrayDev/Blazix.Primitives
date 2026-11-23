using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// ADR 02, second verification item (issue 4): the frame re-emitter preserves <c>@key</c> and <c>@ref</c> semantics
/// across re-renders for a substitute whose root is a consumer component (RND-DOM-43, RND-DOM-45).
/// </summary>
public sealed class SubstituteComponentIdentityTests : IDisposable
{
    private readonly BunitContext context = new();

    public SubstituteComponentIdentityTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a consumer component root keeps its instance and its @ref across re-renders of the part")]
    [Trait("Verifies", "ADR02-2")]
    [Trait("Requirement", "RND-DOM-43")]
    public void A_consumer_component_root_keeps_its_instance_and_its_ref_across_re_renders_of_the_part()
    {
        Probe.Constructions = 0;
        List<Probe> captured = [];
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("data-testid", "root")
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenComponent<Probe>(0);
                builder.SetKey("stable");
                builder.AddComponentReferenceCapture(1, component => captured.Add((Probe)component));
                builder.CloseComponent();
            })));

        Probe first = cut.FindComponent<Probe>().Instance;
        Assert.Single(captured);
        Assert.Same(first, captured[0]);
        Assert.Equal(1, first.Renders);
        Assert.Equal(cut.Instance.ResolvedId, cut.Find("[data-testid=root]").Id);

        cut.InvokeAsync(() => cut.Instance.Refresh());
        cut.Render(p => p.AddUnmatched("data-testid", "root").Add(x => x.Label, "changed"));

        Probe after = cut.FindComponent<Probe>().Instance;
        Assert.Same(first, after);
        Assert.Equal(1, Probe.Constructions);
        Assert.Same(first, captured[^1]);
        Assert.Equal("changed", cut.Find("[data-testid=root]").GetAttribute("data-label"));
        Assert.Equal("group", cut.Find("[data-testid=root]").GetAttribute("role"));
    }

    [Fact(DisplayName = "a changed @key on a consumer component root creates a new instance and recaptures it")]
    [Trait("Verifies", "ADR02-2")]
    [Trait("Requirement", "RND-DOM-45")]
    public void A_changed_key_on_a_consumer_component_root_creates_a_new_instance_and_recaptures_it()
    {
        Probe.Constructions = 0;
        List<Probe> captured = [];
        RenderFragment Substitute(string key) => builder =>
        {
            builder.OpenComponent<Probe>(0);
            builder.SetKey(key);
            builder.AddComponentReferenceCapture(1, component => captured.Add((Probe)component));
            builder.CloseComponent();
        };

        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.Add(x => x.Render, Substitute("a")));
        Probe first = cut.FindComponent<Probe>().Instance;

        cut.Render(p => p.Add(x => x.Render, Substitute("b")));

        Probe second = cut.FindComponent<Probe>().Instance;
        Assert.NotSame(first, second);
        Assert.Equal(2, Probe.Constructions);
        Assert.Same(second, captured[^1]);
        Assert.True(first.Disposed);
    }

    /// <summary>A consumer component that counts its constructions and renders, spreading what it receives onto a <c>span</c> (RND-DOM-43's contract).</summary>
    public sealed class Probe : ComponentBase, IDisposable
    {
        [ThreadStatic]
        private static int s_constructions;

        public Probe()
        {
            s_constructions++;
        }

        public static int Constructions
        {
            get => s_constructions;
            set => s_constructions = value;
        }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? Attributes { get; set; }

        public int Renders { get; private set; }

        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Renders++;
            builder.OpenElement(0, "span");
            builder.AddMultipleAttributes(1, Attributes);
            builder.CloseElement();
        }
    }
}
