# SignalR protocol

- Hub: `/play`; health endpoint: `/healthz`.
- JSON uses camelCase properties and camelCase string enums through Newtonsoft.Json.
- `Create(name, {cards, setup})` and `Join(code, name, token)` return `{code, token, seat, snapshot}`.
- Supply `null` as the token for a new lobby seat; supply the saved token to restore a disconnected seat.
- `AddBot(difficulty)`, `RemoveBot(seat)`, and `Start()` are host-only lobby calls.
- `Act({id, revision, command})` applies a gameplay command; `id` is a canonical UUID, and `revision` is the last received room revision.
- `Leave()` releases a lobby seat or replaces a started seat with Easy AI.
- The `Snapshot` event sends `{code, revision, host, options, players, game}` separately to each connected human.
- `game.hand` contains only the recipient's cards; public `players[].cards` contains counts.
- Territory owners, current player, and winner use stable public seat IDs; simulation indexes stay internal.
- Discard snapshots with an older revision. Never apply a snapshot from another table.
- A repeated successful action ID is acknowledged without another mutation; stale new actions fail with a recoverable message.
- Commands: `claim`, `place`, `trade`, `attack`, `occupy`, `endAttack`, `fortify`, `endTurn`, `surrender`.
- Optional command fields: `from`, `to`, `count`, `dice`, `blitz`, `cards`, `bonusTerritory`.
- `attack.count = 0` commits every available troop; explicit counts keep uncommitted troops in the source. `blitz` defaults to true and `dice` to 3.
- Every boundary failure returns a `HubException` message; clients display it and retain authoritative state.
- Each mutation publishes a fresh snapshot and schedules the next eligible bot action.
