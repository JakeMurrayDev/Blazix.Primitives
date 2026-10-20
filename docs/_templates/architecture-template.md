# <Mechanism name>

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/<paths>` |
| Tests | `<paths>` |
| Prefix | `XXX` |
| Status | draft |
| Used by | <list of component docs that depend on this> |

<!--
Authoring notes (delete in finished doc): follow docs/conventions.md; keep headings and numbering; "Not applicable — <reason>" for empty sections.
-->

## 1. Purpose

What problem this mechanism solves and which components use it. Why a port cannot skip it.

## 2. Concepts and terminology

Define every term used below in framework-neutral language. Include a glossary table mapping Base UI/React names to the neutral names used in the component docs.

## 3. Public surface

If any part of this mechanism is exported to consumers (e.g. `mergeProps`, `useRender`, `DirectionProvider`, positioner props shared by many components), document it with the same Props / Types tables as the component template. Also document here any **public props whose semantics are defined by this mechanism** even though the mechanism itself is internal (e.g. `nativeButton`, `focusableWhenDisabled`, `keepMounted`, `side`/`align`), so component docs can cite one ID instead of re-describing them. Otherwise "Not applicable".

## 4. Behavioral requirements

Organized by AREA (STR, API, STA, DOM, ARIA, KEY, PTR, FOC, DIS, EVT, FRM, ANI, POS, A11Y, I18N, EDGE). Requirement bullets in the fixed format. Large areas may be split with bold run-in sub-headings or `####` headings; the ID sequence stays one per AREA. Requirements derived from a shared conformance suite may be grouped in one sub-section regardless of AREA. An ALG statement may begin with an italic procedure name (*Grid index resolution.*) so the abbreviated index stays informative.

- **XXX-STA-01** — MUST. …
  Source: `…`. Test: `…`.

## 5. Algorithms

Language-neutral pseudo-code for logic that must be reproduced exactly (e.g. list navigation index resolution, collision handling, typeahead matching, animation-finished detection, event-details cancel semantics). Each algorithm gets an ALG requirement and cites the source lines it was transcribed from.

- **XXX-ALG-01** — MUST. The implementation reproduces the following procedure exactly: …
  Source: `…:lines`. Test: `…`.

## 6. Configuration surface used by components

Table of the options/parameters components pass into this mechanism, with meaning and default. This is what component docs reference in their §11.

| Option | Type | Default | Meaning |
|---|---|---|---|

## 7. Edge cases and dev warnings

- **XXX-EDGE-01** — MUST. …

## 8. React-specific notes and porting considerations

Non-normative.

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|

## 10. Open questions
