# src/Blazix.Primitives/Interop

The twins: the C# half of each JavaScript module (ADR 04 §3). Internal to the library (ADR 08 §1); parts talk to twins and never to `IJSRuntime`.

## Inputs

- `docs/decisions/04-js-interop-boundary.md` §3 (contract, decision points), ADR 01 (Server floor), ADR 06 §1 (twin contract tests), ADR 08 §3 (`<Module>Twin`), ADR 16 rule 4 (one `JsonSerializerContext`).
- `../js/twin.ts`, `../js/decide.ts`, `../js/event-info.ts`: the JavaScript side of the same contract.
- `../Events/EventSnapshot.cs` (Track 2): the event snapshot and its JSON names, which `../js/event-info.ts` mirrors.

## Job

- `TwinBase<TTwin, TOptions>`: lazy `import()` of the module on first use; `AttachAsync(ElementReference | string, options)` returning a `TwinHandle<TOptions>` per attached part, idempotent per element; the one `DotNetObjectReference<TTwin>` the module calls back through; `Ping` and `Decide` as `[JSInvokable]` methods; `DisposeAsync` that releases every handle, the reference and the module. A `JSDisconnectedException` (the Server circuit is gone) turns every call into a no-op rather than an exception in a part's lifecycle method.
- `TwinHandle<TOptions>`: `UpdateAsync(options)` and an idempotent `DisposeAsync()`; `update` after `dispose` is a no-op.
- `TwinOptions`: the base of every options record; `Style` is the consumer's merged style string, sent on `attach` and on every `update` (handover at attach).
- `DecisionPolicy` and its kebab-case literals (`run`, `stop`, `start`, `prevent-and-stop`).
- `BlazixJsonContext`: `EventSnapshot`, every options record and every callback payload. Options and payloads cross the boundary as JSON strings the twin serialises and deserialises itself, so the runtime's own serializer only ever sees strings, `ElementReference` and object references (trim- and AOT-safe without configuring the runtime's resolver).

## Traceability (the sources carry no specification references)

- `TwinBase`, `TwinHandle`: ADR 04 §3 (contract, decision points), ADR 06 Amendments 2026-10-18 rule 2 (idempotent attach and dispose), ADR 08 §3 (`<Module>Twin`), ADR 01 decision 1 (the synchronous invoke serves decisions only), ADR 02 decision 1 (attach by generated id under a substitute callback).
- `TwinOptions`: ADR 04 §3 and Amendments 2026-10-18 (handover at attach).
- `DecisionPolicy`: ADR 04 §3 and ADR 01's relaxation table (the fallback per decision point).
- `BlazixJsonContext`: ADR 16 rule 4.

## Outputs

- One `<Module>Twin` per JavaScript module, exposing the module's callbacks as typed events for the parts.

## Human check

- A twin's `[JSInvokable]` callback does one state change per message and is never called per frame.
- A new options record or payload is listed in `BlazixJsonContext` and has an L2 twin contract test.
- `OnDecide` runs synchronous consumer handlers only; nothing awaits inside a decision (ADR 04 §3).
