using System.Collections;

namespace Blazix.Primitives;

// TST-STA-01, TST-STA-02 (createToastMetadata, store.ts:46-68): the list newest first, with each toast's DOM index,
// visible index (-1 while ending) and vertical offset (the heights before it) derived once per list write. A record
// member compares by reference (Store/CONTEXT.md), so every list write is a new instance.
internal sealed class ToastList<TData> : IReadOnlyList<ToastObject<TData>>
{
    private readonly ToastObject<TData>[] items;
    private readonly Dictionary<string, ToastEntry> entries;

    public ToastList(ToastObject<TData>[] items)
    {
        this.items = items;
        entries = new Dictionary<string, ToastEntry>(items.Length, StringComparer.Ordinal);
        int visibleIndex = 0;
        double offsetY = 0;
        for (int i = 0; i < items.Length; i++)
        {
            ToastObject<TData> toast = items[i];
            bool ending = toast.TransitionStatus == TransitionStatus.Ending;
            entries[toast.Id] = new ToastEntry(i, ending ? -1 : visibleIndex, offsetY);
            offsetY += toast.Height ?? 0;
            if (!ending)
            {
                visibleIndex++;
            }
        }
    }

    public static ToastList<TData> Empty { get; } = new([]);

    /// <inheritdoc />
    public int Count => items.Length;

    /// <inheritdoc />
    public ToastObject<TData> this[int index] => items[index];

    // The backing array, for the store's own list writes; never handed to a consumer.
    internal ToastObject<TData>[] Items => items;

    public ToastObject<TData>? Find(string id) => entries.TryGetValue(id, out ToastEntry entry) ? items[entry.DomIndex] : null;

    public int DomIndex(string id) => entries.TryGetValue(id, out ToastEntry entry) ? entry.DomIndex : -1;

    public int VisibleIndex(string id) => entries.TryGetValue(id, out ToastEntry entry) ? entry.VisibleIndex : -1;

    public double OffsetY(string id) => entries.TryGetValue(id, out ToastEntry entry) ? entry.OffsetY : 0;

    /// <inheritdoc />
    public IEnumerator<ToastObject<TData>> GetEnumerator() => ((IEnumerable<ToastObject<TData>>)items).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private readonly record struct ToastEntry(int DomIndex, int VisibleIndex, double OffsetY);
}
