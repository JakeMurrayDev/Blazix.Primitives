# Blazix.Primitives

An unofficial Blazor (C#) port of [MUI Base UI](https://base-ui.com/), the headless, accessible component library. Both interactive render modes (Server and WebAssembly) are first-class targets.

**Status:** specification complete; implementation not started. The port is built documentation-first: `docs/` holds 47 requirement documents (about 6,040 traceable requirements reverse-engineered from Base UI v1.9.0) and the decision records that fix how they map onto Blazor. Start at [docs/README.md](docs/README.md).

| | |
|---|---|
| Base UI pin | v1.9.0 (`ad7ecda`), see `docs/conventions.md` §1 |
| Target | .NET 11 / C# 15 (`net11.0`) |
| Package | `Blazix.Primitives` on NuGet after .NET 11 GA (decision record 10) |
| Decisions | [docs/decisions/](docs/decisions/) — 16 accepted records |
| Glossary | [CONTEXT.md](CONTEXT.md) |
| Plan | [docs/decisions/12-implementation-order-and-milestones.md](docs/decisions/12-implementation-order-and-milestones.md) |

## Building

You need the .NET 11 **RC2** SDK (`11.0.100-rc.2.26504.105`, pinned in [global.json](global.json) with `rollForward: latestFeature`, so GA flows in without an edit) and the active **Node LTS** line ([.node-version](.node-version), currently 24). Consumers of a preview package need the same SDK.

```bash
dotnet build -c Release                                  # whole solution; warnings are errors
dotnet test --solution Blazix.Primitives.slnx -c Release # L1, L2 and the generator tests (Microsoft.Testing.Platform, see global.json)
npm ci && npm run test:js                                # the Node toolchain and L3; L4 is `npm run test:browser`
```

`dotnet test` runs on Microsoft.Testing.Platform (`test.runner` in `global.json`), so a single project is selected with `dotnet test --project tests/Blazix.Primitives.Tests`, not as a positional argument. CI builds and tests the three test projects without the fixture host (see [.github/workflows/ci.yml](.github/workflows/ci.yml)).

## Working in this repository

Read [AGENTS.md](AGENTS.md) first: it says where everything lives and how documents are amended. After any edit under `docs/`, run the lint from the repository root:

```bash
python3 docs/_tools/check_docs.py
```

## Licence and acknowledgements

MIT, see [LICENSE](LICENSE). Base UI is © Material-UI SAS (MIT) and Floating UI is © Floating UI contributors (MIT); their notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). This project is not affiliated with or endorsed by MUI.
