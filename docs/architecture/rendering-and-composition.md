# Rendering and composition

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/internals/useRenderElement.tsx`, `internals/getStateAttributesProps.ts`, `internals/stateAttributesMapping.ts`, `internals/TransitionStatusDataAttributes.ts`, `internals/useBaseUiId.ts`, `internals/resolveRenderedId.ts`, `internals/types.ts`, `types/index.ts`, `merge-props/mergeProps.ts`, `use-render/useRender.ts`, `utils/resolveClassName.ts`, `utils/resolveStyle.ts`, `utils/resolveRef.ts`, `utils/styles.tsx`, `utils/collapsibleOpenStateMapping.ts`, `utils/ItemIndicator.tsx`, `utils/listbox-separator/ListboxSeparator.tsx`, `packages/utils/src/useId.ts`, `packages/utils/src/useMergedRefs.ts`, `packages/utils/src/getReactElementRef.ts`, `packages/utils/src/mergeObjects.ts`, `packages/utils/src/createLogOnce.ts` |
| Tests | `internals/useRenderElement.test.tsx`, `internals/useRenderElement.spec.tsx`, `internals/getStateAttributesProps.test.ts`, `internals/stateAttributesMapping.test.ts`, `internals/resolveRenderedId.test.tsx`, `merge-props/mergeProps.test.ts`, `use-render/useRender.test.tsx`, `use-render/useRender.spec.tsx`, `utils/listbox-separator/ListboxSeparator.test.tsx`, `select/item-indicator/SelectItemIndicator.test.tsx`, `combobox/item-indicator/ComboboxItemIndicator.test.tsx`, `packages/utils/src/useId.test.tsx`, `packages/utils/src/useId.react17.test.tsx`, `packages/utils/src/useMergedRefs.test.tsx`, `packages/utils/src/getReactElementRef.test.tsx`, `test/describeConformance.tsx`, `test/conformanceTests/*.tsx` |
| Prefix | `RND` |
| Status | approved |
| Used by | every component document (all 41 modules render through this pipeline); `architecture/transitions-and-mounting.md` (transition data attributes), `architecture/popup-store-and-handles.md` (popup state mappings), `architecture/field-and-form-infrastructure.md` (validity mapping), `architecture/button-behavior.md` (`nativeButton`, function-form props), `architecture/event-details-and-reasons.md` (`BaseUIEvent` vs event details) |

Citation note: source paths are relative to `reference/base-ui/packages/react/src/` per conventions §3.1. Paths beginning with `test/` are relative to `reference/base-ui/packages/react/` (the conformance suite lives outside `src/`). Paths beginning with `packages/utils/` or `docs/` are relative to `reference/base-ui/`.

## 1. Purpose

Every Base UI part (Menu.Trigger, Switch.Thumb, Dialog.Popup, …) turns three things into exactly one host DOM element: its public parameters, its internal state object, and the attributes and handlers its behavior needs. The code path that does this is a single internal function, `useRenderElement`, and every one of the 172 parts in the library calls it. The public `useRender` utility exposes the same pipeline to consumers building their own components, and the public `mergeProps` utility is the attribute-merging core that the pipeline uses.

Because the pipeline is shared, the following guarantees hold uniformly across the library, and component documents do not restate them:

* a consumer can replace the default element with their own element or component (element substitution), or take full control through a callback;
* `className` and `style` may be plain values or functions of the part's state;
* every field of the part's state object is projected onto the element as a `data-*` attribute, with a small set of custom projections;
* consumer-supplied attributes and handlers are merged with the part's own in a fixed precedence, and consumer handlers can veto the part's internal handler with `preventBaseUIHandler()`;
* consumer DOM handles, substitute-element handles and the part's internal handles all receive the same element;
* ids are generated with a `base-ui-` prefix unless the consumer supplies one, in a way that is identical in server-rendered and client-rendered markup.

A port cannot skip this document: a translated test suite exercises these guarantees on every part through the conformance suite (§4.9), and the merge precedence decides which handler runs first and whether a consumer's `onClick` can stop a trigger from opening its popup.

## 2. Concepts and terminology

| Base UI / React name | Neutral term used in this and the component documents | Meaning |
|---|---|---|
| part | part | One named sub-component of a component (e.g. `Menu.Trigger`). Renders exactly one host element. |
| props | public parameters | The named inputs a consumer passes to a part. |
| `render` prop | element substitution | The consumer supplies what to render in place of the default element. Two forms: *substitute element* and *substitute callback*. |
| render element (`render={<X />}`) | substitute element | An element value; the part merges its attributes onto it. |
| render function (`render={(props, state) => …}`) | substitute callback | A function `(attributes, state) → element`; it receives the fully merged attribute set and is responsible for applying it. No merging happens after it returns. |
| default element / intrinsic tag | default element | The tag the part renders when no substitution is supplied (`div`, `button`, `span`, `h3`, …). Each component document states it per part. |
| `ref` / forwarded ref | DOM handle / consumer DOM handle | A slot that receives the rendered DOM element when it attaches and `null` when it detaches. May be a callback (optionally returning a cleanup) or an object with a `current` field. |
| `State` interface | state object | A plain object the part computes each render from its parameters and internal state; the only input to data attributes, class/style functions and the substitute callback's second argument. |
| state attributes / `data-*` | state data attributes | `data-*` attributes derived from the state object (§4.4). |
| `stateAttributesMapping` | custom attribute mapping | A per-field function that replaces the default projection for that field (§4.4). |
| internal props (`props` parameter of `useRenderElement`) | part-supplied attributes | Attributes and handlers the part itself contributes (`role`, `aria-*`, `onClick`, …). |
| element props / `elementProps` (rest of consumer props) | consumer-supplied attributes | Everything the consumer passed that is not `className`, `style`, `render` or a behavioral parameter. |
| event handler prop (`onXxx`) | handler | Any attribute whose name starts with `on` followed by an uppercase ASCII letter and whose value is a function (or `undefined`). |
| synthetic event | DOM event object | Any first argument that is a non-null object exposing a `nativeEvent` member. Other first arguments (booleans, values, event-details objects) are *non-DOM arguments*. |
| `preventBaseUIHandler()` / `baseUIHandlerPrevented` | handler veto / veto flag | A method installed on a DOM event object by the merge; calling it skips the handlers that would run after the current one in the chain. |
| props getter (function-form input of `mergeProps`) | function-form input | A function `(accumulated) → attributes`; its return value replaces the accumulation. |
| `mergeProps` / `mergePropsN` | attribute merge | The public merging algorithm (§5, ALG-02…06). |
| `useMergedRefs` | handle fan-out | One callback that forwards the element to every participating DOM handle. |
| `useRenderElement` | rendering pipeline | The internal function every part calls (§5, ALG-01). |
| `useRender` | public rendering pipeline | The exported hook wrapping the pipeline for custom components (§3.2). |
| `enabled` | conditional rendering switch | `false` makes the pipeline render nothing and do no work. |
| `useId` / `useBaseUiId` | generated id | A document-unique id, `base-ui-` prefixed, identical between server and client markup. |
| React.lazy / Flight wrapper | deferred element | A wrapper around an element whose value may not be available yet (§4.8, §8). |
| hydration | server-to-client takeover | The client adopting server-rendered markup; attributes (ids above all) must match. |
| `React.cloneElement` | clone with attributes | Producing the substitute element again with a different attribute set; its identity key is preserved. |

### 2.1 Precedence summary

The final attribute set of a part's host element is built from the sources below, lowest precedence first. "Special" attributes (`className`, `style`, handlers, DOM handle) merge instead of overriding.

| Source (lowest → highest) | Non-special attributes (incl. `children`, `id`, `role`, `aria-*`, `data-*`) | `className` | `style` | Handlers (run order) | DOM handle |
|---|---|---|---|---|---|
| 1. State data attributes (§4.4) | overridden by anything above | — | — | — | — |
| 2. Part-supplied attributes, then consumer-supplied attributes (the `props` list of the pipeline, left to right; by convention the consumer's attributes are the last entry, see §6) | later entry wins | later entry's class is placed **first** | later entry's keys win | later entry's handler runs **first** | joins fan-out |
| 3. Class parameter (`className`, resolved against state) | — | placed **first** (before everything accumulated so far) | — | — | — |
| 4. Style parameter (`style`, resolved against state) | — | — | keys win | — | — |
| 5. Substitute element's own attributes | win (an explicit `undefined` also wins) | placed **first** | keys win | runs **first** | joins fan-out |

Resulting observable orders for a part invoked as `<Part className="c" style={s} onClick={h} render={<X className="x" onClick={hx} style={sx} />}>` where the part itself supplies `className="p"`, `style={sp}`, `onClick={hp}`:

* class string: `x c p`;
* style: keys of `sp`, overridden by keys of `s`, overridden by keys of `sx` (key order: first-seen position is kept, see RND-DOM-16);
* click handlers run `hx`, then `h`, then `hp`; any of them may call `event.preventBaseUIHandler()` to skip the ones after it;
* the consumer's DOM handle, `X`'s own handle and the part's internal handle(s) all receive the host element.

When the substitution is a callback instead of an element, steps 1–4 produce the attribute set, which is passed to the callback together with the state object; step 5 does not exist, and nothing is merged after the callback returns.

## 3. Public surface

### 3.0 Common public parameters of every part

These four parameters exist on every part that renders an element. Their semantics are defined here; component documents list them in their props tables by reference to the IDs below and do not restate them.

| Prop | Type | Default | Description | Defined by |
|---|---|---|---|---|
| `className` | `string \| ((state: State) => string \| undefined)` | — | CSS class for the host element, or a function of the part's state object returning one. Merged in front of the part's own classes (and behind a substitute element's class). | RND-API-01, RND-DOM-14, RND-DOM-15, RND-DOM-17 |
| `style` | `CSSProperties \| ((state: State) => CSSProperties \| undefined)` | — | Inline style for the host element, or a function of the state object returning one. Overrides the part's own style keys; overridden by a substitute element's style keys. | RND-API-02, RND-DOM-16, RND-DOM-18 |
| `render` | `ReactElement \| ((props: HTMLProps, state: State) => ReactElement)` | — | Element substitution: an element to render instead of the default (attributes merged onto it), or a callback receiving the merged attribute set and the state object. | RND-API-03, RND-STR-02…04, RND-STR-07 |
| `ref` | DOM handle | — | Receives the host element on attach and `null` on detach, together with any substitute-element handle and the part's internal handles. | RND-API-05, RND-DOM-20…24, RND-DOM-41 |

Source: `internals/types.ts:36-61`. Docs: `handbook/styling/page.mdx` §Style hooks; `handbook/composition/page.mdx`.

### 3.1 `mergeProps` (`@base-ui/react/merge-props`)

Exported names (`merge-props/index.ts:1` re-exports everything from `mergeProps.ts`): `mergeProps`, `mergePropsN`, `makeEventPreventable`, `mergeClassNames`. The docs outline (`docs/src/app/(docs)/react/utils/page.mdx` §mergeProps) lists all four.

| Export | Signature | Meaning |
|---|---|---|
| `mergeProps` | `(a, b, c?, d?, e?) → merged` — each input is an attribute object, a function-form input `(accumulated) → attributes`, or `undefined` | Merges up to five inputs left to right (§5 ALG-02). `merge-props/mergeProps.ts:42-83`. |
| `mergePropsN` | `(inputs: Input[]) → merged` | Same algorithm over an array of any length. Returns a shared empty object for an empty array. `merge-props/mergeProps.ts:99-115`. |
| `makeEventPreventable` | `(event) → event` | Installs `preventBaseUIHandler()` on a DOM event object and returns it (§5 ALG-04). `merge-props/mergeProps.ts:257-263`. |
| `mergeClassNames` | `(ours, theirs) → string \| undefined` | `theirs` first, then a space, then `ours`; falsy operands are skipped (§5 ALG-05). `merge-props/mergeProps.ts:265-279`. |

Docs: `docs/src/app/(docs)/react/utils/merge-props/page.mdx` ("How merging works", "Passing a function instead of an object", "API reference").

### 3.2 `useRender` (`@base-ui/react/use-render`)

Exported names (`use-render/index.ts:1-2`): `useRender` (function and type namespace), plus the types `UseRenderRenderProp`, `UseRenderElementProps`, `UseRenderComponentProps`, `UseRenderParameters`, `UseRenderReturnValue`, `UseRenderState`, and re-exported `HTMLProps`, `ComponentRenderFn`.

`useRender(params)` returns the element to render, or nothing when `enabled` is `false` (`use-render/useRender.ts:11-19`).

| Parameter | Type | Default | Description |
|---|---|---|---|
| `render` | substitute element \| `(attributes, state) → element` | — | Element substitution (§4.2). `useRender.ts:49` |
| `ref` | DOM handle \| DOM handle[] | — | Handle(s) that receive the rendered element; merged with the substitute element's own handle and any `ref` in `props`. `useRender.ts:53` |
| `state` | object | `{}` | Passed as the second argument to the substitute callback; every field is projected to a `data-*` attribute (§4.4). `useRender.ts:58` |
| `stateAttributesMapping` | `{ [field]: (value) → Record<string,string> \| null }` | — | Custom projection per state field (§4.4). `useRender.ts:64` |
| `props` | attribute object | — | Attributes to put on the element; merged with the state attributes (§2.1 row 2). A single object, not a list. `useRender.ts:71` |
| `enabled` | boolean | `true` | `false` renders nothing and skips all work (§4.6). `useRender.ts:77` |
| `defaultTagName` | tag name | `'div'` | Element rendered when `render` is absent. `useRender.ts:82`, `useRender.ts:18` |

Return value: `ReactElement` when `enabled` is not `false`, `null` when it is `false` (`useRender.ts:85-87`; `use-render/useRender.spec.tsx:7-32` pins the three cases: omitted → element, `true` → element, `false` → nothing, boolean → either).

Type namespace (`useRender.ts:91-111`): `useRender.State` (empty), `useRender.RenderProp<TState>`, `useRender.ElementProps<ElementType>` (the element's native attributes incl. `ref`), `useRender.ComponentProps<ElementType, TState, RenderFunctionProps>` (native attributes plus `render`), `useRender.Parameters<TState, RenderedElementType, Enabled>`, `useRender.ReturnValue<Enabled>`.

Docs: `docs/src/app/(docs)/react/utils/use-render/page.mdx` ("Examples", "Merging props", "Merging refs", "TypeScript", "Migrating from Radix UI", "Render prop and polymorphism").

### 3.3 Shared public types (`@base-ui/react/types`, `types/index.ts`)

| Type | Definition | Notes |
|---|---|---|
| `HTMLProps<T = any>` | native attributes of an element plus optional `ref` | `types/index.ts:8-10`. The attribute set handed to a substitute callback. |
| `ComponentRenderFn<Props, State>` | `(props: Props, state: State) → element` | `types/index.ts:18-21`. The substitute-callback shape. |
| `BaseUIEvent<E>` | `E & { preventBaseUIHandler(): void; readonly baseUIHandlerPrevented?: boolean }` | `types/index.ts:23-26`. The DOM event object as seen by every handler on a part. |
| `BaseUIChangeEventDetails`, `BaseUIGenericEventDetails` | re-exported from `internals/createBaseUIEventDetails` | `types/index.ts:3-6`. Specified in `architecture/event-details-and-reasons.md`. |

### 3.4 Shared internal types (`internals/types.ts`, exported on the `@base-ui/react/internals/types` subpath)

| Type | Definition | Notes |
|---|---|---|
| `BaseUIComponentProps<ElementType, State, RenderFunctionProps = HTMLProps>` | all native attributes of `ElementType` (including `ref`), each handler retyped to receive `BaseUIEvent`, **minus** `className`, `color`, `defaultValue`, `defaultChecked`, `style`; **plus** `className?: string \| (state) → string \| undefined`, `render?: element \| ComponentRenderFn<RenderFunctionProps, State>`, `style?: object \| (state) → object \| undefined` | `internals/types.ts:36-61`. Every part's public parameter interface extends this. Component documents list only their additional parameters. |
| `WithBaseUIEvent<T>` | maps every handler-typed member of `T` to accept `BaseUIEvent<E>` instead of `E` | `internals/types.ts:17-30`. |
| `MaybeBaseUIEvent<E>` | `E` with optional `preventBaseUIHandler` / `baseUIHandlerPrevented` | `internals/types.ts:6-7`. Used by internal handlers that may receive an un-merged event. |
| `NativeButtonProps` | `{ nativeButton?: boolean }` default `true` | `internals/types.ts:63-71`. Semantics in `architecture/button-behavior.md`. |
| `NonNativeButtonProps` | `{ nativeButton?: boolean }` default `false` | `internals/types.ts:73-81`. Semantics in `architecture/button-behavior.md`. |
| `Orientation` | `'horizontal' \| 'vertical'` | `internals/types.ts:97`. |
| `Simplify<T>`, `RequiredExcept<T, K>` | type-level helpers | `internals/types.ts:87`, `:95`. No runtime behavior. |
| `FloatingUIOpenChangeDetails` | `{ open, reason, nativeEvent, nested, triggerElement? }` | `internals/types.ts:9-15`. Specified in `architecture/floating-and-positioning.md`. |
| `StateAttributesMapping<State>` | `{ [field in keyof State]?: (value) → Record<string,string> \| null }` | `internals/getStateAttributesProps.ts:1-3`. |
| `TransitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | `internals/useTransitionStatus.ts:6`. Semantics in `architecture/transitions-and-mounting.md`. |

The following internals are also reachable through package subpaths but are not documented on the site: `internals/useRenderElement`, `internals/getStateAttributesProps`, `internals/stateAttributesMapping`, `internals/useBaseUiId` (`packages/react/package.json:86,93,100,105`). A port is not required to expose them.

### 3.5 Public parameters of the shared parts

`Select.ItemIndicator` / `Combobox.ItemIndicator` (in addition to §3.0):

| Prop | Type | Default | Description | Defined by |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while the item is not selected; enables the exit transition. | RND-API-12, RND-ANI-01…03 |
| `children` | node | `'✔️'` | Replaces the default check-mark content. | RND-DOM-50 |

`Autocomplete.Separator` / `Combobox.Separator` / `Select.Separator` (in addition to §3.0):

| Prop | Type | Default | Description | Defined by |
|---|---|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Reflected as `data-orientation`; no ARIA effect. | RND-API-13, RND-DOM-53, RND-ARIA-02 |

## 4. Behavioral requirements

### 4.1 Structure: one host element per part (STR)

- **RND-STR-01** — MUST. Every part renders exactly one host element produced by the rendering pipeline; the part contributes no wrapper element of its own around it.
  Source: `internals/useRenderElement.tsx:23-49`, `:157-199`; e.g. `separator/Separator.tsx:20-24`. Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards custom props to the default element".
- **RND-STR-02** — MUST. When the `render` parameter is absent or falsy (`undefined`, `null`, `false`), the part renders its documented default element.
  Source: `internals/useRenderElement.tsx:163`, `:197-198`. Test: [L2] `use-render/useRender.test.tsx` "renders div by default if no defaultTagName and no render params are provided".
- **RND-STR-03** — MUST. When `render` is a substitute element, the part renders that element (cloned with the merged attribute set of §2.1) and does not render its default element.
  Source: `internals/useRenderElement.tsx:171-195`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts render as a React element and clones it with merged props"; `use-render/useRender.test.tsx` "is overwritten by the render prop".
- **RND-STR-04** — MUST. When `render` is a substitute callback, the part calls it on every render of the part with `(attributes, state)` — `attributes` being the merged set of §2.1 rows 1–4 including the DOM handle — and renders whatever it returns without further merging.
  Source: `internals/useRenderElement.tsx:164-169`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts render as a function that receives props and state"; `use-render/useRender.test.tsx` "render props does not overwrite className in a render function when unspecified".
- **RND-STR-05** — MUST. A default element of tag `button` carries `type="button"` unless the merged attributes supply a `type`.
  Source: `internals/useRenderElement.tsx:226-228`. Test: none (relied on by every button part's tests; see `architecture/button-behavior.md`).
- **RND-STR-06** — MUST. A default element of tag `img` carries `alt=""` unless the merged attributes supply an `alt`.
  Source: `internals/useRenderElement.tsx:229-231`. Test: none (see `components/avatar.md`).
- **RND-STR-07** — MUST. A substitute element may itself be a Base UI part carrying its own `render`; substitution nests to any depth, each level applying §2.1 to the attribute set it receives from the level above.
  Source: `internals/useRenderElement.tsx:171-195` (the clone carries the merged attributes as that part's consumer-supplied attributes). Test: none at this layer (exercised by tooltip/dialog/menu composition tests). Docs: `handbook/composition/page.mdx` §Composing multiple components.
- **RND-STR-08** — MUST. The public rendering pipeline (`useRender`) renders a `div` when neither `defaultTagName` nor `render` is given, and the `defaultTagName` tag when it is given; changing `defaultTagName` between renders changes the rendered tag.
  Source: `use-render/useRender.ts:18`. Test: [L2] `use-render/useRender.test.tsx` "renders div by default if no defaultTagName and no render params are provided", "renders the element with the default tag with no render prop".
- **RND-STR-09** — MUST. In the public rendering pipeline, `render` takes precedence over `defaultTagName`; changing `defaultTagName` while `render` is set has no effect.
  Source: `use-render/useRender.ts:18`, `internals/useRenderElement.tsx:163`. Test: [L2] `use-render/useRender.test.tsx` "is overwritten by the render prop".
- **RND-STR-10** — MUST. The shared `ItemIndicator` implementation renders a `span` and is the implementation of `Select.ItemIndicator` and `Combobox.ItemIndicator`; those public parts only decide whether to mount it (RND-ANI-01) and pass `selected` from their item context.
  Source: `utils/ItemIndicator.tsx:28`; `select/item-indicator/SelectItemIndicator.tsx:18-25`; `combobox/item-indicator/ComboboxItemIndicator.tsx:18-25`. Test: [L2] `select/item-indicator/SelectItemIndicator.test.tsx` (conformance, `refInstanceof: HTMLSpanElement`).
- **RND-STR-11** — MUST. The shared `ListboxSeparator` implementation renders a `div` and is the implementation of `Autocomplete.Separator`, `Combobox.Separator` and `Select.Separator`.
  Source: `utils/listbox-separator/ListboxSeparator.tsx:20`; `autocomplete/separator/AutocompleteSeparator.tsx`, `combobox/separator/ComboboxSeparator.tsx`, `select/separator/SelectSeparator.tsx`. Test: [L2] `utils/listbox-separator/ListboxSeparator.test.tsx` "exposes the listbox separator behavior" (3 variants: `Autocomplete.Separator`, `Combobox.Separator`, `Select.Separator`).

### 4.2 Public parameters common to every part (API)

- **RND-API-01** — MUST. Every part accepts `className` as either a string or a function `(state) → string | undefined`.
  Source: `internals/types.ts:44-48`; `utils/resolveClassName.ts:8-13`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts className as function"; `test/conformanceTests/className.tsx` "should apply the className to the root element when passed as a string".
- **RND-API-02** — MUST. Every part accepts `style` as either an inline-style object or a function `(state) → object | undefined`.
  Source: `internals/types.ts:56-60`; `utils/resolveStyle.ts:8-13`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts style as function"; `test/conformanceTests/propForwarding.tsx` "forwards the custom `style` attribute defined on the component".
- **RND-API-03** — MUST. Every part accepts `render` as either a substitute element or a substitute callback `(attributes, state) → element`.
  Source: `internals/types.ts:49-55`; `types/index.ts:18-21`. Test: [L2] `test/conformanceTests/renderProp.tsx` "renders a customized root element with a function", "renders a customized root element with an element".
- **RND-API-04** — MUST. Every part accepts every native attribute of its default element type (including `id`, `data-*`, `aria-*`, `lang`, `children`, handlers) and forwards those it does not consume to the host element; the native `color`, `defaultValue` and `defaultChecked` attributes are excluded from this set (parts that need them declare their own typed parameters).
  Source: `internals/types.ts:40-43`; `separator/Separator.tsx:16,23` (destructure-and-forward pattern). Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards custom props to the default element".
- **RND-API-05** — MUST. Every part accepts a consumer DOM handle (`ref`) that receives the host element.
  Source: `internals/types.ts:41` (`ComponentPropsWithRef`); `internals/useRenderElement.tsx:100-104`. Test: [L2] `test/conformanceTests/refForwarding.tsx` "attaches the ref".
- **RND-API-06** — MUST. Every handler a consumer passes to a part receives, for DOM events, an event object extended with `preventBaseUIHandler()` and the readonly `baseUIHandlerPrevented` flag.
  Source: `internals/types.ts:17-30`, `types/index.ts:23-26`; `merge-props/mergeProps.ts:257-263`. Test: [L2] `internals/useRenderElement.test.tsx` "makes single prop objects preventable".
- **RND-API-07** — MUST. The public rendering pipeline accepts exactly the parameters of §3.2 with the defaults stated there (`enabled` → `true`, `defaultTagName` → `'div'`, `state` → `{}`).
  Source: `use-render/useRender.ts:41-83`, `internals/useRenderElement.tsx:66-72`. Test: [L2] `use-render/useRender.test.tsx` "handles undefined state", "renders div by default if no defaultTagName and no render params are provided".
- **RND-API-08** — MUST. The `ref` parameter of both pipelines accepts either a single DOM handle or a list of DOM handles (list entries may be `undefined`/`null`); every non-null entry receives the element.
  Source: `internals/useRenderElement.tsx:100-104`, `:258`; `use-render/useRender.ts:53`. Test: [L2] `use-render/useRender.test.tsx` "refs are handled as expected"; `internals/useRenderElement.test.tsx` "updates merged refs and event handlers when ref shape changes across rerenders".
- **RND-API-09** — MUST. `mergeProps` accepts two to five inputs, each an attribute object, a function-form input, or `undefined`.
  Source: `merge-props/mergeProps.ts:42-61`. Test: [L1] `merge-props/mergeProps.test.ts` "merges multiple event handlers" (three inputs), "merges styles with undefined". Docs: `CHANGELOG.md v1.1.0 "Make \`mergeProps\` public"`.
- **RND-API-10** — MUST. `mergePropsN` accepts an array of the same input kinds, of any length including zero.
  Source: `merge-props/mergeProps.ts:99-115`. Test: [L1] `merge-props/mergeProps.test.ts` "makes a first-position synthetic event handler preventable in mergePropsN".
- **RND-API-11** — MUST. `makeEventPreventable` and `mergeClassNames` are exported alongside `mergeProps` with the behavior of ALG-04 and ALG-05.
  Source: `merge-props/mergeProps.ts:257-279`; `merge-props/index.ts:1`. Test: none. Docs: `docs/src/app/(docs)/react/utils/page.mdx` §mergeProps (exports list).
- **RND-API-12** — MUST. The shared `ItemIndicator` accepts `selected: boolean` (required, supplied by the public wrapper from its item context, never by the consumer), `keepMounted?: boolean` (default `false`) and `children`, in addition to the common parameters.
  Source: `utils/ItemIndicator.tsx:62-65`; `select/item-indicator/SelectItemIndicator.tsx:39-48`; `combobox/item-indicator/ComboboxItemIndicator.tsx:28-38`. Test: [L2] `select/item-indicator/SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false".
- **RND-API-13** — MUST. The shared `ListboxSeparator` accepts `orientation?: Orientation` with default `'horizontal'`, in addition to the common parameters.
  Source: `utils/listbox-separator/ListboxSeparator.tsx:16`, `:27-33`. Test: [L2] `utils/listbox-separator/ListboxSeparator.test.tsx` "has role=\"presentation\" and defaults to horizontal".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- RND-STA-01 — for mirrored fields (`side`, `align`, `anchorHidden`, `transitionStatus`, swipe/scroll flags per ADR 04 §2) the data attribute is JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.

### 4.3 State object (STA)

- **RND-STA-01** — MUST. Within one render pass, the class function, the style function, the substitute callback's second argument and the state-data-attribute projection all observe the same state values. Server: see ADR 01 relaxation table.
  Source: `internals/useRenderElement.tsx:45`, `:67`, `:74-78`, `:168`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts render as a function that receives props and state" (`state` equals `{ active: true }` while `data-active` is projected) (L4 for mirrored fields, ADR 01 relaxation table).
- **RND-STA-02** — MUST. A part that defines no state object behaves as if its state were an empty object: class/style functions and the substitute callback receive `{}` (never `null`/`undefined`) and no state data attributes are emitted.
  Source: `internals/useRenderElement.tsx:45`, `:67`. Test: [L2] `use-render/useRender.test.tsx` "handles undefined state", "handles empty state".
- **RND-STA-03** — MUST. In the public rendering pipeline the `state` object is consumer-defined and every one of its fields is projected by the same rules as a part's state (§4.4), including the custom mapping.
  Source: `use-render/useRender.ts:18`, `:58`, `:64`. Test: [L2] `use-render/useRender.test.tsx` "converts state to data attributes automatically", "supports custom stateAttributesMapping for kebab-case conversion".

### 4.4 State data attributes (DOM)

Default projection, applied to each field of the state object in the object's own enumeration order (`internals/getStateAttributesProps.ts:12-29`):

| State value | Emitted attribute |
|---|---|
| `true` | `data-<field lowercased>=""` |
| `false`, `null`, `undefined`, `0`, `NaN`, `''` | nothing |
| any other value (non-empty string, non-zero number, object) | `data-<field lowercased>="<value converted to string>"` |

- **RND-DOM-01** — MUST. A state field whose value is `true` is emitted as a `data-*` attribute with an empty-string value.
  Source: `internals/getStateAttributesProps.ts:24-25`. Test: [L1] `internals/getStateAttributesProps.test.ts` "changes true values to a data-attribute without a value"; `use-render/useRender.test.tsx` "converts boolean values in state to data attributes".
- **RND-DOM-02** — MUST. A state field whose value is `false` is not emitted.
  Source: `internals/getStateAttributesProps.ts:26`. Test: [L1] `internals/getStateAttributesProps.test.ts` "does not include false values".
- **RND-DOM-03** — MUST. A state field whose value is truthy and not `true` is emitted with the value's string conversion (`42` → `"42"`, `99.9` → `"99.9"`, `'vertical'` → `"vertical"`).
  Source: `internals/getStateAttributesProps.ts:26-27`. Test: [L1] `internals/getStateAttributesProps.test.ts` "converts the state fields to data attributes"; `use-render/useRender.test.tsx` "converts number values in state to data attributes".
- **RND-DOM-04** — MUST. A state field whose value is falsy and not `false` (`undefined`, `null`, `0`, `''`, `NaN`) is not emitted.
  Source: `internals/getStateAttributesProps.ts:24-28`. Test: [L1] `use-render/useRender.test.tsx` "handles undefined values in state", "converts number values in state to data attributes" (`count: 0` absent).
- **RND-DOM-05** — MUST. The attribute name is `data-` followed by the field name converted to lowercase; no other transformation (no kebab-casing) is applied (`readOnly` → `data-readonly`).
  Source: `internals/getStateAttributesProps.ts:25,27`. Test: [L1] `internals/getStateAttributesProps.test.ts` "changes the fields names to lowercase".
- **RND-DOM-06** — MUST. When a custom attribute mapping defines an entry for a field, the default projection is not applied to that field; instead the mapping function is called with the field's value and every key/value pair of its non-null result is emitted verbatim.
  Source: `internals/getStateAttributesProps.ts:15-22`. Test: [L1] `internals/getStateAttributesProps.test.ts` "supports custom mapping"; `use-render/useRender.test.tsx` "supports custom stateAttributesMapping for kebab-case conversion".
- **RND-DOM-07** — MUST. When the mapping function returns `null`, nothing is emitted for that field (the default projection is still skipped).
  Source: `internals/getStateAttributesProps.ts:17-21`. Test: [L1] `internals/getStateAttributesProps.test.ts` "supports nulls returned from custom mapping".
- **RND-DOM-08** — MUST. A mapping function receives only the field's value; it has no access to other state fields.
  Source: `internals/getStateAttributesProps.ts:2`, `:16`. Test: none.
- **RND-DOM-09** — MUST. When several mapping results, or a mapping result and a later default projection, produce the same attribute name, the later one (in field enumeration order) wins.
  Source: `internals/getStateAttributesProps.ts:18` (assign-into). Test: none.
- **RND-DOM-10** — MUST. A part-supplied or consumer-supplied attribute with the same name as a state data attribute overrides the state data attribute.
  Source: `internals/useRenderElement.tsx:86-88` (part/consumer attributes are merged over the state attributes). Test: [L2] `use-render/useRender.test.tsx` "props override state-based data attributes", "merges state-based data attributes with existing props".
- **RND-DOM-11** — MUST. The shared transition mapping projects `transitionStatus` as: `'starting'` → `data-starting-style=""`; `'ending'` → `data-ending-style=""`; `'idle'` or `undefined` → nothing. The semantics of the status itself are specified in `architecture/transitions-and-mounting.md`.
  Source: `internals/stateAttributesMapping.ts:7-20`; `internals/TransitionStatusDataAttributes.ts:4,9`. Test: [L4] `internals/stateAttributesMapping.test.ts` "emits the transition data attributes", "emits nothing outside a transition".
- **RND-DOM-12** — MUST. The shared collapsible panel mapping projects `open` as: `true` → `data-open=""`; `false` → `data-closed=""` (never both, never neither).
  Source: `utils/collapsibleOpenStateMapping.ts:26-35`; `collapsible/panel/CollapsiblePanelDataAttributes.ts:6,10`. Test: none at this layer (see `components/collapsible.md`, `components/accordion.md`).
- **RND-DOM-13** — MUST. The shared collapsible trigger mapping projects `open` as: `true` → `data-panel-open=""`; `false` → nothing.
  Source: `utils/collapsibleOpenStateMapping.ts:13-24`; `collapsible/trigger/CollapsibleTriggerDataAttributes.ts:4`. Test: none at this layer (see `components/collapsible.md`). Note: a differently-behaved mapping of the same name (`triggerOpenStateMapping` → `data-popup-open`) exists in `utils/popupStateMapping.ts:30-37` and is specified in `architecture/popup-store-and-handles.md`.

### 4.5 Class and style merging (DOM)

- **RND-DOM-14** — MUST. On the default element, the resulting class string is the resolved class parameter, a single space, then the part-supplied class(es); when either side is absent, the other is used alone with no extra whitespace.
  Source: `internals/useRenderElement.tsx:111-113`; `merge-props/mergeProps.ts:265-279`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts className as function" (`class="active-class test-component"`), "does not throw when className is provided with minimal props" (`class="test-class"`).
- **RND-DOM-15** — MUST. With a substitute element, the resulting class string is the substitute element's own class, a space, the resolved class parameter, a space, the part-supplied class(es) (absent operands skipped as in RND-DOM-14).
  Source: `internals/useRenderElement.tsx:171`; `merge-props/mergeProps.ts:173-175`, `:265-279`. Test: [L2] `internals/useRenderElement.test.tsx` "merges className from render element and component props", "merges className function with render element"; `test/conformanceTests/renderProp.tsx` "should merge the rendering element className with the custom component className".
- **RND-DOM-16** — MUST. The resulting inline style is the part-supplied style, overridden key-by-key by the resolved style parameter, overridden key-by-key by the substitute element's own style; a key keeps the position at which it was first seen, and newly introduced keys are appended in encounter order (serialized order `padding: 10px; color: …` for part `{padding}` then parameter `{color}`).
  Source: `internals/useRenderElement.tsx:115-117`; `merge-props/mergeProps.ts:166-172`; `packages/utils/src/mergeObjects.ts:1-15`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts style as function" (`style="padding: 10px; color: rgb(255, 0, 0);"`), "merges style from render element and component props", "merges style function with render element".
- **RND-DOM-17** — MUST. A class function that returns `undefined` or an empty string adds nothing; the part-supplied and substitute-element classes are kept unchanged.
  Source: `internals/useRenderElement.tsx:111`; `merge-props/mergeProps.ts:269-278`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts className as function that returns undefined" (`class="test-component"`).
- **RND-DOM-18** — MUST. A style function that returns `undefined` adds nothing; the accumulated style is kept unchanged.
  Source: `internals/useRenderElement.tsx:115`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts style as function that returns undefined" (`style="padding: 10px;"`).
- **RND-DOM-19** — MUST. For every attribute other than `className`, `style`, handlers and the DOM handle (this includes `children`, `id`, `role`, `aria-*`, `data-*`, `title`, `type`), a later source in §2.1 replaces an earlier one, and an explicitly `undefined` value on a later source also replaces the earlier value. Port: relaxed (ADR 02).
  Source: `merge-props/mergeProps.ts:162-184` (`for…in` visits keys set to `undefined`; default branch assigns). Test: [L1] `merge-props/mergeProps.test.ts` "merges internal props so that the ones defined first override the ones defined later" (asserts the later `title` wins); `internals/useRenderElement.test.tsx` "merges props with the same precedence as a plain render element" (`data-slot` from the substitute element wins); `internals/resolveRenderedId.test.tsx` "treats an undefined id on a render element as no id".

### 4.6 DOM handles and conditional rendering (DOM)

- **RND-DOM-20** — MUST. The consumer DOM handle receives the host element when it attaches and `null` when it detaches; the element is the one the part's default-element type documents (e.g. an `HTMLSpanElement` for `ItemIndicator`). Port: relaxed (ADR 02).
  Source: `internals/useRenderElement.tsx:100-104`; `packages/utils/src/useMergedRefs.ts:95-152`. Test: [L2] `test/conformanceTests/refForwarding.tsx` "attaches the ref"; `internals/useRenderElement.test.tsx` "handles enabled toggles across rerenders" (`ref.current` becomes `null` after disabling).
- **RND-DOM-21** — MUST. A DOM handle placed on the substitute element itself also receives the host element; it is not discarded by the merge. Port: relaxed (ADR 02).
  Source: `internals/useRenderElement.tsx:101,103` (`getReactElementRef(renderProp)` joins the fan-out), `:173`; `packages/utils/src/getReactElementRef.ts:7-16`. Test: [L2] `internals/useRenderElement.test.tsx` "handles render element with existing ref", "attaches the render element ref"; `test/conformanceTests/renderProp.tsx` "should merge the rendering element ref with the custom component ref"; `packages/utils/src/getReactElementRef.test.tsx` "returns the ref of a React element", "returns null for an element without a ref", "returns null for a fragment", "returns null when not provided a React element" (reading the substitute element's own handle).
- **RND-DOM-22** — MUST. The part's own internal handle(s) (the `ref` list it passes to the pipeline) receive the host element together with the consumer's and the substitute element's handles; all are set on attach and all are cleared (`null`) on detach. Port: relaxed (ADR 02).
  Source: `internals/useRenderElement.tsx:100-104`; `packages/utils/src/useMergedRefs.ts:101-150`. Test: [L2] `use-render/useRender.test.tsx` "refs are handled as expected"; `packages/utils/src/useMergedRefs.test.tsx` "returns a single ref-setter function that forks the ref to its inputs", "forks if only one of the branches requires a ref".
- **RND-DOM-23** — MUST. When the set of handles changes between renders while the element stays mounted, handles removed from the set are cleared to `null`, handles added to the set receive the element, and the element itself is not re-created. Port: relaxed (ADR 02).
  Source: `packages/utils/src/useMergedRefs.ts:36-40`, `:64-85`, `:87-99`. Test: [L2] `internals/useRenderElement.test.tsx` "updates merged refs and event handlers when ref shape changes across rerenders"; `packages/utils/src/useMergedRefs.test.tsx` "handles changing from no ref to some ref", "cleans up detached refs".
- **RND-DOM-24** — MUST. A callback DOM handle that returns a function is treated as returning a cleanup: on detach the cleanup is invoked instead of calling the handle with `null`; a callback handle that returns nothing is called with `null` on detach. Port: withdrawn (ADR 02).
  Source: `packages/utils/src/useMergedRefs.ts:110-116`, `:132-141`. Test: [L2] `packages/utils/src/useMergedRefs.test.tsx` "calls clean up function if it exists".
- **RND-DOM-25** — MUST. When `enabled` is `false`, the part renders nothing: no element exists, every DOM handle holds `null`, the class and style functions are not called, no state attributes are computed, and function-form entries of the part-supplied attributes are not invoked.
  Source: `internals/useRenderElement.tsx:41-43`, `:74-81`, `:97-99`, `:107-109`. Test: [L2] `internals/useRenderElement.test.tsx` "does not resolve props when disabled", "handles enabled toggles across rerenders".
- **RND-DOM-26** — MUST. Toggling `enabled` between renders mounts/unmounts the element accordingly; after re-enabling, handles are attached and handlers are active again.
  Source: `internals/useRenderElement.tsx:34-49`, `:96-105`. Test: [L2] `internals/useRenderElement.test.tsx` "handles enabled toggles across rerenders".

### 4.7 Identifiers (DOM)

- **RND-DOM-27** — MUST. When a part needs an `id` and the consumer supplies none, it generates one of the form `base-ui-<token>` where `<token>` is unique within the document. Exceptions that call the unprefixed generator and therefore render the bare unique `<token>` (no `base-ui-` prefix): `Toast.Title`/`Toast.Description`, `NavigationMenu.Viewport`, the portal node rendered by the floating portal, and popup elements whose default `id` is the popup store's / floating root's `floatingId` (e.g. `Dialog.Popup`, `Popover.Popup`). The ids remain unique and stable (RND-DOM-29); only the prefix differs. Port: relaxed (ADR 02).
  Source: `internals/useBaseUiId.ts:9-11`; `packages/utils/src/useId.ts:42-47`; exceptions `toast/utils/useToastLabelPart.ts:23`, `navigation-menu/viewport/NavigationMenuViewport.tsx:78`, `floating-ui-react/components/FloatingPortal.tsx:74, 129`, `utils/popups/popupStoreUtils.ts:76`, `floating-ui-react/hooks/useFloatingRootContext.ts:27`, `popover/popup/PopoverPopup.tsx:51, 90`, `dialog/popup/DialogPopup.tsx:43, 76`. Test: [L1] `packages/utils/src/useId.test.tsx` "can be prefixed" (first 8 characters are `base-ui-`), "can be used in in IDREF attributes".
- **RND-DOM-28** — MUST. A consumer-supplied `id` (including the empty string) is used verbatim instead of the generated one.
  Source: `packages/utils/src/useId.ts:46` (`idOverride ?? …`). Test: [L2] `packages/utils/src/useId.test.tsx` "returns the provided ID"; `packages/utils/src/useId.react17.test.tsx` "returns the provided id", "returns an empty string id override as-is".
- **RND-DOM-29** — MUST. A generated id is stable for the lifetime of the part instance (it does not change on re-render) and is identical in server-rendered markup and the first client render, so server-to-client takeover does not alter it.
  Source: `packages/utils/src/useId.ts:44-47` (delegates to the framework's deterministic id generator). Test: [L4] `packages/utils/src/useId.test.tsx` "generates an ID if one isn't provided" (server render then takeover), "provides an ID on server in React 18"; `packages/utils/src/useId.react17.test.tsx` "does not regenerate the id on subsequent renders".
- **RND-DOM-30** — MUST. When a consumer-supplied `id` is removed later, the part falls back to a generated id in the very next render; there is no intermediate render with no id. When the consumer id is added again and removed again, the same generated id returns.
  Source: `packages/utils/src/useId.ts:13-29` (fallback is generated even while an override is present), `:46`. Test: [L2] `packages/utils/src/useId.react17.test.tsx` "generates an id when the provided id is removed", "never renders without an id once the provided id is removed", "keeps the generated id when the provided id is added and removed again"; `packages/utils/src/useId.test.tsx` "generates an ID if one isn't provided" (switches to the supplied id).
- **RND-DOM-31** — MUST. Generated ids are usable as IDREF targets: two generated ids concatenated with a space form a valid `aria-labelledby` list, and a generated id with a suffix appended (`<id>-label`) is a valid distinct id.
  Source: `packages/utils/src/useId.ts:42-52`. Test: [L1] `packages/utils/src/useId.test.tsx` "can be suffixed", "can be used in in IDREF attributes".
- **RND-DOM-32** — MUST. The id that lands on a part's host element is resolved with this precedence: (1) if `render` is a substitute element that has its own `id` key — even with value `undefined` — that value is the id, with `undefined` meaning "no id" (`''`); (2) otherwise the consumer's `id` parameter if present (`''` stays `''`); (3) otherwise the generated id. A substitute callback is opaque: precedence stops at (2) and the callback is expected to apply the `id` it is handed. Port: relaxed (ADR 02).
  Source: `internals/resolveRenderedId.ts:18-28`. Test: [L2] `internals/resolveRenderedId.test.tsx` "falls back to the generated id when nothing else is given", "prefers an explicit id prop", "treats an explicitly empty id prop as no id", "prefers a render element's own id", "treats an undefined id on a render element as no id", "ignores a render element that does not set an id", "cannot see an id applied by a render function", "reads a server-created render element's id".
- **RND-DOM-33** — MUST. A part that publishes its resolved id to an owner (so the owner can reference it from `aria-controls`, `aria-labelledby`, …) publishes: `undefined` when the id in use is the generated fallback; the resolved id (including `''`) when it differs from the fallback; and `undefined` again when the element detaches. Removing an explicit id after mount publishes `undefined`.
  Source: `internals/resolveRenderedId.ts:37-51`; consumer: `menu/popup/MenuPopup.tsx:72-76`. Test: [L2] `internals/resolveRenderedId.test.tsx` "does not publish a generated fallback as an override", "publishes the id that lands on the rendered element", "publishes an empty string for an explicitly empty id", "clears an explicit id after it is removed", "releases the override when the element unmounts".
- **RND-DOM-54** — MUST. Rendered-id resolution (RND-DOM-32 precedence (1)) and publication (RND-DOM-33) are applied only by `Menu.Popup` and the filterable `Menu.List` (through `FilterDropdownList`). Every other part whose id an owner references — e.g. Dialog/Popover Title and Description, Select/Combobox labels and group labels — registers with its owner the id it computes — from its own `id` parameter, a root-derived id (Select/Combobox `Label`: `<rootId>-label`) or the generated id (`useBaseUiId`) — never the id that actually lands on the element. For those parts the owner's IDREF (`aria-labelledby`, `aria-describedby`, `aria-controls`, `for`) dangles both when a substitute callback drops the `id` it is handed and when a substitute element carries its own different `id` (which wins on the element per RND-DOM-32). A port MUST NOT silently repair these IDREFs unless it also applies RND-DOM-33 to that part.
  Source: `internals/resolveRenderedId.ts:18-51`; consumers (complete list): `menu/popup/MenuPopup.tsx:72-76`, `filter-dropdown/list/FilterDropdownList.tsx:18-27`. Test: none (component-level consequences: DLG-EDGE-18, SEL-EDGE-21, CMB-EDGE-13).

### 4.8 Deferred elements and the shared style element (DOM)

- **RND-DOM-34** — MUST. If the framework can hand the pipeline a *deferred element* (a wrapper whose element value is produced later), the pipeline unwraps it before reading its attributes or DOM handle, and then treats the unwrapped element exactly as a substitute element (same precedence, same handle fan-out). Port: withdrawn (ADR 02).
  Source: `internals/useRenderElement.tsx:33-37`, `:146-155`. Test: [L2] `internals/useRenderElement.test.tsx` "handles lazy elements", "merges props with the same precedence as a plain render element", "attaches the render element ref". Docs: `CHANGELOG.md v1.2.0 "Support lazy element in \`render\` prop"`, `CHANGELOG.md v1.8.0 "Fix prop and ref merging for lazy render elements"`. (React-specific; see §8.)
- **RND-DOM-35** — MUST. The shared "disable scrollbar" style hook consists of the class name `base-ui-disable-scrollbar` and a style element whose text is exactly `.base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}`; the style element accepts a CSP nonce, is de-duplicated across all parts that emit it (keyed by the string `base-ui-disable-scrollbar`), and is emitted by `ScrollArea.Root` and `Select.Popup` unless their `disableStyleElements` parameter is set.
  Source: `utils/styles.tsx:1-12`; `scroll-area/root/ScrollAreaRoot.tsx:345`; `select/popup/SelectPopup.tsx:478`; class applied at `scroll-area/viewport/ScrollAreaViewport.tsx:370`, `select/popup/SelectPopup.tsx:461`, `select/list/SelectList.tsx:43`. Test: none at this layer (see `components/scroll-area.md`, `components/select.md`). The nonce source (`CSPProvider`) is specified in `architecture/shared-utilities.md`.

### 4.9 Conformance requirements every part satisfies (DOM)

The library runs one shared suite, `describeConformance(minimalElement, () => options)`, against every part (`test/describeConformance.tsx:55-78`). Options: `render` (how to mount the part with any required ancestors), `refInstanceof` (expected element class), `testRenderPropWith` (tag used for substitution tests, default `'div'`), `button` (part accepts `nativeButton`), `wrappingAllowed` (default `true`). Every component document lists the parts it runs the suite on; the requirements below are therefore universal.

- **RND-DOM-36** — MUST. Unrecognized attributes (`lang`, arbitrary `data-*`) passed to a part appear on its default host element with the given values.
  Source: `internals/useRenderElement.tsx:81`, `:198`. Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards custom props to the default element".
- **RND-DOM-37** — MUST. The same unrecognized attributes are included in the attribute set handed to a substitute callback, so that spreading that set onto an element yields them.
  Source: `internals/useRenderElement.tsx:168`. Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards custom props to the customized element defined with a function".
- **RND-DOM-38** — MUST. The same unrecognized attributes appear on a substitute element.
  Source: `internals/useRenderElement.tsx:171-195`. Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards custom props to the customized element defined using JSX".
- **RND-DOM-39** — MUST. An inline style object passed as the `style` parameter appears in the host element's `style` attribute.
  Source: `internals/useRenderElement.tsx:115-117`. Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards the custom `style` attribute defined on the component".
- **RND-DOM-40** — MUST. An inline style set on a substitute element appears in the host element's `style` attribute.
  Source: `internals/useRenderElement.tsx:171`; `merge-props/mergeProps.ts:166-172`. Test: [L2] `test/conformanceTests/propForwarding.tsx` "forwards the custom `style` attribute defined on the render element".
- **RND-DOM-41** — MUST. The consumer DOM handle receives an instance of the part's documented element class (`HTMLDivElement`, `HTMLButtonElement`, `HTMLSpanElement`, …). Port: relaxed (ADR 02).
  Source: `internals/useRenderElement.tsx:100-104`. Test: [L2] `test/conformanceTests/refForwarding.tsx` "attaches the ref".
- **RND-DOM-42** — MUST. A substitute callback may return a tree whose root is a wrapper around the element that receives the attributes and the DOM handle (when `wrappingAllowed`); the attributes given to the callback (e.g. `data-test-value`) land on the element the callback spreads them onto, and exactly one wrapper exists.
  Source: `internals/useRenderElement.tsx:164-169`. Test: [L2] `test/conformanceTests/renderProp.tsx` "renders a customized root element with a function".
- **RND-DOM-43** — MUST. A substitute element may be a custom component that forwards the DOM handle and spreads the received attributes onto its own root; the part's attributes and handle then reach that root (optionally through one wrapper when `wrappingAllowed`).
  Source: `internals/useRenderElement.tsx:171-195`. Test: [L2] `test/conformanceTests/renderProp.tsx` "renders a customized root element with an element"; `internals/useRenderElement.test.tsx` "forwards ref to render element". Docs: `handbook/composition/page.mdx` §Composing custom React components ("must forward the `ref`, and spread all the received props").
- **RND-DOM-44** — MUST. The consumer DOM handle passes through a substitute callback: the element it receives has the tag the callback rendered and carries the attributes the part forwarded (`data-testid`).
  Source: `internals/useRenderElement.tsx:100-104`, `:168`. Test: [L2] `test/conformanceTests/renderProp.tsx` "should pass the ref to the custom component".
- **RND-DOM-45** — MUST. The consumer DOM handle and a DOM handle on the substitute element both receive the same host element, with the same tag and attributes.
  Source: `internals/useRenderElement.tsx:101,103`, `:173`. Test: [L2] `test/conformanceTests/renderProp.tsx` "should merge the rendering element ref with the custom component ref".
- **RND-DOM-46** — MUST. A string class parameter and the substitute element's own class are both present in the host element's class list.
  Source: `internals/useRenderElement.tsx:111-113`, `:171`. Test: [L2] `test/conformanceTests/renderProp.tsx` "should merge the rendering element className with the custom component className".
- **RND-DOM-47** — MUST. A class function's result and the substitute element's own class are both present in the host element's class list.
  Source: `internals/useRenderElement.tsx:74`, `:111-113`, `:171`. Test: [L2] `test/conformanceTests/renderProp.tsx` "should merge the rendering element resolved className with the custom component className".
- **RND-DOM-48** — MUST. A string class parameter is applied to the default host element.
  Source: `internals/useRenderElement.tsx:111-113`. Test: [L2] `test/conformanceTests/className.tsx` "should apply the className to the root element when passed as a string".
- **RND-DOM-49** — MUST. A part that accepts `nativeButton` and is conformance-tested with `button` substitution also satisfies RND-DOM-36…DOM-40 and RND-DOM-42…DOM-47 when substituted with a `div` and `nativeButton={false}`.
  Source: `test/describeConformance.tsx:64-75`. Test: [L2] `test/describeConformance.tsx` "with a non-native button" (re-runs `propForwarding` and `renderProp` only; `refForwarding` and `className` are not re-run). Semantics of `nativeButton` are in `architecture/button-behavior.md`.

### 4.10 Shared part implementations (DOM, ARIA, ANI)

`ItemIndicator` (`utils/ItemIndicator.tsx`), state object `{ selected: boolean, transitionStatus }`:

- **RND-DOM-50** — MUST. The indicator's default content is the single character `✔️` (U+2714 U+FE0F); consumer-supplied `children` replace it.
  Source: `utils/ItemIndicator.tsx:31-37` (default entry precedes consumer attributes). Test: none.
- **RND-DOM-51** — MUST. The indicator carries `data-selected=""` while `selected` is `true` and no `data-selected` attribute otherwise; the `selected` parameter itself is never emitted as an attribute.
  Source: `utils/ItemIndicator.tsx:17` (`selected` is consumed), `:23-26`. Test: [L2] `select/item-indicator/SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false" (`data-selected` present, no `selected` attribute); `combobox/item-indicator/ComboboxItemIndicator.test.tsx` "updates a mounted indicator when its item becomes unselected".
- **RND-DOM-52** — MUST. The indicator emits the transition attributes of RND-DOM-11 from its `transitionStatus`.
  Source: `utils/ItemIndicator.tsx:38`; `select/item-indicator/SelectItemIndicatorDataAttributes.ts:6,10`. Test: [L4] `select/item-indicator/SelectItemIndicator.test.tsx` "settles out of its transition state after the item is deselected".
- **RND-ARIA-01** — MUST. The indicator carries `aria-hidden="true"` unless the consumer overrides it.
  Source: `utils/ItemIndicator.tsx:33`. Test: [L2] `select/item-indicator/SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false".
- **RND-ANI-01** — MUST. `Select.ItemIndicator` and `Combobox.ItemIndicator` render nothing while their item is not selected unless `keepMounted` is `true`; when the item becomes selected the element appears, and when it becomes unselected (without `keepMounted`) the element is removed immediately, with no exit transition.
  Source: `select/item-indicator/SelectItemIndicator.tsx:20-23`; `combobox/item-indicator/ComboboxItemIndicator.tsx:20-23`. Test: [L2] `select/item-indicator/SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false"; `combobox/item-indicator/ComboboxItemIndicator.test.tsx` same title.
- **RND-ANI-02** — MUST. With `keepMounted`, the same element instance persists across selection changes (it is not re-created when selection toggles).
  Source: `select/item-indicator/SelectItemIndicator.tsx:20-25`. Test: [L2] `select/item-indicator/SelectItemIndicator.test.tsx` "preserves the kept-mounted element across selection changes"; `combobox/item-indicator/ComboboxItemIndicator.test.tsx` same title.
- **RND-ANI-03** — MUST. With `keepMounted`, when the item becomes unselected, `data-selected` is removed, `data-ending-style` is present while the element's exit animations run, and `data-ending-style` is removed on animations finished (detection per `architecture/transitions-and-mounting.md`); the attribute never remains permanently.
  Source: `utils/ItemIndicator.tsx:21`, `:41-51`. Test: [L4] `select/item-indicator/SelectItemIndicator.test.tsx` "settles out of its transition state after the item is deselected", "preserves the kept-mounted element across selection changes".

`ListboxSeparator` (`utils/listbox-separator/ListboxSeparator.tsx`), state object `{ orientation }`:

- **RND-DOM-53** — MUST. The separator carries `data-orientation` equal to its `orientation` parameter (`"horizontal"` by default).
  Source: `utils/listbox-separator/ListboxSeparator.tsx:16-18`. Test: [L2] `utils/listbox-separator/ListboxSeparator.test.tsx` "sets data-orientation to $orientation" (2 variants: `horizontal`, `vertical`).
- **RND-ARIA-02** — MUST. The separator carries `role="presentation"` and no `aria-orientation` attribute, regardless of `orientation`.
  Source: `utils/listbox-separator/ListboxSeparator.tsx:23`. Test: [L2] `utils/listbox-separator/ListboxSeparator.test.tsx` "has role=\"presentation\" and defaults to horizontal", "sets data-orientation to $orientation" (2 variants; asserts no `aria-orientation`), "exposes the listbox separator behavior" (3 variants).

### 4.11 Handler merging contract (EVT)

These statements describe what a consumer observes; the algorithm is ALG-02…ALG-04.

- **RND-EVT-01** — MUST. When more than one source supplies a handler of the same name, all of them run on the event (absent a veto); none is silently replaced.
  Source: `merge-props/mergeProps.ts:177-180`, `:210-239`. Test: [L2] `merge-props/mergeProps.test.ts` "merges event handlers"; `internals/useRenderElement.test.tsx` "handles enabled toggles across rerenders".
- **RND-EVT-02** — MUST. Handlers run from the highest-precedence source to the lowest: the substitute element's handler first, then the consumer's, then the part's internal handler (in `mergeProps` terms: rightmost input first, leftmost last).
  Source: `merge-props/mergeProps.ts:226-229`, `:235-236`. Test: [L2] `merge-props/mergeProps.test.ts` "merges event handlers" (their handler invoked before ours), "merges multiple event handlers" (`['1','2','3']`), "forwards all arguments for merged non-standard event handlers" (`theirs` before `ours`).
- **RND-EVT-03** — MUST. For a DOM event object, a handler that calls `event.preventBaseUIHandler()` stops every handler that would run after it in that chain (all lower-precedence handlers, including the part's internal one); handlers that already ran are unaffected.
  Source: `merge-props/mergeProps.ts:221-232`. Test: [L2] `merge-props/mergeProps.test.ts` "prevents internal handler if event.preventBaseUIHandler() is called", "prevents handlers merged after event.preventBaseUIHandler() is called" (`['0','1']`), "does not prevent internal handler if event.preventBaseUIHandler() is not called".
- **RND-EVT-04** — MUST. After `preventBaseUIHandler()` is called, `event.baseUIHandlerPrevented` reads `true` for the rest of the dispatch; before it is called the flag is absent/falsy.
  Source: `merge-props/mergeProps.ts:257-263`. Test: [L2] `merge-props/mergeProps.test.ts` "sets baseUIHandlerPrevented to true after calling preventBaseUIHandler()", "makes a lone synthetic event handler preventable".
- **RND-EVT-05** — MUST. `preventBaseUIHandler()` does not call the event's `preventDefault()` or `stopPropagation()` and does not alter native event behavior.
  Source: `merge-props/mergeProps.ts:258-260`. Test: none. Docs: `docs/src/app/(docs)/react/utils/merge-props/page.mdx` §How merging works.
- **RND-EVT-06** — MUST. `preventBaseUIHandler()` is available on the DOM event object even when only one handler exists for the event (consumer-only or part-only), whichever position it occupies, and for any DOM event name (e.g. `onContextMenu`).
  Source: `merge-props/mergeProps.ts:117-124`, `:140-147`, `:214-216`, `:241-255`. Test: [L2] `merge-props/mergeProps.test.ts` "makes a lone synthetic event handler preventable", "makes a first-position synthetic event handler preventable", "makes a lone obscure synthetic event handler preventable"; `internals/useRenderElement.test.tsx` "makes single prop objects preventable", "makes multi-prop arrays preventable when the event handler is first", "makes obscure single-prop events preventable", "makes obscure multi-prop array events preventable when the event handler is first". Docs: `CHANGELOG.md v1.4.0 "Fix \`preventBaseUIHandler\` runtime wrapping"`.
- **RND-EVT-07** — MUST. For handlers whose first argument is not a DOM event object (change callbacks such as `onValueChange(value, details)`, `onOpenChange(open, details)`), every handler in the chain runs in the order of RND-EVT-02 and no veto is possible. Port: relaxed (ADR 02).
  Source: `merge-props/mergeProps.ts:235-237`. Test: [L2] `merge-props/mergeProps.test.ts` "handles non-standard event handlers called with $name" (6 variants: boolean, number, string, object, array, function).
- **RND-EVT-08** — MUST. Every handler in a chain receives all arguments the chain was invoked with, for both DOM events and non-DOM arguments.
  Source: `merge-props/mergeProps.ts:218`, `:226-229`, `:235-236`, `:246-253`. Test: [L2] `merge-props/mergeProps.test.ts` "forwards all arguments for a lone non-standard event handler", "forwards all arguments for merged non-standard event handlers", "forwards additional arguments for synthetic event handlers". Docs: `CHANGELOG.md v1.4.1 "Fix multi-argument event handler forwarding in \`mergeProps\`"`.
- **RND-EVT-09** — MUST. The chained handler's return value is the return value of the highest-precedence (first-run) handler. Port: withdrawn (ADR 02).
  Source: `merge-props/mergeProps.ts:226,232`, `:235,237`. Test: none.
- **RND-EVT-10** — MUST. A handler attribute whose value is `undefined` on a higher-precedence source does not remove the lower-precedence handler; the existing chain is kept.
  Source: `merge-props/mergeProps.ts:190-202` (an `undefined` value still classifies as a handler), `:211-213`. Test: [L2] `merge-props/mergeProps.test.ts` "merges undefined event handlers" (`['1','3']`).
- **RND-EVT-11** — MUST. A function-form input to `mergeProps` receives the attributes accumulated from the inputs to its left (an empty object when it is first), and its return value replaces that accumulation entirely; handlers it returns are not chained with earlier ones and are not subject to the veto unless the function itself checks `event.baseUIHandlerPrevented`.
  Source: `merge-props/mergeProps.ts:117-131`. Test: [L1] `merge-props/mergeProps.test.ts` "calls the props getter with the props defined after it" (receives `{ id:'2', className }`), "calls the props getter with merged props defined after it", "calls the props getter with an empty object if no props are defined after it", "accepts the result of the props getter", "does not automatically prevent handlers that are manually called by getter handlers", "allows props getter handlers to check baseUIHandlerPrevented manually". Docs: `docs/src/app/(docs)/react/utils/merge-props/page.mdx` §Passing a function instead of an object.
- **RND-EVT-12** — MUST. The object returned by a first-position function-form input is copied before later inputs merge into it; the returned object is not mutated.
  Source: `merge-props/mergeProps.ts:118-121`. Test: [L1] `merge-props/mergeProps.test.ts` "does not mutate a reused object returned by the first props getter".
- **RND-EVT-13** — MUST. Handlers in the part's attribute list are identified by name and value alone (ALG-13): a prop named `onclick` (lowercase) or a non-function `onXxx` value is an ordinary attribute and is overridden, not chained. Port: relaxed (ADR 02).
  Source: `merge-props/mergeProps.ts:190-202`. Test: none.

## 5. Algorithms

All pseudo-code is language-neutral. `absent` means `undefined`/`null`; "truthy/falsy" follow JavaScript semantics (falsy: `false`, `0`, `NaN`, `''`, `null`, `undefined`).

- **RND-ALG-01** — MUST. The implementation reproduces the following procedure exactly (the rendering pipeline):
  Source: `internals/useRenderElement.tsx:23-49`, `:54-120`, `:122-130`. Test: [L2] whole of `internals/useRenderElement.test.tsx`.

```
renderPart(defaultTag, componentProps, params):
  render  ← componentProps.render
  enabled ← params.enabled ≠ false
  if enabled: render ← unwrapDeferred(render)                  // ALG-12; skipped while disabled
  attrs ← computeAttributes(componentProps, params, render, enabled)
  if not enabled: return NOTHING                                // renders no element
  state ← params.state ?? {}
  return materialize(defaultTag, render, attrs, state)          // ALG-08

computeAttributes(componentProps, params, render, enabled):
  if not enabled: return {}      // no class/style resolution, no state projection, no getter calls, handles detached
  state      ← params.state ?? {}
  cls        ← componentProps.className is function ? componentProps.className(state) : componentProps.className
  sty        ← componentProps.style is function     ? componentProps.style(state)     : componentProps.style
  stateAttrs ← stateToDataAttributes(state, params.stateAttributesMapping)          // ALG-07
  partAttrs  ← params.props absent ? absent
               : params.props is list ? mergePropsN(params.props)                   // ALG-02
               : mergeProps(absent, params.props)                                   // wraps lone handlers
  out ← shallowMerge(stateAttrs, partAttrs) ?? {}                                   // ALG-06; partAttrs keys win; fresh object
  out.ref ← fanOut([ out.ref, handleOf(render), …asList(params.ref) ])             // ALG-09; nulls allowed
  if cls ≠ undefined: out.className ← mergeClassNames(out.className, cls)           // ALG-05: cls placed first
  if sty ≠ undefined: out.style     ← shallowMerge(out.style, sty)                  // sty keys win
  return out
```

- **RND-ALG-02** — MUST. The implementation reproduces the following procedure exactly (`mergeProps` / `mergePropsN`):
  Source: `merge-props/mergeProps.ts:61-83`, `:99-115`, `:117-131`, `:133-148`. Test: [L1] `merge-props/mergeProps.test.ts` (all).

```
mergeProps(a, b, c, d, e):
  if a, c, d, e are all falsy: return initial(b)
  merged ← initial(a)
  for x in [b, c, d, e]: if x truthy: merged ← mergeInto(merged, x)
  return merged

mergePropsN(list):
  if list is empty: return SHARED_EMPTY          // a shared, unfrozen empty object
  merged ← initial(list[0])
  for i from 1: merged ← mergeInto(merged, list[i])
  return merged

initial(x):
  if x is function: return shallowCopy( x({}) )  // RND-EVT-12; the getter's handlers are NOT wrapped
  copy ← shallowCopy(x ?? {})
  for (k, v) in copy: if isHandler(k, v): copy[k] ← wrapPreventable(v)   // ALG-04; lone handlers get the veto API
  return copy

mergeInto(merged, x):
  if x is function: return x(merged)             // result replaces accumulation; NOT copied (later inputs mutate it)
  if x is absent:   return merged
  for (k, v) in x, including keys whose value is undefined:
    if k = 'style':        merged.style     ← shallowMerge(merged.style, v)          // ALG-06
    elif k = 'className':  merged.className ← mergeClassNames(merged.className, v)  // ALG-05
    elif isHandler(k, v):  merged[k]        ← chain(merged[k], v)                   // ALG-04
    else:                  merged[k]        ← v                                      // rightmost wins, even undefined; includes 'ref'
  return merged
```

- **RND-ALG-03** — MUST. `ref` is an ordinary attribute for `mergeProps`: the rightmost `ref` wins and no fan-out is performed. (The rendering pipeline fans out handles separately, ALG-01, and overwrites the merged `ref` with the fan-out, ALG-08.)
  Source: `merge-props/mergeProps.ts:33`, `:177-183`. Test: none. Docs: `docs/src/app/(docs)/react/utils/merge-props/page.mdx` §How merging works ("`ref` is not merged").
- **RND-ALG-04** — MUST. The implementation reproduces the following procedure exactly (handler chaining, wrapping and the veto API):
  Source: `merge-props/mergeProps.ts:210-263`, `:281-283`. Test: [L1] `merge-props/mergeProps.test.ts` "merges event handlers", "prevents handlers merged after event.preventBaseUIHandler() is called", "handles non-standard event handlers called with $name", "forwards additional arguments for synthetic event handlers".

```
isDomEvent(x): x is a non-null object that has a member named 'nativeEvent'

makePreventable(ev):
  ev.preventBaseUIHandler ← () ⇒ ev.baseUIHandlerPrevented ← true     // flag lives on the event object
  return ev

wrapPreventable(h):
  if h absent: return h
  return (…args) ⇒ { if isDomEvent(args[0]): makePreventable(args[0]); return h(…args) }

chain(ours, theirs):                 // ours = lower precedence (left), theirs = higher (right)
  if theirs absent: return ours
  if ours absent:   return wrapPreventable(theirs)
  return (…args) ⇒
    ev ← args[0]
    if isDomEvent(ev):
      makePreventable(ev)
      result ← theirs(…args)
      if not ev.baseUIHandlerPrevented: ours(…args)
      return result
    result ← theirs(…args)
    ours(…args)
    return result
```

- **RND-ALG-05** — MUST. `mergeClassNames(ours, theirs)`: if `theirs` is truthy and `ours` is truthy → `theirs + ' ' + ours`; if only `theirs` is truthy → `theirs`; otherwise → `ours` (which may be `undefined`).
  Source: `merge-props/mergeProps.ts:265-279`. Test: [L1] `merge-props/mergeProps.test.ts` "merges classNames with rightmost first" (`'external-class internal-class'`), "merges multiple classNames" (`'class-3 class-2 class-1'`), "merges classNames with undefined", "does not merge classNames if both are undefined".
- **RND-ALG-06** — MUST. `shallowMerge(a, b)`: if `a` truthy and `b` falsy → `a` (same object); if `a` falsy and `b` truthy → `b` (same object); if both truthy → a new object with all keys of `a` then all keys of `b`, `b`'s values winning for shared keys (first-seen key order kept); otherwise `undefined`.
  Source: `packages/utils/src/mergeObjects.ts:1-15`. Test: [L1] `merge-props/mergeProps.test.ts` "merges styles" (`{ color:'red', backgroundColor:'blue' }`), "merges styles with undefined", "does not merge styles if both are undefined".
- **RND-ALG-07** — MUST. The implementation reproduces the following procedure exactly (state projection):
  Source: `internals/getStateAttributesProps.ts:5-32`. Test: [L1] `internals/getStateAttributesProps.test.ts` (all).

```
stateToDataAttributes(state, mapping):
  out ← {}
  for key in enumerationOrder(state):
    v ← state[key]
    if mapping present and mapping has own property key:
      r ← mapping[key](v)
      if r ≠ null: for (name, value) in r: out[name] ← value     // later entries override
      continue
    if v === true:       out['data-' + lowercase(key)] ← ''
    elif truthy(v):      out['data-' + lowercase(key)] ← toString(v)
    // else: nothing
  return out
```

- **RND-ALG-08** — MUST. The implementation reproduces the following procedure exactly (materializing the element):
  Source: `internals/useRenderElement.tsx:157-199`, `:225-233`. Test: [L2] `internals/useRenderElement.test.tsx` "accepts render as a function that receives props and state", "accepts render as a React element and clones it with merged props", "throws error for invalid render element in development".

```
materialize(defaultTag, render, attrs, state):
  if render is truthy:
    if render is function:
      if DEV and looksLikeComponent(render): warnOnce(RENDER_LOOKS_LIKE_COMPONENT(render.name))   // ALG-14, EDGE-01
      return render(attrs, state)                       // nothing merged afterwards
    merged ← mergeProps(attrs, ownAttributesOf(render)) // substitute element's attributes win (ALG-02)
    merged.ref ← attrs.ref                              // the fan-out handle, which already includes render's own handle
    if DEV and render is not a valid element: throw INVALID_RENDER (EDGE-03)
    return cloneWithAttributes(render, merged)          // identity key of render preserved
  // default element (defaultTag is always present here)
  if defaultTag = 'button': attrs ← { type: 'button', …attrs }
  if defaultTag = 'img':    attrs ← { alt: '',        …attrs }
  return createElement(defaultTag, attrs)
```

- **RND-ALG-09** — MUST. The implementation reproduces the following procedure exactly (handle fan-out):
  Source: `packages/utils/src/useMergedRefs.ts:30-41`, `:48-54`, `:64-85`, `:87-153`. Test: [L2] `internals/useRenderElement.test.tsx` "updates merged refs and event handlers when ref shape changes across rerenders"; `use-render/useRender.test.tsx` "refs are handled as expected"; `packages/utils/src/useMergedRefs.test.tsx` (all six tests, incl. "does nothing if none of the forked branches requires a ref").

```
fanOut(handles):                                  // list; entries may be absent
  if every handle is absent: return NO_HANDLE     // nothing is attached to the element
  // one callback per distinct input list (element-wise identity); a changed list ⇒ new callback,
  // and the framework detaches the old callback (running its cleanup) before attaching the new one
  pendingCleanup ← none
  callback(instance):
    if pendingCleanup: pendingCleanup(); pendingCleanup ← none
    if instance ≠ null:
      cleanups ← []
      for i, h in handles:
        if h absent: continue
        if h is function: c ← h(instance); if c is function: cleanups[i] ← c
        else:             h.current ← instance
      pendingCleanup ← () ⇒
        for i, h in handles:
          if h absent: continue
          if h is function: cleanups[i] is function ? cleanups[i]() : h(null)
          else:             h.current ← null
  return callback
```

- **RND-ALG-10** — MUST. The implementation reproduces the following procedure exactly (rendered-id resolution and publication):
  Source: `internals/resolveRenderedId.ts:18-28`, `:37-51`. Test: [L2] `internals/resolveRenderedId.test.tsx` (all).

```
resolveRenderedId(props, fallback):
  r ← unwrapDeferred(props.render)
  if r is a valid element and ownAttributesOf(r) has own property 'id':
    return ownAttributesOf(r).id ?? ''
  return props.id ?? fallback

useRenderedId(props, generatedId, publish):
  id       ← resolveRenderedId(props, generatedId)
  override ← (id = generatedId) ? undefined : id
  attachHandle(element): publish(element ≠ null ? override : undefined)
  return (id, attachHandle)      // attachHandle joins the part's handle list
```

- **RND-ALG-11** — MUST. The implementation reproduces the following procedure exactly (id generation):
  Source: `packages/utils/src/useId.ts:42-52` (primary), `:9-31` (fallback); `internals/useBaseUiId.ts:9-11`. Test: [L1] `packages/utils/src/useId.test.tsx` (all); `packages/utils/src/useId.react17.test.tsx` (all) (L4 for the server-render citations).

```
generatedId(override, prefix):
  if a deterministic per-instance id generator exists (same token on server and client):
    token ← generator()                               // always called, so instance order stays stable
    return override ≠ undefined ? override : (prefix ? prefix + '-' + token : token)
  else (fallback, legacy environments):
    // first render: returns override, or undefined if none; after mount, generate once
    if not yet generated:
      counter ← counter + 1  (global)
      stored ← (prefix ?? 'mui') + '-' + counter        // generated even while override is present
      if override is absent: trigger one re-render (after paint) that returns stored
    return override ≠ undefined ? override : stored
  // on a server with the fallback: undefined (no id attribute emitted)

useBaseUiId(override) = generatedId(override, 'base-ui')
```

- **RND-ALG-12** — MUST. Deferred-element unwrapping: if `render` is not a deferred wrapper, return it unchanged; otherwise take the first item of the wrapper's unwrapped children; if that is a valid element return it, otherwise return the original wrapper (so it is later reported as an invalid substitute rather than silently falling back to the default element).
  Source: `internals/useRenderElement.tsx:146-155`. Test: [L2] `internals/useRenderElement.test.tsx` "handles lazy elements", "reports a wrapper that unwraps to %s as an invalid render element" (4 variants).
- **RND-ALG-13** — MUST. `isHandler(name, value)`: `name` has at least three characters, `name[0] = 'o'`, `name[1] = 'n'`, `name[2]` is an uppercase ASCII letter `A`–`Z`, and `value` is a function or `undefined`. Port: relaxed (ADR 02).
  Source: `merge-props/mergeProps.ts:190-202`. Test: [L1] `merge-props/mergeProps.test.ts` "merges undefined event handlers", "handles non-standard event handlers called with $name".
- **RND-ALG-14** — MUST. `looksLikeComponent(fn)`: let `n` be the function's name; true iff `n` is non-empty, matches `^[A-Z][A-Za-z0-9$]*$`, and contains at least one lowercase ASCII letter. (`UIInput` → true; `DEFAULT_RENDER` → false (underscore, no lowercase); `DropdownMenuExample.useCallback[renderSearchInput]` → false (dot/brackets); `renderFn` → false.)
  Source: `internals/useRenderElement.tsx:137-138`, `:201-213`. Test: [L1] `internals/useRenderElement.test.tsx` "warns when render is passed a function with an uppercase name", "warns when render is passed a function with an uppercase acronym prefix", "does not warn when render is passed a lowercase callback", "does not warn when render is passed a screaming snake case callback", "does not warn when render is passed a callback with an inferred useCallback name".
- **RND-ALG-15** — MUST. `resolveRef(x)`: if `x` is absent return `x`; if `x` has a `current` member return `x.current`; otherwise return `x`. Used wherever a parameter accepts "an element or a handle to one" (initial/return focus targets in `architecture/floating-and-positioning.md`, animated element in `architecture/transitions-and-mounting.md`).
  Source: `utils/resolveRef.ts:5-13`; consumers `internals/useAnimationsFinished.ts:68`, `floating-ui-react/components/FloatingFocusManager.tsx:471-472,647-648,723,881,1023,1047`. Test: none.

## 6. Configuration surface used by components

### 6.1 Rendering pipeline (`useRenderElement(element, componentProps, params)`)

| Option | Type | Default | Meaning |
|---|---|---|---|
| `element` | tag name | — (required; every part passes one) | Default element tag (RND-STR-02). `internals/useRenderElement.tsx:29` |
| `componentProps.className` | string \| `(state) → string \| undefined` | — | Class parameter (RND-DOM-14…17). `:285` |
| `componentProps.style` | object \| `(state) → object \| undefined` | — | Style parameter (RND-DOM-16, 18). `:294` |
| `componentProps.render` | element \| `(attrs, state) → element` | — | Element substitution (RND-STR-02…04). `:289` |
| `params.enabled` | boolean | `true` | Conditional rendering switch (RND-DOM-25, 26). `:250` |
| `params.ref` | handle \| (handle \| undefined)[] | — | Part-internal and consumer handles joining the fan-out (RND-DOM-20…24). `:258` |
| `params.state` | object | `{}` | State object (§4.3, §4.4). `:262` |
| `params.props` | attribute object \| list of (attribute object \| undefined \| function-form input) | — | Part-supplied then consumer-supplied attributes, merged left to right by ALG-02. Convention: 48 parts put the consumer's attributes last; 5 put them first and follow with an internal entry that must win (`children` of viewport parts, the button props getter in `button/Button.tsx:40`). `:266-273` |
| `params.stateAttributesMapping` | `StateAttributesMapping<State>` | — | Custom projection (RND-DOM-06…09). `:277` |
| `params.propGetter` | function | — | Deprecated; ignored (RND-EDGE-07). `:254` |

Component documents cite this table by stating, per part: default element, state fields, mapping used, and the part-supplied attributes (role, ARIA, handlers).

### 6.2 Shared custom mappings

| Mapping | Fields | Emits | Specified in |
|---|---|---|---|
| `transitionStatusMapping` (`internals/stateAttributesMapping.ts:10-20`) | `transitionStatus` | `data-starting-style`, `data-ending-style` (RND-DOM-11) | here; status semantics in `architecture/transitions-and-mounting.md`. Used by 21 parts. |
| `collapsibleOpenStateMapping` (`utils/collapsibleOpenStateMapping.ts:26-35`) | `open` | `data-open` / `data-closed` (RND-DOM-12) | here; consumers `collapsible/root/stateAttributesMapping.ts`, `accordion/item/stateAttributesMapping.ts` |
| `triggerOpenStateMapping` (`utils/collapsibleOpenStateMapping.ts:13-24`) | `open` | `data-panel-open` (RND-DOM-13) | here; consumers `collapsible/trigger/CollapsibleTrigger.tsx:13`, `accordion/trigger/AccordionTrigger.tsx:16` |
| `triggerOpenStateMapping`, `pressableTriggerOpenStateMapping`, `popupStateMapping`, `popupTransitionStateMapping` (`utils/popupStateMapping.ts`) | `open`, `anchorHidden`, `transitionStatus` | `data-popup-open`, `data-pressed`, `data-open`/`data-closed`, `data-anchor-hidden`, transition attributes | `architecture/popup-store-and-handles.md` |
| `fieldValidityMapping` (`internals/field-constants/constants.ts:34-44`) | `valid: boolean \| null` | `data-valid` / `data-invalid` / nothing for `null` | `architecture/field-and-form-infrastructure.md` (tested in `internals/stateAttributesMapping.test.ts` "emits the validity data attributes", "emits nothing while validity is unknown") |

Forty further component-specific mappings exist (`grep -rl "StateAttributesMapping<" packages/react/src`); each component document specifies its own.

### 6.3 Identifier helpers

| Helper | Parameters | Returns | Used by |
|---|---|---|---|
| `useBaseUiId(idOverride?)` | consumer id or `undefined` | `idOverride`, else `base-ui-<token>` (ALG-11) | 46 source files; every part that emits an `id` or IDREF |
| `resolveRenderedId(props, fallbackId)` | `{ id?, render? }`, generated id | the id that will land on the element (ALG-10) | parts that must know their final id |
| `useRenderedId(props, defaultId, setId)` | as above plus a publish callback | `[id, attachHandle]` (RND-DOM-33) | `menu/popup/MenuPopup.tsx:72-76` (publishes the popup id to the popup store) |

### 6.4 Shared parts

| Part | Default element | State | Part-supplied attributes | Mapping | Wrappers |
|---|---|---|---|---|---|
| `ItemIndicator` | `span` | `{ selected, transitionStatus }` | `aria-hidden="true"`, `children: '✔️'` (both overridable, consumer attributes come last) | `transitionStatusMapping` | `Select.ItemIndicator`, `Combobox.ItemIndicator` (mount gating per RND-ANI-01) |
| `ListboxSeparator` | `div` | `{ orientation }` | `role="presentation"` | default projection only | `Autocomplete.Separator`, `Combobox.Separator`, `Select.Separator` |

### 6.5 Style hook

| Name | Value |
|---|---|
| `styleDisableScrollbar.className` | `base-ui-disable-scrollbar` |
| `styleDisableScrollbar.getElement(nonce?)` | the de-duplicated style element of RND-DOM-35 |

## 7. Edge cases and dev warnings

- **RND-EDGE-01** — MUST. In development builds, when `render` is a function whose name satisfies ALG-14, a warning is logged once per distinct message (i.e. once per distinct function name), with the text (single line, segments separated by one space): `Base UI: The \`render\` prop received a function named \`<name>\` that starts with an uppercase letter. This usually means a React component was passed directly as \`render={Component}\`. Base UI calls \`render\` as a plain function, which can break the Rules of Hooks during reconciliation. If this is an intentional render callback, rename it to start with a lowercase letter. Use \`render={<Component />}\` or \`render={(props) => <Component {...props} />}\` instead. https://base-ui.com/r/invalid-render-prop`. The function is still called and rendering proceeds.
  Source: `internals/useRenderElement.tsx:165-167`, `:201-223`; `packages/utils/src/warn.ts:3`; `packages/utils/src/createLogOnce.ts:7-23`. Test: [L2] `internals/useRenderElement.test.tsx` "warns when render is passed a function with an uppercase name", "warns when render is passed a function with an uppercase acronym prefix".
- **RND-EDGE-02** — MUST. No such warning is logged for a substitute callback whose name does not satisfy ALG-14 (lowercase, SCREAMING_SNAKE, dotted/bracketed inferred names, anonymous) or for a substitute element whose type is an uppercase-named component.
  Source: `internals/useRenderElement.tsx:201-213`. Test: [L2] `internals/useRenderElement.test.tsx` "does not warn when render is passed a lowercase callback", "does not warn when render is passed a screaming snake case callback", "does not warn when render is passed a callback with an inferred useCallback name", "does not warn when render is passed as a React element".
- **RND-EDGE-03** — MUST. When `render` is truthy, not a function, and not a valid element, rendering fails with an error. In development builds the error message is exactly (three lines joined by newlines): `Base UI: The \`render\` prop was provided an invalid React element as \`React.isValidElement(render)\` is \`false\`.` / `A valid React element must be provided to the \`render\` prop because it is cloned with props to replace the default element.` / `https://base-ui.com/r/invalid-render-prop`. In production builds the failure comes from the underlying clone operation and its message is unspecified.
  Source: `internals/useRenderElement.tsx:175-193`. Test: [L2] `internals/useRenderElement.test.tsx` "throws error for invalid render element in development".
- **RND-EDGE-04** — MUST. A deferred wrapper (RND-DOM-34) that unwraps to `null`, `false`, `0` or `''` is reported as an invalid substitute (RND-EDGE-03), not treated as "no substitution". Port: withdrawn (ADR 02).
  Source: `internals/useRenderElement.tsx:151-154`. Test: [L2] `internals/useRenderElement.test.tsx` "reports a wrapper that unwraps to %s as an invalid render element" (4 variants: `null`, `false`, `0`, `''`).
- **RND-EDGE-05** — MUST. While `enabled` is `false`, a deferred wrapper is not unwrapped, so a wrapper whose value is still pending does not suspend or block rendering; nothing is rendered. Port: withdrawn (ADR 02).
  Source: `internals/useRenderElement.tsx:34-37`. Test: [L2] `internals/useRenderElement.test.tsx` "does not unwrap a pending element when disabled".
- **RND-EDGE-06** — MUST. A part that supplies no attributes of its own and has an empty state still applies a consumer `className` or `style` (the merge must not fail on a shared immutable empty object).
  Source: `internals/useRenderElement.tsx:83-88`; `packages/utils/src/empty.ts:8`. Test: [L2] `internals/useRenderElement.test.tsx` "does not throw when className is provided with minimal props", "does not throw when style is provided with minimal props".
- **RND-EDGE-07** — MUST. The pipeline's deprecated `propGetter` option has no effect; no part passes it.
  Source: `internals/useRenderElement.tsx:251-254` (declared, never read). Test: none.
- **RND-EDGE-08** — MUST. All development-only diagnostics of this document are suppressed entirely in production builds, and every distinct message is logged at most once per page lifetime.
  Source: `packages/utils/src/createLogOnce.ts:2-4`, `:9-21`. Test: see UTL-EDGE-11 (`packages/utils/src/createLogOnce.test.ts`); also relied on by the single-call assertions of RND-EDGE-01's tests under development double rendering.
- **RND-EDGE-09** — SHOULD. When no deterministic server/client id generator is available (legacy fallback of ALG-11), the server emits no `id` attribute, the first client render also has none, and the generated id (`mui-<n>` with default prefix, `base-ui-<n>` via `useBaseUiId`) appears after paint; a part mounted with a consumer id does not render again just to generate the unused fallback (render count observed through bUnit, L2).
  Source: `packages/utils/src/useId.ts:9-31`. Test: [L2] `packages/utils/src/useId.react17.test.tsx` "returns undefined on the server", "matches the server markup on the first client render", "generates an id if one isn't provided", "generates an id with the provided prefix", "does not rerender after mounting with a provided id" (L4 for the server-markup and first-client-render clauses).

## 8. React-specific notes and porting considerations

Non-normative. The port's decision record for this whole document is `decisions/02-render-pipeline-and-attribute-merging.md`.

* **Hook-order stability.** `useRenderElementProps` is always executed, even when `enabled` is `false`, and calls the handle fan-out hook with `(null, null)` in that case (`useRenderElement.tsx:97-99`) purely to keep React's hook order stable. The port has no equivalent constraint; RND-DOM-25 is the observable contract. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 4).
* **`ref` as a prop (React 19) vs `forwardRef`.** In React 19 a consumer's `ref` arrives inside the props object; in React ≤18 it arrives through `forwardRef`. All parts use `forwardRef` and pass the forwarded handle through `params.ref`. In `useRender`, a consumer's `ref` may instead sit inside `props` and reaches the fan-out as `out.ref` (ALG-01). A port should model "consumer DOM handle" as one input of the fan-out regardless of how it is transported. `getReactElementRef` (`packages/utils/src/getReactElementRef.ts:15`) reads `element.props.ref` on React 19 and `element.ref` earlier; the port needs one way to read a substitute element's own handle. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 1, frame rewriting preserves the consumer's capture frame) and §Requirements relaxed or withdrawn for the port (RND-DOM-20..24).
* **`cloneElement` semantics.** Cloning preserves the substitute element's identity key and type and replaces its attributes with the merged set (ALG-08). The DOM handle in the merged set is the fan-out callback, which already includes the substitute's own handle, so no handle is lost. The clone does not preserve the substitute element's original `ref` separately. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 1).
* **`key` in merged attributes.** `renderTag` passes `key={props.key}` for `button` and `img` (`useRenderElement.tsx:227,230`) so that a `key` present in the merged attributes becomes the element key. No part in v1.9.0 passes a `key` through the pipeline; a port can ignore this.
* **Deferred (lazy) elements.** React Server Components hand a client part a `React.lazy` wrapper in place of an element created on the server; `React.Children.toArray` unwraps it (`useRenderElement.tsx:132-155`). A Blazor/Rust port without an equivalent concept may treat RND-DOM-34/EDGE-04/EDGE-05 as satisfied vacuously, but must keep RND-DOM-32's precedence for ordinary substitute elements. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-34, RND-EDGE-04/05 vacuous).
* **Synthetic events.** "DOM event object" is detected by the presence of a `nativeEvent` member (`mergeProps.ts:281-283`), which is how React's synthetic events differ from Base UI's change-callback arguments. A port should pick an equivalent discriminator (for example "argument is an instance of the framework's event-args base type") and install `preventBaseUIHandler` on that object; the veto flag must live on the event instance so that it is visible to later handlers of the same dispatch but not to a different dispatch. Port: the veto mechanism of RND-EVT-02..04 is settled in decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 3: `PreventBaseUIHandler()` extension on `EventArgs`, part-invoked chain).
* **Mutation of shared inputs.** A function-form input in non-first position returns an object that later inputs mutate in place (ALG-02, `mergeInto`). Callers in the library always return fresh objects. A port may copy defensively; the observable contract (RND-EVT-11, RND-EVT-12) is unchanged.
* **Server-safe ids.** With React ≥18, `React.useId()` produces a token that is identical on server and client because it is derived from tree position, not a counter; Base UI prefixes it with `base-ui-`. Blazor's prerender + interactive takeover has the same requirement (RND-DOM-29); a counter-based scheme only works if the counter sequence is guaranteed identical in both passes. The React 17 fallback (ALG-11 "else" branch, RND-EDGE-09) exists only because React 17 has no such generator. Port: RND-DOM-29 prerender/interactive id stability is settled in decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5) and decisions/01-hosting-model-and-timing.md §Consequences.
* **Refs are skipped on the server.** The fan-out is only built when a `document` global exists (`useRenderElement.tsx:96`); server output never contains handles. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24).
* **Style element hoisting.** `styleDisableScrollbar.getElement` relies on React 19's `<style href precedence>` hoisting and de-duplication (`utils/styles.tsx:7`). A port must de-duplicate the style element itself (RND-DOM-35) or ship the rule in a stylesheet; the class name must remain `base-ui-disable-scrollbar`.
* **Frozen empty object.** `EMPTY_OBJECT` is frozen (`packages/utils/src/empty.ts:8`); the pipeline guarantees a fresh mutable output object (`useRenderElement.tsx:83-88`) before assigning `ref`, `className`, `style`. Only RND-EDGE-06 is observable.
* **StrictMode double rendering and warn-once.** Development double-invocation of render functions means the substitute callback may be called twice per render in React dev mode; tests count console warnings, which are de-duplicated (RND-EDGE-08). A port with no double rendering simply calls the callback once (RND-STR-04).
* **Unprefixed id exceptions.** The bare-token ids of RND-DOM-27 come from components calling the unprefixed generator directly. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 5) — the unprefixed exceptions are collapsed to the prefixed `base-ui-` form.
* **State projection.** `getStateAttributesProps` iterates the state object with `for…in` at render time (RND-ALG-07, §10 Q4). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 4) and §Amendments (amended by decisions/05-state-store-and-event-details.md: projection by source generator, declaration order).
* **Conformance suite.** `describeConformance` runs under React Testing Library in jsdom. Port: the conformance obligations RND-DOM-36..49 run at layer L2 (bUnit) per decisions/06-testing-harness.md §Decision (item 1).
* **Requirements relaxed or withdrawn for the port.** RND-DOM-19/32 (explicit `undefined`), RND-DOM-20..24 (DOM handles), RND-DOM-34/EDGE-04/05 (deferred elements), RND-DOM-41, RND-EVT-07/09/13, RND-ALG-13 and RND-EDGE-01/02/03 (kept with adapted text) have port dispositions; the requirements above still describe Base UI. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port.
* **`useRender` passes its parameter object as the component-props argument** (`useRender.ts:18`), so `className`/`style` keys present on that object would be resolved as class/style parameters even though they are not declared (§10, Q1).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| RND-STR-01 | MUST | Exactly one host element per part | 4.1 |
| RND-STR-02 | MUST | Falsy `render` → default element | 4.1 |
| RND-STR-03 | MUST | Substitute element rendered with merged attributes | 4.1 |
| RND-STR-04 | MUST | Substitute callback called with (attributes, state), no post-merge | 4.1 |
| RND-STR-05 | MUST | Default `button` gets `type="button"` | 4.1 |
| RND-STR-06 | MUST | Default `img` gets `alt=""` | 4.1 |
| RND-STR-07 | MUST | Substitution nests to any depth | 4.1 |
| RND-STR-08 | MUST | `useRender` default tag `div` / `defaultTagName` | 4.1 |
| RND-STR-09 | MUST | `render` beats `defaultTagName` | 4.1 |
| RND-STR-10 | MUST | `ItemIndicator` is a `span`, backs Select/Combobox indicators | 4.1 |
| RND-STR-11 | MUST | `ListboxSeparator` is a `div`, backs three Separator parts | 4.1 |
| RND-API-01 | MUST | `className` string or function of state | 4.2 |
| RND-API-02 | MUST | `style` object or function of state | 4.2 |
| RND-API-03 | MUST | `render` element or callback | 4.2 |
| RND-API-04 | MUST | Native attributes accepted/forwarded; `color`/`defaultValue`/`defaultChecked` excluded | 4.2 |
| RND-API-05 | MUST | Consumer DOM handle accepted | 4.2 |
| RND-API-06 | MUST | Handlers receive `preventBaseUIHandler`/`baseUIHandlerPrevented` | 4.2 |
| RND-API-07 | MUST | `useRender` parameters and defaults | 4.2 |
| RND-API-08 | MUST | `ref` accepts handle or list | 4.2 |
| RND-API-09 | MUST | `mergeProps` takes 2–5 inputs | 4.2 |
| RND-API-10 | MUST | `mergePropsN` takes an array | 4.2 |
| RND-API-11 | MUST | `makeEventPreventable`, `mergeClassNames` exported | 4.2 |
| RND-API-12 | MUST | `ItemIndicator` parameters | 4.2 |
| RND-API-13 | MUST | `ListboxSeparator` `orientation` default horizontal | 4.2 |
| RND-STA-01 | MUST | Same state values to class/style/callback/attributes | 4.3 |
| RND-STA-02 | MUST | Missing state behaves as `{}` | 4.3 |
| RND-STA-03 | MUST | `useRender` state projected identically | 4.3 |
| RND-DOM-01 | MUST | `true` → `data-x=""` | 4.4 |
| RND-DOM-02 | MUST | `false` → absent | 4.4 |
| RND-DOM-03 | MUST | Truthy → stringified value | 4.4 |
| RND-DOM-04 | MUST | Other falsy → absent | 4.4 |
| RND-DOM-05 | MUST | Name = `data-` + lowercased field | 4.4 |
| RND-DOM-06 | MUST | Custom mapping replaces default projection | 4.4 |
| RND-DOM-07 | MUST | Mapping `null` → nothing | 4.4 |
| RND-DOM-08 | MUST | Mapping sees only the field value | 4.4 |
| RND-DOM-09 | MUST | Later mapping result wins on name clash | 4.4 |
| RND-DOM-10 | MUST | Supplied attribute overrides state attribute | 4.4 |
| RND-DOM-11 | MUST | Transition mapping attributes | 4.4 |
| RND-DOM-12 | MUST | Collapsible panel `data-open`/`data-closed` | 4.4 |
| RND-DOM-13 | MUST | Collapsible trigger `data-panel-open` | 4.4 |
| RND-DOM-14 | MUST | Class order on default element | 4.5 |
| RND-DOM-15 | MUST | Class order with substitute element | 4.5 |
| RND-DOM-16 | MUST | Style merge precedence and key order | 4.5 |
| RND-DOM-17 | MUST | Class function `undefined`/`''` adds nothing | 4.5 |
| RND-DOM-18 | MUST | Style function `undefined` adds nothing | 4.5 |
| RND-DOM-19 | MUST | Non-special attributes: later source wins, even `undefined` | 4.5 |
| RND-DOM-20 | MUST | Consumer handle gets element / `null` | 4.6 |
| RND-DOM-21 | MUST | Substitute element's handle also gets element | 4.6 |
| RND-DOM-22 | MUST | Internal handles set/cleared together | 4.6 |
| RND-DOM-23 | MUST | Handle set change: removed cleared, added set | 4.6 |
| RND-DOM-24 | MUST | Callback handle cleanup semantics | 4.6 |
| RND-DOM-25 | MUST | `enabled=false` renders nothing, no work | 4.6 |
| RND-DOM-26 | MUST | Toggling `enabled` mounts/unmounts consistently | 4.6 |
| RND-DOM-27 | MUST | Generated ids `base-ui-<token>` (unprefixed exceptions: Toast labels, NavigationMenu.Viewport, portal node, popup `floatingId`) | 4.7 |
| RND-DOM-28 | MUST | Consumer id (incl. `''`) used verbatim | 4.7 |
| RND-DOM-29 | MUST | Generated id stable and takeover-safe | 4.7 |
| RND-DOM-30 | MUST | Removing consumer id falls back without gap | 4.7 |
| RND-DOM-31 | MUST | Generated ids usable in IDREF lists/suffixes | 4.7 |
| RND-DOM-32 | MUST | Rendered-id precedence (substitute id > prop > generated) | 4.7 |
| RND-DOM-33 | MUST | Rendered-id publication rules | 4.7 |
| RND-DOM-34 | MUST | Deferred element unwrapped before merging | 4.8 |
| RND-DOM-35 | MUST | Disable-scrollbar class and style element | 4.8 |
| RND-DOM-36 | MUST | Conformance: custom attributes on default element | 4.9 |
| RND-DOM-37 | MUST | Conformance: custom attributes via callback | 4.9 |
| RND-DOM-38 | MUST | Conformance: custom attributes on substitute element | 4.9 |
| RND-DOM-39 | MUST | Conformance: `style` parameter applied | 4.9 |
| RND-DOM-40 | MUST | Conformance: substitute element style applied | 4.9 |
| RND-DOM-41 | MUST | Conformance: handle receives documented element class | 4.9 |
| RND-DOM-42 | MUST | Conformance: callback with wrapper root | 4.9 |
| RND-DOM-43 | MUST | Conformance: custom-component substitute element | 4.9 |
| RND-DOM-44 | MUST | Conformance: handle passes through callback | 4.9 |
| RND-DOM-45 | MUST | Conformance: both handles get same element | 4.9 |
| RND-DOM-46 | MUST | Conformance: class string + substitute class | 4.9 |
| RND-DOM-47 | MUST | Conformance: class function + substitute class | 4.9 |
| RND-DOM-48 | MUST | Conformance: class string on default element | 4.9 |
| RND-DOM-49 | MUST | Conformance: button parts re-run DOM-36…40, 42…47 with `div` + `nativeButton=false` | 4.9 |
| RND-DOM-50 | MUST | `ItemIndicator` default content `✔️` | 4.10 |
| RND-DOM-51 | MUST | `ItemIndicator` `data-selected`; no `selected` attribute | 4.10 |
| RND-DOM-52 | MUST | `ItemIndicator` transition attributes | 4.10 |
| RND-DOM-53 | MUST | `ListboxSeparator` `data-orientation` | 4.10 |
| RND-DOM-54 | MUST | Only Menu Popup / filterable List resolve the rendered id; other referenced parts register the computed id | 4.7 |
| RND-ARIA-01 | MUST | `ItemIndicator` `aria-hidden="true"` | 4.10 |
| RND-ARIA-02 | MUST | `ListboxSeparator` `role="presentation"`, no `aria-orientation` | 4.10 |
| RND-ANI-01 | MUST | Indicator mounted only while selected unless `keepMounted` | 4.10 |
| RND-ANI-02 | MUST | Kept-mounted indicator element identity preserved | 4.10 |
| RND-ANI-03 | MUST | Kept-mounted indicator clears `data-ending-style` after exit | 4.10 |
| RND-EVT-01 | MUST | Same-name handlers all run | 4.11 |
| RND-EVT-02 | MUST | Run order: substitute → consumer → internal | 4.11 |
| RND-EVT-03 | MUST | Veto skips later handlers in chain | 4.11 |
| RND-EVT-04 | MUST | `baseUIHandlerPrevented` reads `true` after veto | 4.11 |
| RND-EVT-05 | MUST | Veto does not preventDefault/stopPropagation | 4.11 |
| RND-EVT-06 | MUST | Veto available on lone handlers, any position/event | 4.11 |
| RND-EVT-07 | MUST | Non-DOM-argument handlers: all run, no veto | 4.11 |
| RND-EVT-08 | MUST | All arguments forwarded | 4.11 |
| RND-EVT-09 | MUST | Chain returns first-run handler's result | 4.11 |
| RND-EVT-10 | MUST | `undefined` handler keeps existing chain | 4.11 |
| RND-EVT-11 | MUST | Function-form input replaces accumulation, no auto-chain | 4.11 |
| RND-EVT-12 | MUST | First-position function-form result copied | 4.11 |
| RND-EVT-13 | MUST | Handler identification by name/value only | 4.11 |
| RND-ALG-01 | MUST | Rendering pipeline pseudo-code | 5 |
| RND-ALG-02 | MUST | `mergeProps`/`mergePropsN` pseudo-code | 5 |
| RND-ALG-03 | MUST | `mergeProps` does not fan out `ref` | 5 |
| RND-ALG-04 | MUST | Handler chain / wrap / veto pseudo-code | 5 |
| RND-ALG-05 | MUST | `mergeClassNames` | 5 |
| RND-ALG-06 | MUST | `shallowMerge` (`mergeObjects`) | 5 |
| RND-ALG-07 | MUST | State projection pseudo-code | 5 |
| RND-ALG-08 | MUST | Materialize (substitute vs default) pseudo-code | 5 |
| RND-ALG-09 | MUST | Handle fan-out pseudo-code | 5 |
| RND-ALG-10 | MUST | Rendered-id resolution/publication pseudo-code | 5 |
| RND-ALG-11 | MUST | Id generation pseudo-code | 5 |
| RND-ALG-12 | MUST | Deferred-element unwrapping | 5 |
| RND-ALG-13 | MUST | `isHandler` predicate | 5 |
| RND-ALG-14 | MUST | `looksLikeComponent` predicate | 5 |
| RND-ALG-15 | MUST | `resolveRef` | 5 |
| RND-EDGE-01 | MUST | Dev warning for component-looking callback (verbatim) | 7 |
| RND-EDGE-02 | MUST | No warning for other names / elements | 7 |
| RND-EDGE-03 | MUST | Invalid substitute throws (dev message verbatim) | 7 |
| RND-EDGE-04 | MUST | Deferred wrapper to non-element is invalid | 7 |
| RND-EDGE-05 | MUST | Pending deferred element not forced while disabled | 7 |
| RND-EDGE-06 | MUST | Minimal part still applies className/style | 7 |
| RND-EDGE-07 | MUST | `propGetter` ignored | 7 |
| RND-EDGE-08 | MUST | Diagnostics dev-only and once per message | 7 |
| RND-EDGE-09 | SHOULD | Legacy id fallback semantics | 7 |

Counts: STR 11, API 13, STA 3, DOM 53, ARIA 2, ANI 3, EVT 13, ALG 15, EDGE 9 — 122 requirements.

## 10. Open questions

1. **`useRender` component-props argument.** `use-render/useRender.ts:18` passes the whole `params` object as both the component-props and the params argument of the pipeline, so `className`/`style` keys on `params` would be resolved as class/style parameters even though `UseRenderParameters` (`useRender.ts:41-83`) does not declare them. Undocumented; specified here only as the declared surface (RND-API-07). Confirm whether a port should accept them.
   Resolved for the port: decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 4) gives the public `RenderElement<TState>` exactly the §3.2 parameters (`Render`, `State`, `StateAttributesMapping`, `Props`, `Enabled`, `DefaultTagName`); no undeclared class/style channel.
2. **Function-form input in non-first position is used by reference.** `merge-props/mergeProps.ts:126-131` returns the getter's object as the new accumulation without copying, so subsequent inputs mutate it (contrast `:117-124`, which copies for the first position, tested by "does not mutate a reused object returned by the first props getter"). Intentional or oversight? Specified as-is in ALG-02 with a porting note in §8.
3. **Misleading test titles.** `merge-props/mergeProps.test.ts:405` "merges internal props so that the ones defined first override the ones defined later" asserts the *later* input wins (`title: 'internal title 1'`), and `:440` "calls the props getter with the props defined after it" asserts the getter receives the inputs defined *before* it. Requirements follow the assertions (RND-DOM-19, RND-EVT-11); a translated suite should keep the titles for traceability.
   Resolved: decisions/06-testing-harness.md §Decision (item 1, rules) — every translated test keeps Base UI's title.
4. **State enumeration and object values.** `internals/getStateAttributesProps.ts:12` enumerates with `for…in` (inherited enumerable properties would be included) and `:27` stringifies any truthy value (an object would emit `"[object Object]"`). No part relies on either; a port may restrict projection to own properties of primitive type. Flagged so the deviation is deliberate.
   Resolved: decisions/02-render-pipeline-and-attribute-merging.md §Decision (item 4) restricts projection to own properties of primitive type, amended by decisions/05-state-store-and-event-details.md to a source generator over declared properties in declaration order.
5. **`element` parameter typed as possibly `undefined`.** `internals/useRenderElement.tsx:26,29` allow `undefined` as the default tag ("always provided when there is no `render` prop", `:197`), but no part passes `undefined` (grep over `src/`). Treated as type-only; RND-STR-02 requires a default element.
6. **Name collision `triggerOpenStateMapping`.** Two exports share the name: `utils/collapsibleOpenStateMapping.ts:13` (emits `data-panel-open`) and `utils/popupStateMapping.ts:30` (emits `data-popup-open`). Component documents must cite the file to disambiguate (RND-DOM-13 note).
7. **`describeConformance` harness error.** `test/conformanceTests/utils.ts:1-6` throws `missing "<field>" in options` when a suite is misconfigured; this is a test-harness contract, not library behavior, and is not given a requirement. Confirm the translated suite should keep it.
8. **ARIA/`id` on element substitution.** RND-DOM-32 states callbacks are opaque and "expected to use the id passed to them" (`internals/resolveRenderedId.ts:12-14`). If a callback drops the `id`, IDREFs computed by the owner (e.g. a trigger's `aria-controls`) dangle. The same happens with a substitute *element* carrying its own `id` on every part except `Menu.Popup` and the filterable `Menu.List`, because only those two resolve the rendered id (RND-DOM-54); Dialog/Popover Title/Description, Select/Combobox labels and group labels register the computed (`id`-prop, root-derived or `useBaseUiId`) value. This is documented library behavior, not a defect, but component documents for popups should repeat the expectation.
   Resolved for Dialog (DLG-EDGE-18), Select (SEL-EDGE-21) and Combobox (CMB-EDGE-13); Menu covered by MNU-EDGE-16.
