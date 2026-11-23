using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Iodine.Headless.Utilities;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// SLD §11 (SliderRootContext); slider/root/SliderRootContext.ts:110-118. Cascaded fixed by SliderRoot (ADR 16 rule 9): the
// parts read the store through their selections (design point 3 (a) of track 4), and the non-reactive registries below
// (the thumbs by index, the control, the indicators) feed the `slider` module's options, which the Root sends after the
// batch that changed them. The store's Values is the one normalized list every part renders from (SLD-STA-03).
/// <summary>The state a slider root shares with every part in its subtree.</summary>
internal sealed class SliderRootContext
{
    private readonly List<SliderThumbEntry> thumbs = [];
    private readonly List<SliderIndicatorEntry> indicators = [];
    private readonly Action syncRequested;

    /// <summary>Initializes a new instance of the <see cref="SliderRootContext"/> class.</summary>
    /// <param name="initial">The store's first record.</param>
    /// <param name="thumbList">The registry of the thumbs, or <see langword="null"/> when none is kept.</param>
    /// <param name="field">The root's field half.</param>
    /// <param name="syncRequested">Asks the root to send the module what changed, after the current batch.</param>
    public SliderRootContext(SliderStoreState initial, CompositeListContext? thumbList, SliderField field, Action syncRequested)
    {
        Store = new Store<SliderStoreState>(initial);
        ThumbList = thumbList;
        Field = field;
        this.syncRequested = syncRequested;
    }

    /// <summary>Gets the store the parts subscribe to.</summary>
    /// <value>The slider's store.</value>
    public Store<SliderStoreState> Store { get; }

    /// <summary>Gets the current record.</summary>
    /// <value>The store's state.</value>
    public SliderStoreState State => Store.State;

    /// <summary>Gets the registry the thumbs join, for their indexes in document order.</summary>
    /// <value>The registry, or <see langword="null"/>.</value>
    public CompositeListContext? ThumbList { get; }

    /// <summary>Gets the root's field half: the field flags, the message ids and the focused flag's owners.</summary>
    /// <value>The field half.</value>
    public SliderField Field { get; }

    // SLD-DOM-19, SLD-ARIA-07: the label publishes its id here; the root resolves aria-labelledby from it.
    /// <summary>Gets the slot the slider label publishes its id to.</summary>
    /// <value>The slot; the root re-resolves its labelling when the id changes.</value>
    public LabelIdSlot Label { get; } = new();

    /// <summary>Gets the thumbs that are mounted, in mount order.</summary>
    /// <value>The registrations.</value>
    public IReadOnlyList<SliderThumbEntry> Thumbs => thumbs;

    /// <summary>Gets the indicators that are mounted, in mount order.</summary>
    /// <value>The registrations.</value>
    public IReadOnlyList<SliderIndicatorEntry> Indicators => indicators;

    /// <summary>Gets or sets the mounted control.</summary>
    /// <value>The control, whose element the module listens on, or <see langword="null"/>.</value>
    public SliderControl? Control { get; set; }

    /// <summary>Gets the control's element.</summary>
    /// <value>The element, or <see langword="null"/> while no control has rendered one (or under a substitute callback).</value>
    public ElementReference? ControlElement => Control?.Element;

    /// <summary>Gets or sets a value indicating whether the control's consumer has a C# <c>onpointerdown</c> handler.</summary>
    /// <value><see langword="true"/> to ask the press decision point.</value>
    public bool ControlPressHandler { get; set; }

    /// <summary>Gets or sets the C# chain of the press decision point, which the control sets.</summary>
    /// <value>The control's decider, or <see langword="null"/>.</value>
    public Func<EventSnapshot, DecisionPolicy>? PressDecider { get; set; }

    /// <summary>Throws the error of a slider part rendered outside a slider root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static SliderRootContext Require(SliderRootContext? context)
    {
        // SLD-EDGE-01, in every build (ADR 02 Amendments 2026-10-20); Base UI's wording with the product prefix (the M5
        // Select precedent).
        return context ?? throw new InvalidOperationException(
            "Iodine: `SliderRootContext` is missing. Slider parts must be placed within `<SliderRoot>`.");
    }

    /// <summary>Adds a thumb.</summary>
    /// <param name="entry">The thumb's registration.</param>
    public void AddThumb(SliderThumbEntry entry)
    {
        thumbs.Add(entry);
        RequestSync();
    }

    /// <summary>Removes a thumb.</summary>
    /// <param name="entry">The thumb's registration.</param>
    public void RemoveThumb(SliderThumbEntry entry)
    {
        if (thumbs.Remove(entry))
        {
            RequestSync();
        }
    }

    /// <summary>Adds an indicator.</summary>
    /// <param name="entry">The indicator's registration.</param>
    public void AddIndicator(SliderIndicatorEntry entry)
    {
        indicators.Add(entry);
        RequestSync();
    }

    /// <summary>Removes an indicator.</summary>
    /// <param name="entry">The indicator's registration.</param>
    public void RemoveIndicator(SliderIndicatorEntry entry)
    {
        if (indicators.Remove(entry))
        {
            RequestSync();
        }
    }

    /// <summary>Finds the mounted thumb whose input has the given id.</summary>
    /// <param name="inputId">The input's id, or <see langword="null"/>.</param>
    /// <returns>The thumb, or <see langword="null"/>.</returns>
    public SliderThumbEntry? FindThumbByInput(string? inputId)
    {
        if (inputId is null)
        {
            return null;
        }

        for (int i = 0; i < thumbs.Count; i++)
        {
            if (string.Equals(thumbs[i].InputId, inputId, StringComparison.Ordinal))
            {
                return thumbs[i];
            }
        }

        return null;
    }

    /// <summary>Finds the mounted thumb that takes the given index.</summary>
    /// <param name="index">The value index.</param>
    /// <returns>The first thumb in mount order with that index, or <see langword="null"/>.</returns>
    public SliderThumbEntry? FindThumb(int index)
    {
        for (int i = 0; i < thumbs.Count; i++)
        {
            if (thumbs[i].Index == index)
            {
                return thumbs[i];
            }
        }

        return null;
    }

    /// <summary>Asks the root to send the module what changed after the current batch.</summary>
    public void RequestSync() => syncRequested();

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<SliderRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<SliderRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<SliderRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<SliderRootContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

// SLD-API-16/17/22/25, SLD-DOM-15: what the module and the root need of one mounted thumb.
/// <summary>The registration of one mounted thumb.</summary>
internal sealed class SliderThumbEntry
{
    /// <summary>Gets or sets the value index the thumb takes.</summary>
    /// <value>0 in single mode; the thumb's <c>Index</c>, else its registry index, in range mode; -1 before the first report.</value>
    public int Index { get; set; } = -1;

    /// <summary>Gets or sets the id of the thumb's input.</summary>
    /// <value>The id the input renders.</value>
    public string InputId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the thumb's own <c>Disabled</c> is set.</summary>
    /// <value><see langword="true"/> while the thumb alone is disabled.</value>
    public bool OwnDisabled { get; set; }

    /// <summary>Gets or sets the consumer's merged style of the thumb element.</summary>
    /// <value>The style, or <see langword="null"/>.</value>
    public string? ConsumerStyle { get; set; }

    /// <summary>Gets or sets the consumer's <c>onkeydown</c> handler of the thumb's input.</summary>
    /// <value>The handler, or the default when none.</value>
    public AttributeValue KeyDown { get; set; }

    /// <summary>Gets or sets the consumer's <c>onfocus</c> handler of the thumb's input.</summary>
    /// <value>The handler, or the default when none.</value>
    public AttributeValue Focus { get; set; }

    /// <summary>Gets or sets the consumer's <c>onblur</c> handler of the thumb's input.</summary>
    /// <value>The handler, or the default when none.</value>
    public AttributeValue Blur { get; set; }

    /// <summary>Gets or sets the owner of the field's focused flag for this thumb.</summary>
    /// <value>The thumb part.</value>
    public object Owner { get; set; } = new();
}

// SLD-DOM-05/06: an indicator's element and the consumer's style, which the module composes with its own declarations.
/// <summary>The registration of one mounted indicator.</summary>
internal sealed class SliderIndicatorEntry
{
    /// <summary>Gets or sets the indicator.</summary>
    /// <value>The part, whose element the module writes.</value>
    public SliderIndicator? Part { get; set; }

    /// <summary>Gets the indicator's element.</summary>
    /// <value>The element, or <see langword="null"/> before its first render or under a substitute callback.</value>
    public ElementReference? Element => Part?.Element;

    /// <summary>Gets or sets the consumer's merged style of the indicator.</summary>
    /// <value>The style, or <see langword="null"/>.</value>
    public string? ConsumerStyle { get; set; }
}
