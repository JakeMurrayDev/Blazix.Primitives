using System.Globalization;

namespace Iodine.Headless;

// UTL-EDGE-10 (generateId, packages/utils/src/generateId.ts:1-5): `${prefix}-${r}-${n}`, `r` four base-36 characters of
// Math.random().toString(36).slice(2, 6), `n` a process-wide counter incremented before use. Toast ids are its only
// consumer (TST-API-06), so it lives beside them. Never server-rendered: a toast exists only after an add.
internal static class ToastIds
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private static int s_counter;

    public static string Next()
    {
        int counter = Interlocked.Increment(ref s_counter);
        Span<char> chars = stackalloc char[4];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return string.Create(CultureInfo.InvariantCulture, $"toast-{chars}-{counter}");
    }
}
