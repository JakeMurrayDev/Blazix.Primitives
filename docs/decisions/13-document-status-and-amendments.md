# ADR 13 — Document status and amendment rule

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | what "done" means for a component; the status a document carries once its component is done; how an approved document is amended |
| Affects | `docs/conventions.md` §3 (new §3.5) and §6; `docs/_tools/check_docs.py`; `docs/README.md` status table; every component document's front-matter table |

## Context

Conventions §6 knows three statuses, `draft`, `reviewed` and `approved`; ADR 06 speaks of "approved-implemented" without defining it; the lint accepts only the three. The documents were amended after approval on 2026-10-18 and 2026-10-20 (second-pass corrections, the three implementation-revision marks, React-assuming rewrites) with "Decided (date)" notes in §16, dated ADR amendments and records in `_qa-notes.md`, but no written rule says what an amendment must carry. Conventions §3 already forbids renumbering and keeps withdrawn IDs in the index. ICM's rule that state is derivable from the file system argues against a hand-maintained "in progress" status.

## Decision

### 1. Done for a component

A component is done when: every §13 row of its document is translated at its tagged layer and green, L4 in both render modes; every relaxation row is asserted per mode (ADR 01); the conformance suite is green; the red check passed for each new test (ADR 11); the ownership and module-graph lints are green; every §16 open question is resolved or converted to a tracked issue; a fixture page exists; the public API has XML documentation; the document's front-matter and the README row read `implemented`. A component is not done on Server alone.

### 2. Status values (option S1)

One value is added after `approved`: `implemented` (the component meets §1). The lint accepts it. No in-progress status: progress is visible from the milestone table (ADR 12) and from the family folder's existence.

### 3. Amendment rule (option A1)

After approval, a change to a requirement bullet (statement, level, `Source:`, `Test:` tag, `Writer` cell) keeps its ID (append or withdraw, never renumber), carries a dated note in the bullet or in §16, and cites the ADR or ADR amendment that caused it **when it is a port decision**; a fidelity correction against the pinned source needs the dated note and a line in `_qa-notes.md`, no ADR. The front-matter table gains a `Revised` row holding the latest amendment date. A layer-tag or `Writer` correction discovered while implementing is made in the implementing pull request under the same rule. Only the user sets `approved` and `implemented`. Amendments are listed in the document (dated note) and in `_qa-notes.md` per pass; there is no separate changelog.

## Consequences

* Conventions gain §3.5 (this rule) and `implemented` in §6; `check_docs.py` accepts the new status.
* The README status table gains `implemented` rows as milestones close.
* The first `Revised` row appears on `drawer.md` with the Writer fix of ADR 04's amendment of 2026-10-20.

## Verification items

* None.

## Considered options

* An `implementing` status (a hand-maintained progress flag across 47 rows that drifts and tells less than the milestone table).
* An ADR reference on every amendment (citation and tag fixes would each need a record nobody reads); no rule (the amendments of the last three days show the churn is real).
* A docs changelog file (a second home for facts that already have one).
