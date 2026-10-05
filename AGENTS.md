# Risk Game

- Use .NET 10, SignalR, Newtonsoft.Json, React, TypeScript, Vite, and Babylon.js.
- Keep authoritative rules in the pure `Risk.Sim` library; clients and bots submit the same validated commands.
- Keep Classic map topology and rendering geometry in `content/classic.json`.
- Keep private cards and reconnect credentials out of public room snapshots.
- Run the application through Docker Compose; the default port is 8092.
- Run `dotnet test`, `npm --prefix client test`, and `npm --prefix client run build` before delivery.
- Run `npm --prefix client run test:e2e` against Docker for multiplayer and player flows.
- Run an independent audit-only full-diff review after implementation and testing.
- Keep generated test artifacts under ignored `artifacts/`.
- Keep repository-wide instructions in this root file.
