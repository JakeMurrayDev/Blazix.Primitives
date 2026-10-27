# Contributor template: a part on `PartBase<TState>`

The shape every library part takes (ADR 02 decision 4, ADR 08 §5, ADR 16). Members follow the eleven-group order:
constants, fields, private properties, parameter properties, public properties, internal properties, lifecycle
methods, dispose, public methods, internal methods, private methods. Private instance fields carry no prefix,
private static fields are `s_camelCase`, constants are PascalCase. The build checks the order (BLZX2003, with the
placements of `src/Blazix.Primitives.Analyzers/CONTEXT.md`: constructors after the fields, protected members with the
public ones, `BuildRenderTree` with the lifecycle methods, nested types last) and the `/// <inheritdoc />` on every
override (BLZX2007).

```csharp
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// ADR 08 §3: the state record is sealed, partial and [PartState]; the generator projects it to data-* attributes.
/// <summary>Represents the state of a switch root, which the part exposes as data attributes.</summary>
/// <param name="Checked">A value indicating whether the switch is on.</param>
/// <param name="Disabled">A value indicating whether the switch ignores user interaction.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; JavaScript writes its attribute, so the projection skips it.</param>
[PartState]
public sealed partial record SwitchRootState(bool Checked, bool Disabled, [property: MirroredField] bool AnchorHidden);

// Base UI's Switch.Root; the contract is docs/components/switch.md.
/// <summary>Represents a control that turns a setting on or off: Base UI's <c>Switch.Root</c>, rendered as a <c>button</c>.</summary>
public sealed class SwitchRoot : PartBase<SwitchRootState>
{
    // 1. constants
    private const string DefaultTag = "button";

    // 2. fields: one EventCallback per event, created once (ADR 16 rule 2); cached state
    private readonly EventCallback onClick;
    private SwitchRootState? state;
    private bool isChecked;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="SwitchRoot"/> class.</summary>
    public SwitchRoot()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
    }

    // 3. private properties

    // 4. parameter properties (PascalCase of the Base UI prop; never `Class` or `Style`, ADR 08 §2)
    [Parameter]
    public bool DefaultChecked { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    public bool Checked => isChecked;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override SwitchRootState State => state ??= new SwitchRootState(isChecked, Disabled, AnchorHidden: false);

    // 6. internal properties

    // 7. lifecycle methods: the base draws the generated id and calls SubscribeSelectors from OnInitialized
    /// <inheritdoc />
    protected override void OnInitialized()
    {
        isChecked = DefaultChecked;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        state = null;
    }

    // 8. dispose (release twins and subscriptions here; call base)
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    // Part-supplied attributes (RND §2.1 row 2): role, ARIA, the handlers. The generated id is already added.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "switch");
        attributes.Set("aria-checked", isChecked ? "true" : "false");
        attributes.Set("onclick", onClick);
    }

    // 10. internal methods

    // 11. private methods: the handler body runs after the substitute's and the consumer's handlers unless vetoed;
    //     it renders only by calling StateHasChanged (ADR 16 rule 8)
    private Task HandleClickAsync(MouseEventArgs e)
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        isChecked = !isChecked;
        state = null;
        StateHasChanged();
        return Task.CompletedTask;
    }
}
```

What the base provides: `AdditionalAttributes` (the bag), `ChildContent`, `Render`, `RenderFunction`, `ClassValue`,
`StyleValue`, `Enabled`, `Element`, `GeneratedId`, `Id`, `CreateHandler`, `SubscribeSelectors` (called once
before the first render; a part that reads a store overrides it, calls `SubscribeTo(store)` once and adds its
selections with `Select(selector)` / `Select(selector, arg)`; the part then re-renders only when a selection changed, and
the subscription is disposed with the part, ADR 05 §1), `IHandleEvent` and `IDisposable`. What a part supplies: `DefaultTagName`, `State`, optionally
`StateAttributesMapping`, `UsesGeneratedId`, `AddPartAttributes`, `BuildBeforeElement` and `BuildAfterElement`.
Internal elements are rendered inline by the part, never as a nested library component (ADR 16 rule 10): inside the
element through `ChildContent` (an indicator), after it as its siblings through `BuildAfterElement` (the hidden inputs
of Switch, Checkbox and Radio, which label activation needs outside the root; ADR 02 Amendments 2026-10-22), or before
it as its siblings through `BuildBeforeElement` (a popup's leading focus guards; ADR 02 Amendments 2026-10-27). Both
number their frames from 0: the pipeline wraps each call in a region of its own and skips it while `Enabled` is false.
`src/Blazix.Primitives/Rendering/CONTEXT.md`'s human check says which siblings belong there.
