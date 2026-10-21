# Blazix.Primitives — entry file

Blazor port of MUI Base UI v1.9.0, built documentation-first. This file routes; it holds no content. The root `CONTEXT.md` is the project glossary; a `CONTEXT.md` inside a working folder is that folder's contract (inputs, job, outputs, human check).

## Where things live

| Need | Go to |
|---|---|
| What the port must do (requirements, one document per component or mechanism) | `docs/README.md` (status table, reading order) → `docs/architecture/*.md`, `docs/components/*.md` |
| How to read or write a requirement, IDs, citations, status values, amendment rule | `docs/conventions.md` (§3 IDs, §3.4 implementation marks, §3.5 amendments, §6 statuses) |
| How Base UI maps onto Blazor (settled decisions) | `docs/decisions/01..16-*.md`; index in `docs/README.md` §Decisions |
| Vocabulary the port adds on top of Base UI | `CONTEXT.md` |
| The pinned Base UI source and vendored packages (ignored by git) | `reference/base-ui/` (tag `v1.9.0`), `reference/vendor/`; re-create per `docs/conventions.md` §1 |
| Lint of the documents | `python3 docs/_tools/check_docs.py` from the root; `docs/_tools/test_titles.py`, `apply_layers.py` |
| QA history and open gaps | `docs/_qa-notes.md` |
| Milestones and what is next | `docs/decisions/12-implementation-order-and-milestones.md` |
| Verification items and their schedule | `docs/decisions/14-verification-plan.md` (one GitHub issue each, label `verification`) |
| Upstream Base UI releases | `docs/upstream/` (triage notes, policy in decision record 15) |

## Where code goes (from milestone M0, decision record 09)

| Path | Role |
|---|---|
| `src/Blazix.Primitives/<Family>/` | the parts of one Base UI family; contract = `docs/components/<family>.md`; browser specs = `tests/browser/<family>.spec.ts` |
| `src/Blazix.Primitives/{Rendering,Store,Events,Interop}/` | pipeline, store, event details, twins |
| `src/Blazix.Primitives/Utilities/` | cross-family helpers; contract = `docs/architecture/shared-utilities.md` (decision record 09, amended 2026-10-21) |
| `src/Blazix.Primitives/js/` | TypeScript modules (one per decision-record-04 module) with their vitest tests beside them |
| `src/Blazix.Primitives.Generators/`, `src/Blazix.Primitives.Analyzers/` | shipped generator; internal convention analyzers |
| `tests/Blazix.Primitives.Tests*`, `tests/Blazix.Primitives.Fixtures*`, `tests/browser/` | L1, L2, generator tests; the fixture host; L4 specs |
| `.github/` | CI: workflows, composite actions, the scripts they run (with their tests); contract = `.github/CONTEXT.md`, decision record 11 |
| `spikes/` | throwaway verification probes, excluded from the solution |

## How to work here

- Decisions are made with the user by grilling (one question at a time, options with costs, a recommendation); a settled cross-cutting decision becomes a record in `docs/decisions/`.
- A requirement document is amended only under `docs/conventions.md` §3.5: IDs are never renumbered, every change is dated, a port decision cites its record.
- A component is `implemented` only when every `Test:` row is green at its layer, L4 on both render modes (decision record 13).
- Run the docs lint after every edit under `docs/`; it must report only the seven accepted React-term mentions.
- Naming, member order and field rules: decision record 08. Performance rules: decision record 16.
