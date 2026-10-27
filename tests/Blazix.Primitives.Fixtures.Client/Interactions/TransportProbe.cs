using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Text.Json;
using Blazix.Primitives.Interop;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.Interactions;

// Issue 26 (ADR 16 §B and Amendments 2026-10-27; ADR 14 Amendments 2026-10-27; M4 track 3): the twin base's transport
// against [JSImport]/[JSExport], the JSON held equal and the work at each end the same (the module parses an update; C#
// deserializes a report and a Decide's snapshot through BlazixJsonContext, as InteractionsTwin does).

/// <summary>The twin base's form of the three messages, over the probe module.</summary>
internal sealed class TransportProbeTwin(IJSRuntime jsRuntime)
    : TwinBase<TransportProbeTwin, InteractionsOptions>(jsRuntime, ModulePath, BlazixJsonContext.Default.InteractionsOptions)
{
    public const string ModulePath = "./js/transport-probe.js";

    public int Reports { get; private set; }

    /// <summary>The report message, as <see cref="InteractionsTwin.OnRequest"/> receives it.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="InteractionsRequest"/>.</param>
    [JSInvokable]
    public void Report(string payloadJson)
    {
        _ = Deserialize(payloadJson, BlazixJsonContext.Default.InteractionsRequest);
        Reports++;
    }

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.PreventAndStop;
}

/// <summary>The [JSExport] form of the report and Decide messages.</summary>
[SupportedOSPlatform("browser")]
internal static partial class TransportProbeExports
{
    public static int Reports { get; private set; }

    [JSExport]
    internal static void Report(string payloadJson)
    {
        _ = JsonSerializer.Deserialize(payloadJson, BlazixJsonContext.Default.InteractionsRequest);
        Reports++;
    }

    [JSExport]
    internal static string Decide(string point, string eventJson)
    {
        _ = JsonSerializer.Deserialize(eventJson, BlazixJsonContext.Default.EventSnapshot);
        return string.Equals(point, "escape", StringComparison.Ordinal) ? DecisionPolicy.PreventAndStop.ToLiteral() : DecisionPolicy.Run.ToLiteral();
    }
}

/// <summary>The [JSImport] form of the update message.</summary>
[SupportedOSPlatform("browser")]
internal static partial class TransportProbeImports
{
    public const string ModuleName = "transport-probe";

    [JSImport("update", ModuleName)]
    internal static partial void Update(string optionsJson);
}

// A failed call surfaces to the fixture, which renders it; the interop calls inside the sampled lambdas are not guarded
// one by one (BL0016), because a guard would be part of what is timed.
#pragma warning disable BL0016

/// <summary>One run of the probe: every message kind both ways, rendered as per-message microseconds.</summary>
[SupportedOSPlatform("browser")]
internal static class TransportProbeRun
{
    // The interpreter's clock ticks in whole milliseconds under Playwright's Chromium, so a batch of 500 messages
    // resolves 2 microseconds per message.
    private const int Batches = 30;
    private const int BatchSize = 500;
    private const int Warmup = 500;

    // The messages' JSON, sized as the module sends them: a hover request report with its snapshot and an Escape
    // keydown's snapshot. The positioning mirror report of a flip is costed as a request report, an upper bound: its JSON
    // is shorter.
    private const string EventJson = """{"kind":"MouseEvent","type":"mouseenter","key":null,"code":null,"altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":0,"target":{"id":"base-ui-13","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":1234.5}""";

    private const string ReportJson = $$"""{"open":true,"reason":"trigger-hover","event":{{EventJson}},"triggerId":"base-ui-13"}""";

    private const string EscapeJson = """{"kind":"KeyboardEvent","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,"target":{"id":"base-ui-14","tagName":"DIV"},"defaultPrevented":false,"timeStamp":1234.5}""";

    // A session update, as InteractionsSessionBehavior sends it after a commit.
    private static readonly InteractionsOptions s_update = new()
    {
        SessionToken = "base-ui-12",
        Session = new InteractionsSessionOptions { Open = true, ActiveTriggerId = "base-ui-13", FloatingId = "base-ui-14", Dismiss = new InteractionsDismissOptions() },
    };

    private static readonly string s_updateJson = JsonSerializer.Serialize(s_update, BlazixJsonContext.Default.InteractionsOptions);

    /// <summary>Measures every message kind both ways.</summary>
    /// <param name="runtime">The runtime of the WebAssembly application.</param>
    /// <returns>
    /// <c>kind=median/p95</c> pairs in microseconds per message, separated by semicolons, then the hover-and-flip cycle's
    /// cost both ways.
    /// </returns>
    /// <exception cref="Microsoft.JSInterop.JSException">A call into the probe module failed.</exception>
    public static async Task<string> MeasureAsync(IJSRuntime runtime)
    {
        await using TransportProbeTwin twin = new(runtime);
        using DotNetObjectReference<TransportProbeTwin> self = DotNetObjectReference.Create(twin);
        TwinHandle<InteractionsOptions> handle = await twin.AttachTokenAsync("transport", new InteractionsOptions());
        await JSHost.ImportAsync(TransportProbeImports.ModuleName, "../js/transport-probe.js");
        await using IJSObjectReference module = await runtime.InvokeAsync<IJSObjectReference>("import", TransportProbeTwin.ModulePath);
        await using IJSObjectReference raw = await module.InvokeAsync<IJSObjectReference>("attach", null, "{}", self);

        // The twin base's update serializes the record each time (update-twin); update-twin-raw sends the same string
        // as the import, so the two transports carry identical JSON.
        double[] updateTwin = await SampleAsync(async () => await handle.UpdateAsync(s_update));
        double[] updateTwinRaw = await SampleAsync(async () => await raw.InvokeVoidAsync("update", s_updateJson));
        double[] updateImport = await SampleAsync(() =>
        {
            TransportProbeImports.Update(s_updateJson);
            return Task.CompletedTask;
        });
        string measured = await module.InvokeAsync<string>("measure", self, ReportJson, EscapeJson, Batches, BatchSize, Warmup);
        await handle.DisposeAsync();

        using JsonDocument document = JsonDocument.Parse(measured);
        double[] Read(string name) => [.. document.RootElement.GetProperty(name).EnumerateArray().Select(value => value.GetDouble())];
        List<(string Kind, double[] Samples)> kinds =
        [
            ("update-twin", updateTwin),
            ("update-twin-raw", updateTwinRaw),
            ("update-import", updateImport),
            ("report-twin", Read("reportTwin")),
            ("report-twin-dispatch", Read("reportTwinDispatch")),
            ("report-export", Read("reportExport")),
            ("decide-twin", Read("decideTwin")),
            ("decide-export", Read("decideExport")),
        ];

        Dictionary<string, (double Median, double P95)> figures = kinds.ToDictionary(kind => kind.Kind, kind => (Percentile(kind.Samples, 0.5), Percentile(kind.Samples, 0.95)), StringComparer.Ordinal);

        // A hover open and close (two request reports, two session updates) plus one positioning mirror report per flip.
        double cycleTwin = (3 * figures["report-twin"].Median) + (2 * figures["update-twin-raw"].Median);
        double cycleNative = (3 * figures["report-export"].Median) + (2 * figures["update-import"].Median);
        return string.Join(';', kinds.Select(kind => FormattableString.Invariant($"{kind.Kind}={figures[kind.Kind].Median * 1000:0.0}/{figures[kind.Kind].P95 * 1000:0.0}")))
            + FormattableString.Invariant($";cycle-twin={cycleTwin * 1000:0.0};cycle-native={cycleNative * 1000:0.0}");
    }

    // Per-message milliseconds of Batches batches of BatchSize messages, after Warmup messages.
    private static async Task<double[]> SampleAsync(Func<Task> send)
    {
        for (int i = 0; i < Warmup; i++)
        {
            await send();
        }

        double[] samples = new double[Batches];
        for (int b = 0; b < Batches; b++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < BatchSize; i++)
            {
                await send();
            }

            samples[b] = Stopwatch.GetElapsedTime(start).TotalMilliseconds / BatchSize;
        }

        return samples;
    }

    private static double Percentile(double[] values, double quantile)
    {
        double[] sorted = [.. values.Order()];
        int index = (int)Math.Ceiling(quantile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}

#pragma warning restore BL0016
