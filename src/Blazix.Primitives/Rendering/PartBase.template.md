# Contributor template: a part on `PartBase<TState>`

The shape every library part takes (ADR 02 decision 4, ADR 08 §5, ADR 16). Members follow the eleven-group order:
constants, fields, private properties, parameter properties, public properties, internal properties, lifecycle
methods, dispose, public methods, internal methods, private methods. Private instance fields carry no prefix,
private static fields are `s_camelCase`, constants are PascalCase.

```csharp
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

/// <summary>The state record (ADR 08 §3): sealed, partial, [PartState]; the generator projects it to data-* attributes.</summary>
[PartState]
public sealed partial record SwitchRootState(bool Checked, bool Disabled, [property: MirroredField] bool AnchorHidden);

/// <summary>Switch.Root (docs/components/switch.md).</summary>
public sealed class SwitchRoot : PartBase<SwitchRootState>
{
    // 1. constants
    private const string DefaultTag = "button";

    // 2. fields: one EventCallback per event, created once (ADR 16 rule 2); cached state
    private readonly EventCallback onClick;
    private SwitchRootState? state;
    private bool isChecked;

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

    // 5. public properties
    public bool Checked => isChecked;

    // 6. internal properties

    // 7. lifecycle methods: the base draws the generated id and calls SubscribeSelectors from OnInitialized
    protected override string DefaultTagName => DefaultTag;

    protected override bool UsesGeneratedId => true;

    protected override SwitchRootState State => state ??= new SwitchRootState(isChecked, Disabled, AnchorHidden: false);

    protected override void OnInitialized()
    {
        isChecked = DefaultChecked;
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        state = null;
    }

    /// <summary>Part-supplied attributes (RND §2.1 row 2): role, ARIA, the handlers. The generated id is already added.</summary>
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "switch");
        attributes.Set("aria-checked", isChecked ? "true" : "false");
        attributes.Set("onclick", onClick);
    }

    // 8. dispose (release twins and subscriptions here; call base)
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    // 9. public methods

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
`StyleValue`, `Enabled`, `Element`, `GeneratedId`, `Id`, `CreateHandler`, `SubscribeSelectors` (the store seam of
ADR 05 §1), `IHandleEvent` and `IDisposable`. What a part supplies: `DefaultTagName`, `State`, optionally
`StateAttributesMapping`, `UsesGeneratedId` and `AddPartAttributes`. Internal elements (hidden inputs, indicators)
are rendered inline by the part inside `ChildContent` or through `AddPartAttributes`, never as a nested library
component (ADR 16 rule 10).
