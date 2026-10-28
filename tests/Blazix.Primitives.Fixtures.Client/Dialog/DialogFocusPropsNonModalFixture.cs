namespace Blazix.Primitives.Fixtures.Client.DialogFixtures;

/// <summary>The <c>dialog-focus-props-nonmodal</c> fixture: <c>dialog-focus-props</c> with <c>Modal="false"</c> (DialogPopup.test.tsx's second run).</summary>
public sealed class DialogFocusPropsNonModalFixture : DialogFocusPropsFixture
{
    protected override string Name => "dialog-focus-props-nonmodal";

    protected override ModalValue? Modal => false;
}
