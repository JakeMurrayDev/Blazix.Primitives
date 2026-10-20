# ADR 02 — Render pipeline, element substitution and attribute merging

| | |
|---|---|
| Status | accepted (2026-10-17) |
| Project | Blazix.Primitives |
| Decides | how `render`, `className`/`style` functions, handler chaining with `preventBaseUIHandler()`, state data attributes, DOM handles and generated ids map onto Blazor; whether one shared implementation satisfies the conformance obligations RND-DOM-36..49 |
| Affects | RND-STR-01..09, RND-API-01..11, RND-STA-01..03, RND-DOM-01..33, RND-DOM-36..49, RND-EVT-01..13, RND-ALG-01..15, RND-EDGE-01..09; UBT handler-merging notes (UBT-EVT-01..03); EVD-API-11, EVD-EVT-26/27 |

## Context

Every Base UI part turns public parameters, a state object and part-supplied attributes into exactly one host element through one internal function (`useRenderElement`, RND-ALG-01), which is also public as `useRender`. The function supports two substitution forms (a *substitute element* that is cloned with merged attributes, and a *substitute callback* that receives the merged attribute set), class and style parameters that may be functions of state, a fixed merge precedence (RND §2.1), handler chaining with a per-dispatch veto, state-to-`data-*` projection, DOM-handle fan-out and server-stable ids. The conformance suite (RND-DOM-36..49) runs against every part.

Blazor facts that shaped the decision, verified where marked:

* Attribute splatting never merges; when `@attributes` and an explicit attribute collide, the attribute written last in the markup wins (verified, Microsoft docs). Class and style are therefore the part's job to merge before emission.
* A `RenderFragment` is an opaque delegate; there is no `cloneElement`. `RenderTreeBuilder` is public, has a public constructor and `GetFrames()` (verified, API docs), so a fragment can be rendered into a scratch builder and its frames read and re-emitted through the same public API.
* A consumer's `@onclick="H"` on a part lands in the `CaptureUnmatchedValues` bag as a boxed `EventCallback<MouseEventArgs>`; Razor emits event attribute names in lowercase. Each element's handler receives its own deserialized `EventArgs` instance, so a flag on the instance is invisible to a handler on an ancestor element. `EventArgs` carry no `target` and no `preventDefault()`.
* Blazor has no `useId` and no way for a component to learn its tree position. Prerender and interactive activation are two renders in two DI scopes.
* The old port (Blazix.BaseUI) used a child component `<RenderElement>` inside every part with a callback-only `Render`; its slider notes record a race between the parent's first render and the child's element reference, and its handler rule ran the consumer's callback after the internal handler, inverting RND-EVT-02.

## Decision

1. **Both substitution forms are offered (option B).** `Render` accepts a plain `RenderFragment` whose root is cloned (the *substitute element*), and `RenderFunction` accepts a `RenderFragment<RenderProps<TState>>` that receives the merged attribute bag, the state object and the child content (the *substitute callback*). Blazor cannot type one parameter as either, so the two parameters together are Base UI's `render`; supplying both is a development-time error.
   The element form is implemented by **frame rewriting**: the fragment is rendered into a scratch `RenderTreeBuilder`; its root element frame, attribute frames (including `EventCallback` values, `@ref` capture frames and `@key`) and child frames are read; the pipeline re-emits the root element with attributes merged per RND-ALG-02 (substitute attributes win, substitute class first, substitute style keys win, substitute handlers run first), preserves the consumer's capture frame and key, adds the part's own element reference capture, and re-emits the children frame by frame (text, markup, region, element, component with parameters, captures, named events, render modes). A root that is a component frame receives the merged attributes as its parameters, which is how a Blazix part or a consumer component accepting unmatched values nests (RND-STR-07, RND-DOM-43); such a component must forward the merged `id` so the part's JavaScript can locate the host element.
2. **Class and style have two channels each (option P1).** The plain `class`/`style` strings arrive through the unmatched bag; `ClassValue`/`StyleValue` are `Func<TState, string?>` parameters. On the consumer's side the string comes first, then the function result. Order overall is RND-DOM-14/15: substitute class, consumer string, consumer function, part classes. Style is parsed into declarations (split on `;` outside parentheses and quotes) and merged by property name with first-seen order kept, so RND-DOM-16 holds as a literal string.
3. **Handler chaining and veto (option V1).** The part registers the single real handler for each event on the host element and invokes the chain itself: substitute handler, consumer handler, part handler. The veto is `e.PreventBaseUIHandler()` / `e.BaseUIHandlerPrevented()`, extension methods on `EventArgs` backed by a `ConditionalWeakTable`; the per-dispatch scope of RND-EVT-04 follows from Blazor creating a fresh instance per dispatch. A consumer handler in the unmatched bag is any key starting with `on` whose value is an `EventCallback`, `EventCallback<T>` or delegate; name casing is irrelevant (deviation from RND-ALG-13/RND-EVT-13, where lowercase `onclick` was an ordinary attribute, because Razor lowercases event names). The cross-part case of EVD-EVT-27 (a part converts Enter/Space to a click and an enclosing part must not handle the same key) uses a *consumed flag* on the ancestor-provided context the enclosing part already reads, set by the inner handler and read-and-cleared by the outer one, which Blazor dispatches next. `preventDefault` and `stopPropagation` are not on the C# surface; they are JavaScript policy (ADR 01, ADR 04).
4. **Pipeline shape (option S2).** One static function implements RND-ALG-01/02/05/06/07/08 and the frame rewrite against a `RenderTreeBuilder`. Library parts derive from `PartBase<TState>`, which owns the state object, the unmatched bag, `ClassValue`/`StyleValue`, `Render`/`RenderFunction`, `Enabled` and the element reference, and calls the static function from `BuildRenderTree`; parts are C# classes, not `.razor` files. The public `useRender` equivalent is a `RenderElement<TState>` component over the same static function with the parameters of RND §3.2 (`Render`, `State`, `StateAttributesMapping`, `Props`, `Enabled`, `DefaultTagName`); `MergeProps` is public over RND-ALG-02. One component instance per part; `Enabled = false` renders nothing (RND-DOM-25).
   Default projection of the state object to `data-*` (RND-ALG-07) is cached reflection over the record's public properties, trimming-safe via `[DynamicallyAccessedMembers(PublicProperties)]`, restricted to own properties of primitive type (RND §10 Q4). Custom mappings (`transitionStatusMapping`, `collapsibleOpenStateMapping`, the forty component-specific ones) are hand-written.
5. **Generated ids (option I1).** `AddBlazixPrimitives()` registers a scoped id generator; every part that may need an id draws `base-ui-<n>` at first render in render order, whether or not the consumer supplied one, so RND-DOM-28/30/31 hold exactly. RND-DOM-29 holds whenever the prerender and interactive passes render the same tree in the same order; divergence (streaming rendering, data that differs between passes, roots activating out of document order) produces a different id at activation, which in Blazor is an attribute change, not a failure. The unprefixed exceptions of RND-DOM-27 are collapsed: all generated ids carry the `base-ui-` prefix (no Base UI test pins the bare form, and bare numeric ids are poor CSS targets).

One shared implementation therefore satisfies RND-DOM-36..49 for every part; the per-part conformance run exists to catch a part that bypasses or misconfigures the pipeline.

## Requirements relaxed or withdrawn for the port

| ID | Disposition |
|---|---|
| RND-DOM-19, RND-DOM-32 ("an explicitly `undefined` value on a later source also replaces the earlier value") | not expressible: the Razor compiler omits a frame for a `null` attribute value, so only present attributes participate in the merge |
| RND-DOM-20..24 (DOM handles) | the part exposes `Element` (`ElementReference?`); set after the first render of the default or substitute element, `null` while disabled, and `null` under a substitute callback (the part's JavaScript locates the host by id instead). Callback-handle cleanup semantics (RND-DOM-24) have no Blazor analogue |
| RND-DOM-34, RND-EDGE-04, RND-EDGE-05 (deferred elements) | vacuous: Blazor has no lazy element wrapper |
| RND-DOM-41 (handle receives the documented element class) | `ElementReference` is opaque; asserted in browser tests through `tagName` |
| RND-EVT-07 (non-DOM-argument handler chains) | change callbacks are typed `EventCallback` parameters, not bag entries; the part invokes them directly |
| RND-EVT-09 (chain returns the first handler's result) | handlers return `Task`; no return value |
| RND-ALG-13, RND-EVT-13 (handler identified by `on` + uppercase letter) | handler identified by `on` prefix plus delegate/`EventCallback` value, any casing |
| RND-DOM-27 (unprefixed id exceptions) | collapsed to the prefixed form |
| RND-EDGE-01/02/03 (dev warnings and the invalid-`render` error) | kept, with the message text adapted to Blazor terms; a `Render` fragment whose root is not an element or component frame throws RND-EDGE-03 |

## Consequences

* Parts are written in C#; `.razor` is for demos, docs and consumer code. A contributor template for `PartBase<TState>` is part of the implementation plan.
* The frame re-emitter is the one novel piece: it must handle every `RenderTreeFrameType` and is exercised by every part's conformance run on day one. Each render of a substituted part costs one scratch render of the consumer's fragment.
* Consumers get one small superset of Base UI (string plus function class/style at once) and one setup line (`AddBlazixPrimitives()`).
* Decision 4 owns what JavaScript receives about an event (`target`, `pointerType`, a native-event id) and the `preventDefault`/`stopPropagation` policy; this ADR fixes only the C# merge contract.

## Verification items

* Confirm that interactive activation replaces the prerendered subtree rather than reconciling attributes, and that multiple interactive roots activate in document order (affects how often RND-DOM-29's divergence case occurs).
* Confirm the frame re-emitter preserves `@key` and `@ref` semantics across re-renders for a substitute whose root is a consumer component (RND-DOM-43, RND-DOM-45).
* Measure the scratch-render cost on a list part (Menu items) before deciding whether the element form caches frames between renders.

## Considered options

* Substitution: callback only (old port), typed substitute object. Rejected: the first withdraws eleven of fourteen conformance IDs and the ergonomic core of composition; the second removes Razor markup from the substitute.
* Class/style: single `object` parameter mirroring the union (consumers would write `Class=`), or string and function mutually exclusive. Rejected for ergonomics.
* Veto: subclassed `EventArgs` copied per dispatch (no gain over the weak table); library-registered custom event types with a native-event id (deferred to ADR 04 as the event contract, not needed for the veto).
* Pipeline: child component per part (old port; double instance, ref race), static function with no base class (repetition).
* Ids: random per instance (guaranteed flip at activation), consumer-supplied required ids (breaks the no-props contract).

## Amendments

**2026-10-17, by ADR 05 (no-boxing constraint; .NET 11 / C# 15).**

* Decision 4's default state projection changes from cached reflection to a **source generator** emitting typed projection code per `[PartState]` record (reflection boxes every value on every render). Property order is declaration order by construction; RND-DOM-09 is then deterministic.
* Decision 1's two parameters (`Render`, `RenderFunction`) and Decision 2's `ClassValue`/`StyleValue` functions become **single union-typed parameters** where C# 15 unions are available: `Render : RenderFragment | RenderFragment<RenderProps<TState>>`, `ClassValue : string | Func<TState, string?>`, `StyleValue` likewise. The plain `class`/`style` strings still arrive through the unmatched bag and the P1 concatenation rule stands. The two-parameter shape remains the fallback if the Razor compiler does not bind attributes to union-typed parameters (ADR 05 verification item).
* The merged attribute bag is a typed entry list with a non-boxing `AttributeValue` union, emitted through `RenderTreeBuilder`'s typed `AddAttribute` overloads; `MergeProps` keeps a `Dictionary<string, object>` overload only for the public `useRender` surface.

**2026-10-18, by ADR 04 (dispatch-time decisions).** Decision 3's statement that `preventDefault` and `stopPropagation` are JavaScript policy is made concrete: Blazor's `@on…:preventDefault` / `@on…:stopPropagation` directives are rejected on parts and on substitute elements with a development-time error (new RND-EDGE diagnostic), and the consumer veto channels are `PreventBaseUIHandler()` from C# and `preventDefault()` from a consumer JavaScript listener. See ADR 04 Amendments 2026-10-18.

**2026-10-18, `inputRef` (combobox §16 item 5).** Following the RND-DOM-20..24 relaxation, `Combobox.Root.inputRef` (a handle to the hidden form input) has no port counterpart: `Combobox.Input.Element` is the visible input and the hidden input stays internal; the same applies to every other `inputRef`-style parameter (Checkbox, Radio, Switch, Slider thumbs).

**2026-10-18, by ADR 06 (React-assuming statements).** Withdrawn for the port, IDs kept: NUM-EDGE-07, POP-EDGE-08, CMB-STR-10, TAB-EVT-06; DLG-DOM-26 is rewritten without the hook name in the implementation revision.

**2026-10-18, verification.** Razor binds component attributes to parameters case-insensitively (`class="x"` → a parameter named `Class`; confirmed on SDK 10.0.301, see ADR 05 verification items). Decision 2's two channels therefore require that no part declares a parameter named `Class` or `Style` until the union-typed single parameter of ADR 05 §4 replaces them. Blazor event directives on a component tag compile to attribute frames (see ADR 04 Amendments 2026-10-18).

**2026-10-20, by ADR 08 (product-name rule).** The veto of Decision 3 is named `e.PreventPartHandler()` with the query `e.PartHandlerPrevented()` (previously `PreventBaseUIHandler()` / `BaseUIHandlerPrevented()`); semantics unchanged. The union-typed class and style parameters of the 2026-10-17 amendment are named `ClassValue` and `StyleValue` and the plain `class`/`style` strings keep arriving through the unmatched bag, so no part declares a parameter named `Class` or `Style` (ADR 08 §2). The generated id prefix stays `base-ui-`.
