using Iodine.Headless.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless.Tests.Components.Rendering;

/// <summary>
/// Issue 110, settled by the user 2026-11-28 as option (a), "accept and record" (ADR 16 Amendments 2026-11-28). A part
/// whose ancestor renders for the same change through a fixed <see cref="CascadingValue{TValue}"/> wrapping its output
/// renders twice: once in its own render-queue entry and once more when the cascade, a component of its own queued after
/// it, re-renders its content. Blazor counts every delegate parameter as changed, so the cascade's content re-renders even
/// when the delegate is the same instance. The title pins that shape at the pipeline level, counting renders by the class
/// function, so a framework change that removes or worsens it shows up here.
/// </summary>
public sealed class FixedCascadeRenderTests : IDisposable
{
    private readonly BunitContext context = new();

    public FixedCascadeRenderTests()
    {
        context.Services.AddIodineHeadless();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a part under a fixed cascade whose owner renders for the same change renders twice, the owner once")]
    [Trait("Requirement", "ADR16-8")]
    [Trait("Coverage", "issue-110")]
    public void A_part_under_a_fixed_cascade_whose_owner_renders_for_the_same_change_renders_twice()
    {
        Dictionary<string, int> renders = new(StringComparer.Ordinal) { ["owner"] = 0, ["member"] = 0 };
        Func<EmptyState, string?> ownerClass = _ => Count(renders, "owner");
        Func<EmptyState, string?> memberClass = _ => Count(renders, "member");
        RenderFragment content = builder =>
        {
            builder.OpenComponent<CascadeMember>(0);
            builder.AddComponentParameter(1, nameof(CascadeMember.ClassValue), memberClass);
            builder.CloseComponent();
        };
        IRenderedComponent<CascadeOwner> cut = context.Render<CascadeOwner>(p => p
            .Add(x => x.ClassValue, ownerClass)
            .Add(x => x.ChildContent, content));
        Dictionary<string, int> before = new(renders, StringComparer.Ordinal);

        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("data-open"));
        Assert.Equal(1, renders["owner"] - before["owner"]);
        Assert.Equal(2, renders["member"] - before["member"]);
    }

    private static string? Count(Dictionary<string, int> renders, string key)
    {
        renders[key]++;
        return null;
    }

    /// <summary>The shared state of <see cref="CascadeOwner"/> and its members: one flag and its listeners, in registration order.</summary>
    public sealed class CascadeContext
    {
        private readonly List<Action> listeners = [];

        public bool Open { get; private set; }

        public void Listen(Action listener) => listeners.Add(listener);

        public void Toggle()
        {
            Open = !Open;
            foreach (Action listener in listeners)
            {
                listener();
            }
        }
    }

    /// <summary>A part that wraps its whole output in one fixed cascade, the <c>FieldRoot</c> pattern, and renders on each toggle.</summary>
    public sealed class CascadeOwner : PartBase<EmptyState>
    {
        private readonly CascadeContext shared = new();
        private readonly RenderFragment renderPart;

        public CascadeOwner()
        {
            renderPart = builder => base.BuildRenderTree(builder);
        }

        /// <inheritdoc />
        protected override string DefaultTagName => "div";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            shared.Listen(StateHasChanged);
            base.OnInitialized();
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<CascadeContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<CascadeContext>.Value), shared);
            builder.AddComponentParameter(2, nameof(CascadingValue<CascadeContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<CascadeContext>.ChildContent), renderPart);
            builder.CloseComponent();
        }
    }

    /// <summary>A part under <see cref="CascadeOwner"/> that toggles the shared flag on click and renders on each toggle.</summary>
    public sealed class CascadeMember : PartBase<EmptyState>
    {
        private readonly EventCallback onClick;

        public CascadeMember()
        {
            onClick = CreateHandler<MouseEventArgs>("onclick", _ =>
            {
                Shared!.Toggle();
                return Task.CompletedTask;
            });
        }

        [CascadingParameter]
        public CascadeContext? Shared { get; set; }

        /// <inheritdoc />
        protected override string DefaultTagName => "button";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            Shared!.Listen(StateHasChanged);
            base.OnInitialized();
        }

        /// <inheritdoc />
        protected override void AddPartAttributes(AttributeList attributes)
        {
            attributes.Set("onclick", onClick);
            if (Shared!.Open)
            {
                attributes.Set("data-open", "true");
            }
        }
    }
}
