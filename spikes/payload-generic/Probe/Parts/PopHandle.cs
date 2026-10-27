namespace Probe.Parts;

/// <summary>A handle created with <c>new</c>, the only carrier of the type argument on a detached trigger.</summary>
public class PopHandle<TPayload>
{
    public string Describe() => $"PopHandle<{typeof(TPayload).Name}>";
}
