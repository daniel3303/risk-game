# Risk Game

- Use .NET 10, SignalR, Newtonsoft.Json, React, TypeScript, Vite, and Babylon.js.
- Keep authoritative rules in the pure `Risk.Sim` library; clients and bots submit the same validated commands.
- Author map borders in `content/classic-topology.json`; regenerate `content/classic.json` with `npm --prefix client run generate:map` after geometry changes.
- Preserve the cartography attribution and CC BY-SA 4.0 notices for the source paths and derived geometry.
- Keep private cards and reconnect credentials out of public room snapshots.
- Run the application through Docker Compose; the default port is 8092.
- Run `dotnet test`, `npm --prefix client test`, and `npm --prefix client run build` before delivery.
- Run `npm --prefix client run test:e2e` against Docker for multiplayer and player flows.
- Run an independent audit-only full-diff review after implementation and testing.
- Keep generated test artifacts under ignored `artifacts/`.
- Compare the running game with supplied visual references at matching viewport dimensions before delivery.
- Keep repository-wide instructions in this root file.
