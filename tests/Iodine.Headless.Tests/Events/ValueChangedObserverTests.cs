namespace Blazix.Primitives.Tests.Events;

/// <summary><c>internals/useValueChanged.test.tsx</c> (EVD-ALG-09, UTL-STA-36). Strict Mode has no port equivalent; both variants run the same steps.</summary>
public sealed class ValueChangedObserverTests
{
    [Theory(DisplayName = "retains -0 as the previous value without treating it as a change from 0 (strict: %s)")]
    [Trait("Requirement", "EVD-ALG-09")]
    [Trait("Requirement", "UTL-STA-36")]
    [InlineData(false)]
    [InlineData(true)]
    public void Retains_negative_zero_as_the_previous_value_without_treating_it_as_a_change_from_zero(bool strict)
    {
        _ = strict;
        List<double> calls = [];
        ValueChangedObserver<double> observer = new(0.0, calls.Add);

        observer.Observe(-0.0);
        Assert.Empty(calls);

        observer.Observe(1);
        Assert.Single(calls);
        Assert.True(double.IsNegative(calls[0]) && calls[0] == 0);
    }

    [Fact(DisplayName = "NaN to NaN is not a change")]
    [Trait("Requirement", "UTL-STA-36")]
    public void NaN_to_NaN_is_not_a_change()
    {
        List<double> calls = [];
        ValueChangedObserver<double> observer = new(double.NaN, calls.Add);

        observer.Observe(double.NaN);
        observer.Observe(double.NaN);

        Assert.Empty(calls);
    }

    [Fact(DisplayName = "reports the previous value on every change")]
    [Trait("Requirement", "EVD-ALG-09")]
    public void Reports_the_previous_value_on_every_change()
    {
        List<string> calls = [];
        ValueChangedObserver<string> observer = new("a", calls.Add);

        observer.Observe("a");
        observer.Observe("b");
        observer.Observe("b");
        observer.Observe("c");

        Assert.Equal(["a", "b"], calls);
    }
}
