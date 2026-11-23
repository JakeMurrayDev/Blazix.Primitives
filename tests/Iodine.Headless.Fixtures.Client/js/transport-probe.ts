// The transport probe of issue 26 (ADR 16 §B, Amendments 2026-10-27; M4 track 3): test-only, WebAssembly only. It times
// the twin base's three messages, the `IJSObjectReference` `update`, the `invokeMethodAsync` report and the synchronous
// `invokeMethod` `Decide`, against `[JSImport]` and `[JSExport]` with the same JSON, so the per-message cost of each
// transport is measured on the interpreter. Each sample is a batch, so the timer's resolution does not decide the figure.
import type { DotNetTwin, TwinHandle } from '../../../src/Blazix.Primitives/js/twin';

let parsed = 0;

/** The `[JSImport]` target of the `update` message: the same work as a handle's `update`. */
export function update(optionsJson: string): void {
  parsed += (JSON.parse(optionsJson) as object) === null ? 0 : 1;
}

/** The twin base's form: a handle whose `update` does the same work. */
export function attach(_elementOrId: unknown, _optionsJson: string, _twin: DotNetTwin): TwinHandle {
  return {
    update(optionsJson: string) {
      update(optionsJson);
    },
    dispose() {},
  };
}

interface RuntimeExports {
  [name: string]: unknown;
}

async function assemblyExports(): Promise<RuntimeExports> {
  const host = globalThis as unknown as {
    Blazor?: { runtime?: { getAssemblyExports(name: string): Promise<RuntimeExports> } };
    getDotnetRuntime?: (id: number) => Promise<{ getAssemblyExports(name: string): Promise<RuntimeExports> }>;
  };
  const runtime = host.Blazor?.runtime ?? (await host.getDotnetRuntime?.(0));
  if (!runtime) {
    throw new Error('Blazix: the .NET runtime API is not reachable');
  }
  return runtime.getAssemblyExports('Blazix.Primitives.Fixtures.Client.dll');
}

function probeExports(root: RuntimeExports): { Report(json: string): void; Decide(point: string, json: string): string } {
  let node: unknown = root;
  for (const key of ['Blazix', 'Primitives', 'Fixtures', 'Client', 'Interactions', 'TransportProbeExports']) {
    node = (node as RuntimeExports)[key];
  }
  return node as { Report(json: string): void; Decide(point: string, json: string): string };
}

/** Per-message milliseconds of `batches` batches of `size` messages, after `warmup` messages. */
async function sample(batches: number, size: number, warmup: number, send: () => unknown): Promise<number[]> {
  for (let i = 0; i < warmup; i += 1) {
    await send();
  }
  const samples: number[] = [];
  for (let b = 0; b < batches; b += 1) {
    const start = performance.now();
    for (let i = 0; i < size; i += 1) {
      await send();
    }
    samples.push((performance.now() - start) / size);
  }
  return samples;
}

/**
 * Times the report and `Decide` messages both ways.
 * @returns The JSON of `{ reportTwin, reportTwinDispatch, reportExport, decideTwin, decideExport }`, each an array of
 * per-message milliseconds.
 */
export async function measure(twin: DotNetTwin, reportJson: string, eventJson: string, batches: number, size: number, warmup: number): Promise<string> {
  const exportsRoot = probeExports(await assemblyExports());
  const reportTwin = await sample(batches, size, warmup, () => twin.invokeMethodAsync('Report', reportJson));
  // The share of a report the module's listener pays: `report` never awaits the promise.
  const reportTwinDispatch = await sample(batches, size, warmup, () => {
    void twin.invokeMethodAsync('Report', reportJson);
  });
  const reportExport = await sample(batches, size, warmup, () => exportsRoot.Report(reportJson));
  const decideTwin = await sample(batches, size, warmup, () => twin.invokeMethod('Decide', 'escape', eventJson));
  const decideExport = await sample(batches, size, warmup, () => exportsRoot.Decide('escape', eventJson));
  return JSON.stringify({ reportTwin, reportTwinDispatch, reportExport, decideTwin, decideExport, parsed });
}
