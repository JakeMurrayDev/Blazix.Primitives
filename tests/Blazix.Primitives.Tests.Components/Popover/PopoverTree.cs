using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>What a test's popover tree holds: the Root's parameters (only the supplied ones are passed) and the parts.</summary>
internal sealed record PopoverSpec
{
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public ModalValue? Modal { get; init; }

    public bool TriggerIdSupplied { get; init; }

    public string? TriggerId { get; init; }

    public string? DefaultTriggerId { get; init; }

    public PopoverHandle<int>? Handle { get; init; }

    public bool DetachedTriggers { get; init; }

    public int Triggers { get; init; } = 1;

    public bool OpenOnHover { get; init; }

    public bool TriggerDisabled { get; init; }

    public bool KeepMounted { get; init; }

    public bool Portal { get; init; } = true;

    public bool CloseInside { get; init; }

    public bool CloseOutside { get; init; }

    public bool Title { get; init; }

    public bool Description { get; init; }

    public bool Backdrop { get; init; }

    public bool Viewport { get; init; }

    public bool Arrow { get; init; }

    public bool RootMounted { get; init; } = true;

    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; init; }

    public EventCallback<bool> OpenChanged { get; init; }

    public EventCallback<bool> OnOpenChangeComplete { get; init; }

    public Func<PopoverTriggerState, string?>? TriggerClass { get; init; }

    public Func<PopoverBackdropState, string?>? BackdropClass { get; init; }

    public Action<PopoverRoot<int>>? CaptureRoot { get; init; }
}

/// <summary>
/// Builds the tree most Popover tests render: triggers <c>trigger-1</c>.. (number as payload, inside the Root or detached
/// with a handle), the Root's payload template as <c>root-payload</c>, then a Portal, a Positioner and a Popup holding
/// <c>content</c> (the payload) and the parts the spec asks for.
/// </summary>
internal static class PopoverTree
{
    /// <summary>Renders the tree of <paramref name="spec"/>.</summary>
    public static void Build(RenderTreeBuilder builder, PopoverSpec spec)
    {
        if (spec.DetachedTriggers)
        {
            Triggers(builder, spec, spec.Handle);
        }

        if (!spec.RootMounted)
        {
            return;
        }

        builder.OpenComponent<PopoverRoot<int>>(0);
        if (spec.Open is { } open)
        {
            builder.AddComponentParameter(1, nameof(PopoverRoot<int>.Open), open);
        }

        builder.AddComponentParameter(2, nameof(PopoverRoot<int>.DefaultOpen), spec.DefaultOpen);
        if (spec.Modal is not null)
        {
            builder.AddComponentParameter(3, nameof(PopoverRoot<int>.Modal), spec.Modal);
        }

        if (spec.TriggerIdSupplied)
        {
            builder.AddComponentParameter(4, nameof(PopoverRoot<int>.TriggerId), spec.TriggerId);
        }

        if (spec.DefaultTriggerId is not null)
        {
            builder.AddComponentParameter(5, nameof(PopoverRoot<int>.DefaultTriggerId), spec.DefaultTriggerId);
        }

        if (spec.Handle is not null)
        {
            builder.AddComponentParameter(6, nameof(PopoverRoot<int>.Handle), spec.Handle);
        }

        if (spec.OnOpenChange.HasDelegate)
        {
            builder.AddComponentParameter(7, nameof(PopoverRoot<int>.OnOpenChange), spec.OnOpenChange);
        }

        if (spec.OpenChanged.HasDelegate)
        {
            builder.AddComponentParameter(8, nameof(PopoverRoot<int>.OpenChanged), spec.OpenChanged);
        }

        if (spec.OnOpenChangeComplete.HasDelegate)
        {
            builder.AddComponentParameter(9, nameof(PopoverRoot<int>.OnOpenChangeComplete), spec.OnOpenChangeComplete);
        }

        builder.AddComponentParameter(10, nameof(PopoverRoot<int>.ChildContent), (RenderFragment)(b => RootContent(b, spec)));
        builder.AddComponentParameter(11, nameof(PopoverRoot<int>.PayloadContent), (RenderFragment<int>)(payload => b =>
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "data-testid", "root-payload");
            b.AddContent(2, payload == 0 ? "No payload" : payload.ToString(CultureInfo.InvariantCulture));
            b.CloseElement();
        }));
        if (spec.CaptureRoot is { } capture)
        {
            builder.AddComponentReferenceCapture(12, root => capture((PopoverRoot<int>)root));
        }

        builder.CloseComponent();
    }

    private static void RootContent(RenderTreeBuilder builder, PopoverSpec spec)
    {
        if (!spec.DetachedTriggers)
        {
            Triggers(builder, spec, null);
        }

        if (spec.Backdrop)
        {
            builder.OpenComponent<PopoverBackdrop>(0);
            builder.AddComponentParameter(1, "data-testid", "backdrop");
            if (spec.BackdropClass is not null)
            {
                builder.AddComponentParameter(2, nameof(PopoverBackdrop.ClassValue), spec.BackdropClass);
            }

            builder.CloseComponent();
        }

        if (spec.CloseOutside)
        {
            Close(builder, "close-outside");
        }

        if (!spec.Portal)
        {
            return;
        }

        builder.OpenComponent<PopoverPortal>(3);
        builder.AddComponentParameter(4, nameof(PopoverPortal.KeepMounted), spec.KeepMounted);
        builder.AddComponentParameter(5, "data-testid", "portal");
        builder.AddComponentParameter(6, nameof(PopoverPortal.ChildContent), (RenderFragment)(b =>
        {
            b.OpenComponent<PopoverPositioner>(0);
            b.AddComponentParameter(1, "data-testid", "positioner");
            b.AddComponentParameter(2, nameof(PopoverPositioner.ChildContent), (RenderFragment)(p =>
            {
                p.OpenComponent<PopoverPopup>(0);
                p.AddComponentParameter(1, "data-testid", "popup");
                p.AddComponentParameter(2, nameof(PopoverPopup.ChildContent), (RenderFragment)(c => PopupContent(c, spec)));
                p.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private static void PopupContent(RenderTreeBuilder builder, PopoverSpec spec)
    {
        if (spec.Arrow)
        {
            builder.OpenComponent<PopoverArrow>(0);
            builder.AddComponentParameter(1, "data-testid", "arrow");
            builder.CloseComponent();
        }

        if (spec.Title)
        {
            builder.OpenComponent<PopoverTitle>(2);
            builder.AddComponentParameter(3, "data-testid", "title");
            builder.CloseComponent();
        }

        if (spec.Description)
        {
            builder.OpenComponent<PopoverDescription>(4);
            builder.AddComponentParameter(5, "data-testid", "description");
            builder.CloseComponent();
        }

        if (spec.Viewport)
        {
            builder.OpenComponent<PopoverViewport>(6);
            builder.AddComponentParameter(7, "data-testid", "viewport");
            builder.AddComponentParameter(8, nameof(PopoverViewport.ChildContent), (RenderFragment)(c => Content(c)));
            builder.CloseComponent();
        }
        else
        {
            Content(builder);
        }

        if (spec.CloseInside)
        {
            Close(builder, "close");
        }
    }

    private static void Content(RenderTreeBuilder builder)
    {
        builder.OpenElement(9, "span");
        builder.AddAttribute(10, "data-testid", "content");
        builder.AddContent(11, "Content");
        builder.CloseElement();
    }

    private static void Close(RenderTreeBuilder builder, string testId)
    {
        builder.OpenRegion(12);
        builder.OpenComponent<PopoverClose>(0);
        builder.AddComponentParameter(1, "data-testid", testId);
        builder.CloseComponent();
        builder.CloseRegion();
    }

    private static void Triggers(RenderTreeBuilder builder, PopoverSpec spec, PopoverHandle<int>? handle)
    {
        for (int i = 1; i <= spec.Triggers; i++)
        {
            string id = "trigger-" + i.ToString(CultureInfo.InvariantCulture);
            builder.OpenComponent<PopoverTrigger<int>>(13);
            builder.SetKey(id);
            builder.AddComponentParameter(14, "id", id);
            builder.AddComponentParameter(15, "data-testid", id);
            builder.AddComponentParameter(16, nameof(PopoverTrigger<int>.Payload), i);
            builder.AddComponentParameter(17, nameof(PopoverTrigger<int>.OpenOnHover), spec.OpenOnHover);
            builder.AddComponentParameter(18, nameof(PopoverTrigger<int>.Disabled), spec.TriggerDisabled);
            if (handle is not null)
            {
                builder.AddComponentParameter(19, nameof(PopoverTrigger<int>.Handle), handle);
            }

            if (spec.TriggerClass is not null)
            {
                builder.AddComponentParameter(20, nameof(PopoverTrigger<int>.ClassValue), spec.TriggerClass);
            }

            builder.AddComponentParameter(21, nameof(PopoverTrigger<int>.ChildContent), (RenderFragment)(b => b.AddContent(0, "Toggle")));
            builder.CloseComponent();
        }
    }
}
