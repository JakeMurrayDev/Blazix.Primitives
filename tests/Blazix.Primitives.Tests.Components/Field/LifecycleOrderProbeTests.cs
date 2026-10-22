using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The ADR 01 probe of issue 77: Blazor's order of <c>OnAfterRender</c> and <c>Dispose</c> against FFI §8 ("after DOM
/// update, before paint" callbacks child-before-parent, cleanup-before-setup on replacement) and FFI-ALG-07's
/// unmount-before-replacement. The assertions record what Blazor does, so a framework change that alters the order fails
/// here first. The field parts rely on neither difference: a control claims its id and registers after its own render, and
/// no enclosing part's after-render reads those registrations (each notifies the field scope); siblings run in document
/// order on both platforms; and the replaced control's withdrawal precedes the replacement's claim, as FFI-ALG-07 needs.
/// The label, description and legend publications run in <c>OnParametersSet</c> (ADR 01 Amendments 2026-10-22).
/// </summary>
public sealed class LifecycleOrderProbeTests : IDisposable
{
    private readonly BunitContext context = new();
    private readonly List<string> log = [];

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "Blazor runs OnAfterRender parent before child, the reverse of FFI §8's layout-effect order")]
    [Trait("Verifies", "ADR01-FFI8")]
    public void Blazor_runs_OnAfterRender_parent_before_child()
    {
        context.Render<Probe>(p => p.Add(x => x.Name, "parent").Add(x => x.Log, log).Add(x => x.Children, ["child"]));

        Assert.Equal(["parameters parent", "parameters child", "after parent", "after child"], log);
    }

    [Fact(DisplayName = "Blazor disposes a replaced component before the replacement's OnAfterRender, as FFI §8's cleanup-before-setup")]
    [Trait("Verifies", "ADR01-FFI8")]
    [Trait("Requirement", "FFI-ALG-07")]
    public void Blazor_disposes_a_replaced_component_before_the_replacements_OnAfterRender()
    {
        IRenderedComponent<Probe> cut = context.Render<Probe>(p => p.Add(x => x.Name, "parent").Add(x => x.Log, log).Add(x => x.Children, ["a"]));
        log.Clear();

        cut.Render(p => p.Add(x => x.Children, ["b"]));

        Assert.Equal(["parameters parent", "parameters b", "dispose a", "after parent", "after b"], log);
    }

    /// <summary>A component that logs its lifecycle and renders one keyed child probe per name.</summary>
    public sealed class Probe : ComponentBase, IDisposable
    {
        [Parameter]
        public string Name { get; set; } = string.Empty;

        [Parameter]
        public List<string> Log { get; set; } = [];

        [Parameter]
        public IReadOnlyList<string> Children { get; set; } = [];

        /// <inheritdoc />
        public void Dispose() => Log.Add($"dispose {Name}");

        /// <inheritdoc />
        protected override void OnParametersSet() => Log.Add($"parameters {Name}");

        /// <inheritdoc />
        protected override void OnAfterRender(bool firstRender) => Log.Add($"after {Name}");

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            foreach (string child in Children)
            {
                builder.OpenComponent<Probe>(0);
                builder.SetKey(child);
                builder.AddComponentParameter(1, nameof(Name), child);
                builder.AddComponentParameter(2, nameof(Log), Log);
                builder.CloseComponent();
            }
        }
    }
}
