# Architecture rules

- Keep event-map positions in a stable logical coordinate system and scale only the rendered surface, so saved layouts remain device-independent.
- The visual layer (colors, fonts, layout, Tailwind classes, `src/styles.css`, `src/components/ui/*`) must not change when working on data or back-end code. Screens read data only through `useStore` in `frontend/src/lib/store.ts`.
- Back-end follows Clean Architecture: `EventFlow.Api → EventFlow.Infrastructure → EventFlow.Application → EventFlow.Domain`. The Domain project has no external dependencies.
- Order totals are always computed on the server from catalog prices; never trust values sent by the client.
- Every API change needs a test in `backend/tests` (`dotnet test` must stay green).
