namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The <c>composite-list-virtual</c> fixture (tests/browser/composite-list.spec.ts; M5 track 1, issue 159): the
/// <c>composite-list-dom</c> page with an input reference (<c>role="combobox"</c>) under virtual focus, the Combobox
/// configuration's host. The query is the same.
/// </summary>
public sealed class CompositeListVirtualFixture : CompositeListDomFixture
{
    /// <inheritdoc />
    protected override string FixtureName => "composite-list-virtual";

    /// <inheritdoc />
    protected override bool Virtual => true;
}
