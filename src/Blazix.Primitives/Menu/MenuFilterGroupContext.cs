using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// MNU-STA-24, DOM-20 (filter-dropdown/group/useFilterDropdownGroup.ts:12-64, menu/filter-root/MenuFilterGroup.tsx): what a
// Menu.Group or Menu.RadioGroup of a filterable menu cascades to its subtree. Its items and nested groups register as
// members; it is hidden while it has members and none is visible (a visible set that does not exist leaves every item
// visible, so isGroupHidden's null check is the members' own state here). A nested group is one member of its parent,
// whose visibility is its own hidden state.
/// <summary>Represents the filter half of a group in a filterable menu: its members and whether the query hides it.</summary>
internal sealed class MenuFilterGroupContext : IDisposable
{
    private readonly List<Member> members = [];
    private readonly Action hiddenChanged;
    private readonly Member? inParent;
    private readonly RenderFragment<RenderFragment?> cascade;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterGroupContext"/> class.</summary>
    /// <param name="parent">The enclosing group's context, or <see langword="null"/>.</param>
    /// <param name="hiddenChanged">The callback run when <see cref="Hidden"/> changes: the group's render.</param>
    public MenuFilterGroupContext(MenuFilterGroupContext? parent, Action hiddenChanged)
    {
        this.hiddenChanged = hiddenChanged;
        cascade = content => builder =>
        {
            builder.OpenComponent<CascadingValue<MenuFilterGroupContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<MenuFilterGroupContext>.Value), this);
            builder.AddComponentParameter(2, nameof(CascadingValue<MenuFilterGroupContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<MenuFilterGroupContext>.ChildContent), content);
            builder.CloseComponent();
        };
        inParent = parent?.Register(() => !Hidden);
    }

    /// <summary>Gets a value indicating whether the query hides the group: it has members and none is visible.</summary>
    public bool Hidden { get; private set; }

    /// <inheritdoc />
    public void Dispose() => inParent?.Dispose();

    /// <summary>Registers a member: an item, or a nested group.</summary>
    /// <param name="visible">Reads whether the member is visible.</param>
    /// <returns>The member; <see cref="Member.Changed"/> reports a change of its visibility, and disposing it unregisters it.</returns>
    public Member Register(Func<bool> visible)
    {
        Member member = new(this, visible);
        members.Add(member);
        Recompute();
        return member;
    }

    /// <summary>Wraps the group's content in the cascade of this context.</summary>
    /// <param name="content">The group's content.</param>
    /// <returns>The wrapped content.</returns>
    public RenderFragment Cascade(RenderFragment? content) => cascade(content);

    private void Recompute()
    {
        bool hidden = members.Count > 0;
        for (int i = 0; i < members.Count && hidden; i++)
        {
            hidden = !members[i].Visible();
        }

        if (hidden != Hidden)
        {
            Hidden = hidden;
            hiddenChanged();
            inParent?.Changed();
        }
    }

    /// <summary>Represents one member of a group.</summary>
    internal sealed class Member(MenuFilterGroupContext owner, Func<bool> visible) : IDisposable
    {
        private bool disposed;

        /// <summary>Gets the reader of the member's visibility.</summary>
        public Func<bool> Visible { get; } = visible;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                owner.members.Remove(this);
                owner.Recompute();
            }
        }

        /// <summary>Reports that the member's visibility changed.</summary>
        public void Changed()
        {
            if (!disposed)
            {
                owner.Recompute();
            }
        }
    }
}
