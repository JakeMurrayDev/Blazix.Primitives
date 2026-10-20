# Spike: union binding on .NET 11 RC2 (issue 10)

Throwaway probe for ADR 05 §4 / ADR 08 §3 verification items (ADR 14 §3: lives in `spikes/`, excluded
from the solution, deleted when the issue closes). The finding is the note in the side-session folder
and the comment on issue 10; this code only reproduces it.

    dotnet run --project spikes/union-binding/Harness

`Probe/` is a Razor class library (one part per case under `Parts/`, one consumer per case under
`Cases/`); `Harness/` renders every consumer through `HtmlRenderer` and prints what each parameter
received. The Razor-generated code is under `Probe/obj/Debug/net11.0/generated/`.
