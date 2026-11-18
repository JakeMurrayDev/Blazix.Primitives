using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// toast/utils/useToastLabelPart.ts (TST-API-15, TST-DOM-16, TST-ARIA-04/05/10), shared by Title and Description: the content
// is the part's own ChildContent, else the toast's fragment, else its string; with none of them the part renders nothing
// (the port's isRenderableNode over a string and a fragment: null or empty and no fragment render nothing, "0" renders).
// While rendered, the part publishes its id to its Root's slot; when it stops rendering, or goes away, it withdraws it.
internal sealed class ToastLabelBehavior(LabelIdSlot slot)
{
    private string? text;
    private RenderFragment? textFragment;
    private string? published;

    // The content to render, or null to render nothing.
    public RenderFragment? Content(RenderFragment? childContent, RenderFragment? toastContent, string? toastText)
    {
        if (childContent is not null)
        {
            return childContent;
        }

        if (toastContent is not null)
        {
            return toastContent;
        }

        if (string.IsNullOrEmpty(toastText))
        {
            return null;
        }

        if (!string.Equals(text, toastText, StringComparison.Ordinal) || textFragment is null)
        {
            string current = toastText;
            text = current;
            textFragment = builder => builder.AddContent(0, current);
        }

        return textFragment;
    }

    public void Publish(bool rendered, string? id)
    {
        if (rendered && id is not null)
        {
            // Published once per id, as Base UI's layout effect keyed on the id: a later part's id wins until it goes.
            if (!string.Equals(published, id, StringComparison.Ordinal))
            {
                Withdraw();
                published = id;
                slot.Publish(id);
            }

            return;
        }

        Withdraw();
    }

    public void Withdraw()
    {
        if (published is not null)
        {
            slot.Withdraw(published);
            published = null;
        }
    }
}
