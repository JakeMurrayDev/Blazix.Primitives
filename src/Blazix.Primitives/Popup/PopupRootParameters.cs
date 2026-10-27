using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// PSH §3.2 and ADR 08 Amendments 2026-10-27 (the shared Root parameters): what every family's Root hands its
// PopupRootBehavior after its parameters are assigned. Open and TriggerId are read from the ParameterView by the behavior,
// since whether they were supplied decides control.
/// <summary>Holds the shared parameters of a popup root, as its behavior applies them.</summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
/// <param name="DefaultOpen">The root's <c>DefaultOpen</c>.</param>
/// <param name="DefaultTriggerId">The root's <c>DefaultTriggerId</c>.</param>
/// <param name="Handle">The root's <c>Handle</c>, or <see langword="null"/>.</param>
/// <param name="OnOpenChange">The root's <c>OnOpenChange</c>.</param>
/// <param name="OpenChanged">The root's <c>OpenChanged</c>.</param>
/// <param name="OnOpenChangeComplete">The root's <c>OnOpenChangeComplete</c>.</param>
/// <param name="CloseOnActiveTriggerUnmount">Whether the family closes the popup when its active trigger unmounts (Tooltip, PreviewCard).</param>
/// <param name="ChildContent">The root's <c>ChildContent</c>.</param>
/// <param name="PayloadContent">The root's <c>PayloadContent</c>.</param>
internal readonly record struct PopupRootParameters<TPayload>(
    bool DefaultOpen,
    string? DefaultTriggerId,
    PopupHandle<TPayload>? Handle,
    EventCallback<ValueChangeEventArgs<bool>> OnOpenChange,
    EventCallback<bool> OpenChanged,
    EventCallback<bool> OnOpenChangeComplete,
    bool CloseOnActiveTriggerUnmount,
    RenderFragment? ChildContent,
    RenderFragment<TPayload?>? PayloadContent);
