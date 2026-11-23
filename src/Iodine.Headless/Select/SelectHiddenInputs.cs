using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// SEL-STR-01, SEL-DOM-47..53 (select/root/SelectRoot.tsx:486-586): the form-participation input after the root's
// children, then one `<input type="hidden">` per value of a named multiple select. A child component of the root, so a
// value change renders the inputs alone, never the root's children (ADR 16 rules 8, 9).
// - The shared input's `value` is C#'s until the `form` module attaches, then the module's (C# → JS; ADR 04 Amendments
//   2026-11-04, track 4's form additions): C# keeps rendering the value it rendered at the handover, so Blazor never writes
//   the property again, and sends each committed value through the module's options.
// - Every other attribute is C#'s (SEL §5.17's Writer column).
/// <summary>Renders a select's hidden form inputs and runs its field half after each render.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
internal sealed class SelectHiddenInputs<TValue> : ComponentBase, IDisposable
{
    private readonly Action fieldChanged;
    private SelectorSubscription<SelectStoreState>? subscription;
    private ElementReference input;
    private string? renderedValue;
    private bool handedOver;
    private int boxedVersion = -1;
    private object? formValue;
    private Func<object?>? getValue;
    private Func<bool>? dirty;

    /// <summary>Initializes a new instance of the <see cref="SelectHiddenInputs{TValue}"/> class.</summary>
    public SelectHiddenInputs()
    {
        fieldChanged = () => _ = InvokeAsync(StateHasChanged);
    }

    private SelectStore<TValue> Store => Root.Store;

    /// <summary>Gets or sets the root the inputs belong to.</summary>
    [Parameter]
    public SelectRoot<TValue> Root { get; set; } = null!;

    /// <summary>Gets or sets the root's own form name.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Gets or sets the id of the form the inputs belong to.</summary>
    [Parameter]
    public string? Form { get; set; }

    /// <summary>Gets or sets the shared input's autofill hint.</summary>
    [Parameter]
    public string? AutoComplete { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        subscription = new SelectorSubscription<SelectStoreState>(Store, StateHasChanged);
        subscription.Select(SelectStoreState.ValueVersionSelector);
        subscription.Select(SelectStoreState.DisabledSelector);
        subscription.Select(SelectStoreState.ReadOnlySelector);
        subscription.Select(SelectStoreState.RequiredSelector);
        subscription.Select(SelectStoreState.MultipleSelector);
        subscription.Select(SelectStoreState.RootIdSelector);
        subscription.Select(SelectStoreState.TriggerIdSelector);
        subscription.Select(SelectStoreState.HasSelectedValueSelector);
        Store.FieldStateChanged += fieldChanged;
        getValue = GetFormValue;
        dirty = IsDirty;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        SelectStore<TValue> store = Store;
        SelectStoreState state = store.State;
        SelectField field = Root.Field;
        string? name = field.Name;
        bool multiple = state.Multiple;
        string serialized = Serialized(store, multiple);
        if (!handedOver)
        {
            renderedValue = serialized;
        }

        // SEL-DOM-47..52.
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "aria-hidden", "true");
        builder.AddAttribute(2, "tabindex", "-1");
        builder.AddAttribute(3, "id", InputId(state, name, multiple));
        builder.AddAttribute(4, "name", multiple ? null : name);
        builder.AddAttribute(5, "form", Form);
        builder.AddAttribute(6, "autocomplete", AutoComplete);
        builder.AddAttribute(7, "value", renderedValue);
        builder.AddAttribute(8, "disabled", state.Disabled);
        builder.AddAttribute(9, "readonly", state.ReadOnly);
        builder.AddAttribute(10, "required", state.Required && !(multiple && state.HasSelectedValue));
        builder.AddAttribute(11, "aria-describedby", field.DescribedBy(null));
        builder.AddAttribute(12, "aria-invalid", field.AriaInvalid ? "true" : null);

        // SEL-DOM-49: absolutely positioned when named, so the validation bubble anchors next to the control (FFI-DOM-02).
        builder.AddAttribute(13, "style", name is null ? VisuallyHidden.Style : VisuallyHidden.InputStyle);
        builder.AddElementReferenceCapture(14, element => input = element);
        builder.CloseElement();

        // SEL-DOM-53: one per selected value, in value order, keyed by the value's text.
        if (multiple && name is not null)
        {
            IReadOnlyList<TValue> values = store.Values;
            for (int i = 0; i < values.Count; i++)
            {
                string text = ValueLabels.StringifyAsValue(values[i], store.ItemToStringValue);
                builder.OpenElement(15, "input");
                builder.SetKey(text);
                builder.AddAttribute(16, "type", "hidden");
                builder.AddAttribute(17, "name", name);
                builder.AddAttribute(18, "value", text);
                builder.AddAttribute(19, "form", Form);
                builder.AddAttribute(20, "disabled", state.Disabled);
                builder.CloseElement();
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        SelectStore<TValue> store = Store;
        SelectStoreState state = store.State;
        SelectField field = Root.Field;
        string serialized = Serialized(store, state.Multiple);
        await field.AfterRenderAsync(
            state.TriggerId,
            state.RootId ?? string.Empty,
            input,
            InputId(state, field.Name, state.Multiple),
            Root.BoxedValue(),
            getValue!,
            state.ValueVersion,
            serialized,
            state.HasSelectedValue,
            dirty!);
        if (!handedOver && field.TwinAttached.IsCompleted)
        {
            handedOver = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        Store.FieldStateChanged -= fieldChanged;
    }

    // SEL-DOM-48: only a nameless input carries the id (FFI-DOM-03).
    private static string? InputId(SelectStoreState state, string? name, bool multiple) =>
        (multiple || name is null) && state.RootId is { } id ? id + "-hidden-input" : null;

    // SEL-DOM-51/52: the serialized value in single mode, the empty string in multiple mode.
    private static string Serialized(SelectStore<TValue> store, bool multiple) =>
        multiple || !store.HasValue ? string.Empty : ValueLabels.StringifyAsValue(store.Value, store.ItemToStringValue);

    // SEL-FRM-02, FFI-FRM-29: the form value is the string projection, an array of strings in multiple mode; boxed once
    // per committed value.
    private object? GetFormValue()
    {
        SelectStore<TValue> store = Store;
        int version = store.State.ValueVersion;
        if (version == boxedVersion)
        {
            return formValue;
        }

        boxedVersion = version;
        if (store.State.Multiple)
        {
            IReadOnlyList<TValue> values = store.Values;
            string[] texts = new string[values.Count];
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = ValueLabels.StringifyAsValue(values[i], store.ItemToStringValue);
            }

            formValue = texts;
        }
        else
        {
            formValue = store.HasValue ? ValueLabels.StringifyAsValue(store.Value, store.ItemToStringValue) : string.Empty;
        }

        return formValue;
    }

    // SEL-FRM-04 (isSelectedValueDirty, internals/itemEquality.ts:23-35).
    private bool IsDirty()
    {
        SelectStore<TValue> store = Store;
        return store.State.Multiple
            ? ItemEquality.IsDirty(store.ValuesState.Value, store.InitialValues, store.Comparer)
            : ItemEquality.IsDirty(store.HasValue ? store.Value : default!, store.InitialValue);
    }
}
