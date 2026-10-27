using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// The C# half of a family's Arrow part (FLT-DOM-09/10; ADR 04 §2's Arrow row and Amendments 2026-10-27). The Arrow has no
// handle of its own (design point 6 of the positioning track, settled by the orchestrator 2026-10-27): it publishes its id
// and its consumer's merged style through the positioner's context, both travel in the Positioner's options, and the
// `positioning` module re-resolves it by id. Before the handover it renders Base UI's arrow before the first pass:
// `aria-hidden="true"`, `position: absolute` under the consumer's style, and data-side, data-align and data-uncentered from
// the mirror; from the first attach or update of the Positioner that carries its id those are the module's, so the values
// rendered then are frozen (the ADR 04 Amendments 2026-10-18 handover) and the consumer's later styles travel in the
// Positioner's update. A change of the mirror renders the Arrow only when its side, align or uncentered changed.
/// <summary>
/// The arrow of one positioner: its registration through the positioner's context, the attributes rendered before the
/// handover and frozen at it, and the mirror the part's state reads.
/// </summary>
internal sealed class PositionerArrowBehavior : IDisposable
{
    private readonly Action mirrorChanged;
    private PositionerContext? context;
    private string? renderedStyle;
    private string? renderedSide;
    private string? renderedAlign;
    private bool renderedUncentered;
    private bool frozen;
    private string? frozenStyle;
    private string? frozenSide;
    private string? frozenAlign;
    private bool frozenUncentered;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="PositionerArrowBehavior"/> class.</summary>
    /// <param name="stateChanged">Renders the arrow again; called when its mirrored values change.</param>
    public PositionerArrowBehavior(Action stateChanged)
    {
        mirrorChanged = () =>
        {
            if (!disposed)
            {
                stateChanged();
            }
        };
    }

    /// <summary>Gets the placement the positioner's browser-side behavior last rendered.</summary>
    /// <value>The positioner's mirror.</value>
    /// <exception cref="InvalidOperationException">No positioner context was set.</exception>
    public PositionerMirror Mirror => context?.Mirror ?? throw new InvalidOperationException("Blazix: an arrow needs a positioner.");

    /// <summary>Gets the id of the arrow's element, which the browser-side behavior finds it by.</summary>
    /// <value>The id rendered on the element, or <see langword="null"/> before the first render.</value>
    internal string? Id { get; private set; }

    /// <summary>Gets the consumer's merged style of the arrow.</summary>
    /// <value>The style string, or <see langword="null"/> for none.</value>
    internal string? ConsumerStyle { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (context is not null)
        {
            context.RemoveListener(mirrorChanged);
            context.Unregister(this);
            context = null;
        }
    }

    /// <summary>Registers the arrow with the nearest positioner.</summary>
    /// <param name="next">The cascaded context.</param>
    public void SetContext(PositionerContext next)
    {
        if (ReferenceEquals(next, context) || disposed)
        {
            return;
        }

        if (context is not null)
        {
            context.RemoveListener(mirrorChanged);
            context.Unregister(this);
        }

        context = next;
        context.AddListener(mirrorChanged);
        context.Register(this);
    }

    /// <summary>Adds the arrow's own attributes to a render: <c>aria-hidden</c> and the placement.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("aria-hidden", "true");
        PositionerMirror mirror = Mirror;
        renderedSide = frozen ? frozenSide : FloatingLiterals.ToLiteral(mirror.Side);
        renderedAlign = frozen ? frozenAlign : FloatingLiterals.ToLiteral(mirror.Align);
        renderedUncentered = frozen ? frozenUncentered : mirror.Uncentered;
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes), ADR 04 Amendments 2026-10-18 (the handover).
    /// <summary>Renders the handed-over attributes over the merged ones and records the id and the consumer's style.</summary>
    /// <param name="attributes">The merged attributes.</param>
    /// <param name="id">The id the part renders on its element.</param>
    public void FinishAttributes(AttributeList attributes, string id)
    {
        string? merged = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
        if (!string.Equals(id, Id, StringComparison.Ordinal) || !string.Equals(merged, ConsumerStyle, StringComparison.Ordinal))
        {
            Id = id;
            ConsumerStyle = merged;
            context?.ArrowChanged();
        }

        renderedStyle = frozen ? frozenStyle : string.IsNullOrEmpty(ConsumerStyle) ? "position: absolute;" : $"position: absolute; {ConsumerStyle}";
        attributes.Set(MergeProps.StyleAttribute, renderedStyle);
        attributes.Set("data-side", renderedSide!);
        attributes.Set("data-align", renderedAlign!);
        if (renderedUncentered)
        {
            attributes.Set("data-uncentered", true);
        }
        else
        {
            attributes.Remove("data-uncentered");
        }
    }

    /// <summary>Sends the positioner's options when the arrow's render changed its id or style.</summary>
    /// <returns>A task that completes when the browser-side behavior has the options.</returns>
    public Task AfterRenderAsync() => context?.FlushAsync() ?? Task.CompletedTask;

    /// <summary>Freezes the values rendered last: the positioner sent the arrow's id, so the browser-side behavior owns them.</summary>
    internal void HandOver()
    {
        if (frozen)
        {
            return;
        }

        frozen = true;
        frozenStyle = renderedStyle;
        frozenSide = renderedSide;
        frozenAlign = renderedAlign;
        frozenUncentered = renderedUncentered;
    }
}
