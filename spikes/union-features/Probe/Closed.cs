namespace Probe;

public closed class Details
{
    public string Reason { get; init; } = "";
}

public sealed class OpenDetails : Details { }
public sealed class TabsDetails : Details { }

public static class ClosedUse
{
    public static string Describe(Details d) => d switch
    {
        OpenDetails => "open",
        TabsDetails => "tabs",
    };
}
