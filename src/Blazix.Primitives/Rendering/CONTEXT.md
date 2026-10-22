# src/Blazix.Primitives/Rendering

The rendering pipeline every part renders through (ADR 02 as amended by ADRs 04, 05, 08 and 16): the attribute merge, the frame rewrite, the part base class, the public `useRender` component, the generated-id source and the state-projection contract the generator implements.

## Inputs

- `docs/architecture/rendering-and-composition.md` (RND): §2.1 precedence, §3.2 the `useRender` parameters, §4.9 the conformance obligations, §5 the algorithms.
- Decision records 02 (pipeline, substitution, merge, veto, ids), 04 Amendments 2026-10-18 (no event directives on parts), 05 §4 (no boxing; `AttributeValue`), 08 (names, member order), 16 (rules 2, 3, 5, 7, 8, 10).
- Blazor's `RenderTreeBuilder` and `RenderTreeFrame` (the public `GetFrames()`), `EventCallback`, `IHandleEvent`; the `[PartState]` generator (`../../Blazix.Primitives.Generators`) for `IPartState<TSelf>`.

## Job

| File | Role |
|---|---|
| `AttributeValue.cs`, `AttributeList.cs` | the typed entry list: a hand-written non-boxing `[Union]` struct `(string, bool, EventCallback, MulticastDelegate)` with `HasValue`/`TryGetValue`, and the ordered name-keyed list. `FromObject` turns a bag value into a case value; a boxed `EventCallback<T>` is unpacked through a throwaway element frame, the public path Blazor itself uses for `@attributes` |
| `MergeProps.cs`, `StyleDeclarations.cs`, `HandlerChain.cs` | the public merge (RND-ALG-02..06): later input's plain attributes win, class placed first, style keys win, handlers run first with the veto; the style parser of RND-DOM-16; the chain runner shared with the pipeline |
| `RenderPipeline.cs` | the one static function (RND-ALG-01/07/08): state projection, part then consumer attributes, class and style functions, then the default element, the substitute callback or the frame rewrite of the substitute element (element root, component root, or a static markup root read by `MarkupRootParser.cs`) |
| `PartRenderState.cs` | per-instance scratch: the lists, the scratch builder, one `HandlerSlot` per event name (substitute, consumer, part), the class and style memo (rule 5), the one capture delegate |
| `PartBase.cs`, `PartBase.template.md` | the base class of every part and the contributor template in the eleven-group member order (ADR 08 §5) |
| `RenderElement.cs`, `RenderProps.cs` | the public `useRender` (RND §3.2) over the same function; what a substitute callback receives |
| `IPartState.cs`, `StateAttributesMapping.cs`, `StateAttributeWriter.cs`, `StateDataAttribute.cs`, `PartStateAttribute.cs`, `MirroredFieldAttribute.cs`, `EmptyState.cs` | the projection contract the generator implements per `[PartState]` record, the custom-mapping hook (RND-DOM-06..09), the JS-owned exclusion (ADR 04 §2) |
| `PartIdGenerator.cs` | the scoped `base-ui-<n>` source registered by `AddBlazixPrimitives()` (ADR 02 decision 5) |

Rules the folder keeps: handlers are `EventCallback` fields created once through `CreateHandler` (rule 2); every pipeline frame uses a literal sequence number and re-emitted substitute frames keep theirs inside a region (rule 3); class and style merges are memoised on input identity (rule 5); a part re-renders only on its own `StateHasChanged` (rule 8); the library never introduces a component instance beyond the one per part (rule 10). Three errors throw in every build, because they are programming errors a release must not hide: both `Render` and `RenderFunction` supplied, a `Render` root that is neither an element nor a component (RND-EDGE-03), and a Blazor event directive on a part or substitute (ADR 04).

Known limits, stated once: a consumer handler is invoked the way Blazor invokes any typed handler (through the receiver and `DynamicInvoke` when the delegate is typed); an event the part does not handle that has both a substitute and a consumer handler is chained through a callback typed `EventArgs`, so a custom (non-standard) event in that one case receives bare `EventArgs`; the part-supplied attributes always precede the consumer's (the five Base UI parts that put an internal entry after the consumer's decide their own `ChildContent` instead). A part can decline a consumer handler by event name (`ForwardsConsumerHandler`, ADR 02 Amendments 2026-10-21): the pipeline then leaves it off the element, which is how a button-like part gates the consumer's handlers while disabled. A part has the last word on its merged attributes (`FinishAttributes`, ADR 02 Amendments 2026-10-21, M1 track 6): the pipeline calls it once per render after the merge and before the element, the substitute or the callback receives them, which is how a part that hands its style over to a module renders it once and then freezes it. A part can render siblings after its element (`BuildAfterElement`, ADR 02 Amendments 2026-10-22, M2 track 4): the pipeline calls it once per render after the element, the substitute or the callback, inside a region at its own literal, and not while `Enabled` is false; it is how Switch, Checkbox and Radio render their hidden inputs as the root's next siblings.

## Outputs

- `Blazix.Primitives.Rendering` (public): `PartBase<TState>`, `RenderElement<TState>`, `RenderProps<TState>`, `MergeProps`, `AttributeList`, `AttributeValue`, `StateAttributesMapping<TState>`, `PartIdGenerator`, the attributes; `Blazix.Primitives`: `PreventPartHandler()` / `PartHandlerPrevented()` (in `../Events/`).
- Tested at L1 (`tests/Blazix.Primitives.Tests/Rendering`) and L2 (`tests/Blazix.Primitives.Tests.Components/Rendering`, the conformance suite on `SampleRoot`).

## Human check

- A part's `FinishAttributes` changes only an attribute its document names as part-owned or gives a documented fallback (a style handed over to a module, COL-API-10's empty `id`); every other attribute keeps the consumer-wins rule of ADR 02 decision 2 (ADR 02 Amendments 2026-10-21). The analyzers cannot see it, and `FinishAttributesTests` shows that such a change would win over the consumer.

- A part's `BuildAfterElement` renders only the siblings its document places after the element (the hidden inputs of SWT-STR-01, CHK-STR-01, RAD-STR-01), numbers them with literals from 0, and renders nothing that belongs inside the element; `AfterElementTests` and `RenderPipelineSequenceTests` pin the region and its literal.

- A new part derives from `PartBase<TState>`, creates its handlers once, adds them from `AddPartAttributes`, and names no parameter `Class` or `Style`.
- Nothing here reflects; `BL0006` is suppressed only where the frame rewrite reads frames, and `BLZX2002` only where a sequence number is not ours to choose (`Reemit`, `AttributeValue.EmitTo`), each with the ADR citation.
- The allocation budgets in both test projects only tighten.
- A sequence number the pipeline emits is one of the named literals in `RenderPipeline`, never a counter or a calculation (a repeated capture repeats its literal); each variable section sits in its own region, and a re-emitted consumer frame keeps its own number inside it (ADR 16 rule 3). `BLZX2002` (`../../Blazix.Primitives.Analyzers`) fails the build on a sequence number that is not a constant expression, and `RenderPipelineSequenceTests` asserts what is emitted; Microsoft's `ASP0006` does not run on this project (it ships with `Microsoft.AspNetCore.App`) and would reject named constants.
