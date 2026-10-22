using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// SWT-STA-08, SWT-STA-04; switch/thumb/SwitchThumb.tsx:33 (the thumb's state is the root's). ADR 08 §3: one record per part.
/// <summary>Represents the state of a switch thumb, the same as its root's, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Checked">A value indicating whether the switch is on; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
/// <param name="Disabled">A value indicating whether the switch ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot toggle the switch; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether the switch must be on to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Touched">A value indicating whether the switch's field has lost focus once; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the field has a value, which for a switch is whether it is on; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the switch has focus within its field; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record SwitchThumbState(bool Checked, bool Disabled, bool ReadOnly, bool Required, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// SWT-DOM-06; switch/thumb/SwitchThumbDataAttributes.ts (the same names as the root's, switch §3.3).
/// <summary>Provides the names of the data attributes a switch thumb renders.</summary>
public static class SwitchThumbDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the switch is on: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present, with an empty value, while the switch is off: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present, with an empty value, while the switch is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the switch is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while the switch is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the switch's field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the switch's field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once the switch's field has lost focus: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the field's initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while the switch is on within a field: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while the switch has focus within a field: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Switch.Thumb (switch/thumb/SwitchThumb.tsx); the contract is docs/components/switch.md. Structural only
// (SWT-STR-03): no handler, ARIA attribute or id of its own; it re-renders when its root's state changes (the context's
// listener), and reads the root's state through the fixed cascade.
/// <summary>
/// Represents the movable part of a switch, a styling hook that mirrors its root's state: Base UI's <c>Switch.Thumb</c>,
/// rendered as a <c>span</c>. It must be placed within a <see cref="SwitchRoot"/>.
/// </summary>
public sealed class SwitchThumb : PartBase<SwitchThumbState>
{
    private const string DefaultTag = "span";

    private static readonly StateAttributesMapping<SwitchThumbState> s_mapping = SwitchStateMapping.Create<SwitchThumbState>();

    private readonly Action stateChanged;
    private SwitchRootContext? context;

    /// <summary>Initializes a new instance of the <see cref="SwitchThumb"/> class.</summary>
    public SwitchThumb()
    {
        stateChanged = StateHasChanged;
    }

    [CascadingParameter]
    private SwitchRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SwitchThumbState State => context!.ThumbState;

    /// <inheritdoc />
    protected override StateAttributesMapping<SwitchThumbState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The thumb is not placed within a <see cref="SwitchRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SwitchRootContext.Require(RootContext);
        context.AddListener(stateChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
        }

        base.Dispose(disposing);
    }
}
