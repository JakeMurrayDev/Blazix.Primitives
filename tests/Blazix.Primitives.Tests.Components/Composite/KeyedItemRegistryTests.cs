using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the keyed registry (CMP §4.2.2, CMP-STA-19..21, CMP-ALG-30; M5 track 12's design point 3 (a)), Base UI's
/// <c>internals/useItemRegistry.test.tsx</c> titles on a host whose children register as they initialize and unregister as
/// they leave. The host publishes at its render, which the registry requests once per batch.
/// </summary>
public sealed class KeyedItemRegistryTests : IDisposable
{
    private readonly BunitContext context = new();

    [Fact(DisplayName = "publishes one immutable snapshot for all registrations in a commit")]
    [Trait("Requirement", "CMP-STA-19")]
    [Trait("Requirement", "CMP-ALG-30")]
    public void Publishes_one_immutable_snapshot_for_all_registrations_in_a_commit()
    {
        List<IReadOnlyDictionary<string, string>> snapshots = [];
        IRenderedComponent<RegistryHost> host = context.Render<RegistryHost>(parameters => parameters
            .Add(p => p.Items, ["a", "b", "c"])
            .Add(p => p.Snapshots, snapshots));

        Assert.Equal([[], ["a", "b", "c"]], snapshots.Select(snapshot => snapshot.Keys.ToArray()));

        host.Render(parameters => parameters.Add(p => p.Items, ["b", "d"]));

        Assert.Equal(["b", "d"], snapshots[^1].Keys);
        Assert.Equal(["a", "b", "c"], snapshots[1].Keys);
    }

    [Fact(DisplayName = "publishes every registration under StrictMode double effects")]
    [Trait("Requirement", "CMP-STA-20")]
    public void Publishes_every_registration_under_double_effects()
    {
        List<IReadOnlyDictionary<string, string>> snapshots = [];
        IRenderedComponent<RegistryHost> host = context.Render<RegistryHost>(parameters => parameters
            .Add(p => p.Items, ["a", "b", "c"])
            .Add(p => p.Snapshots, snapshots)
            .Add(p => p.DoubleEffects, true));

        Assert.Equal(["a", "b", "c"], snapshots[^1].Keys);
        Assert.Equal("value-b", snapshots[^1]["b"]);

        host.Render(parameters => parameters.Add(p => p.Items, ["b", "d"]));

        Assert.Equal(["b", "d"], snapshots[^1].Keys);
        Assert.Equal("value-d", snapshots[^1]["d"]);
    }

    [Fact(DisplayName = "publishes an empty snapshot once the last item unregisters")]
    [Trait("Requirement", "CMP-STA-21")]
    public void Publishes_an_empty_snapshot_once_the_last_item_unregisters()
    {
        List<IReadOnlyDictionary<string, string>> snapshots = [];
        IRenderedComponent<RegistryHost> host = context.Render<RegistryHost>(parameters => parameters
            .Add(p => p.Items, ["a", "b"])
            .Add(p => p.Snapshots, snapshots));
        Assert.Equal(["a", "b"], snapshots[^1].Keys);

        host.Render(parameters => parameters.Add(p => p.Items, Array.Empty<string>()));

        Assert.Empty(snapshots[^1]);
    }

    [Fact(DisplayName = "port: an earlier registration's disposal does not remove the key a later registration holds")]
    [Trait("Requirement", "CMP-STA-20")]
    public void An_earlier_registrations_disposal_does_not_remove_the_key_a_later_registration_holds()
    {
        int requests = 0;
        KeyedItemRegistry<string, string> registry = new(() => requests++);

        IDisposable first = registry.Register("a", "first");
        _ = registry.Register("a", "second");
        first.Dispose();
        Assert.True(registry.Publish());

        Assert.Equal("second", registry.Items["a"]);
        Assert.Equal(1, requests);
        Assert.False(registry.Publish());
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    private sealed class RegistryHost : ComponentBase
    {
        private KeyedItemRegistry<string, string>? registry;

        [Parameter]
        public IReadOnlyList<string> Items { get; set; } = [];

        [Parameter]
        public List<IReadOnlyDictionary<string, string>> Snapshots { get; set; } = [];

        [Parameter]
        public bool DoubleEffects { get; set; }

        protected override void OnInitialized() => registry = new KeyedItemRegistry<string, string>(StateHasChanged);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            registry!.Publish();
            Snapshots.Add(registry.Items);
            foreach (string item in Items)
            {
                builder.OpenComponent<RegistryItem>(0);
                builder.SetKey(item);
                builder.AddComponentParameter(1, nameof(RegistryItem.Id), item);
                builder.AddComponentParameter(2, nameof(RegistryItem.Registry), registry);
                builder.AddComponentParameter(3, nameof(RegistryItem.DoubleEffects), DoubleEffects);
                builder.CloseComponent();
            }
        }
    }

    private sealed class RegistryItem : ComponentBase, IDisposable
    {
        private IDisposable? registration;

        [Parameter]
        public string Id { get; set; } = "";

        [Parameter]
        public KeyedItemRegistry<string, string> Registry { get; set; } = null!;

        [Parameter]
        public bool DoubleEffects { get; set; }

        public void Dispose() => registration?.Dispose();

        protected override void OnInitialized()
        {
            string value = DoubleEffects ? $"value-{Id}" : Id;
            registration = Registry.Register(Id, value);
            if (DoubleEffects)
            {
                // StrictMode's register -> cleanup -> register within one commit.
                registration.Dispose();
                registration = Registry.Register(Id, value);
            }
        }
    }
}
