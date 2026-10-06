# Risk Game

A multiplayer browser game of World Domination: a detailed 3D Classic map, private tables for friends, and selectable AI opponents. The .NET server owns every rule and dice result; the TypeScript client displays the board and sends commands.

## Play locally

```sh
docker compose up --build -d
```

Open **http://localhost:8092**. Create a table, copy its invite link, and add up to five friends or AI players. The host starts the game when at least two commanders are seated. Each AI seat has its own difficulty.

To watch AI strategies, select **Your role → Spectate an AI-only game**, create the room, and add 2–6 AI commanders with individual difficulties. The host watches without occupying a commander seat. Friends can use the invite link to spectate before or after the game starts; the current turn, troop counts, and battle journal update automatically.

- Friends must reach the same server address. For LAN play, open the host computer's LAN IP on port 8092 before copying the invite; a `localhost` invite works only on the host computer.
- The map keeps a fixed viewing angle. Drag to pan and use the wheel, pinch gesture, or zoom buttons to inspect territories. Click your territory and a destination, then use the turn panel. **Territory list** provides keyboard and touch controls and remains usable without WebGL.
- The full-screen board uses angular coastlines, deep black territory edges, shaded land, a textured ocean chart, outlined troop discs, and illustrated commander portraits. The bottom medallion shows the current commander, phase, and deployable army. Territory names appear on keyboard focus and remain available in **Territory list**.
- **Continent bonuses** (the chart button) switches to a colored overlay showing each region's bonus and your control progress. The card stack opens your hand. Zoom buttons also support touch play; the top question mark opens the rules.
- **Game menu → Campaign journal** shows recent moves and the last battle's dice. Phones also show a compact casualty result below turn controls; map controls move when the turn panel expands.
- A browser reload or transport reconnect resumes the seat using a secret stored in that tab's session storage. A second device cannot resume the same seat without its credential.
- Disconnecting preserves the seat and waits at that player's turn. **Leave table** replaces a started-game seat with an Easy AI. **Surrender** eliminates the commander and leaves passive armies on the board.
- Spectators have no army or private card hand and cannot submit gameplay commands. Leaving releases only their spectator place; reloading or reconnecting restores it. AI turns pause when nobody is connected and resume when a spectator returns.
- Leaving while offline first reconnects and releases the seat; if the server is unreachable, the saved credential is retained so the game can be resumed later.
- Tables are in memory. Restarting the server ends them. Tables with no connected humans expire after 30 minutes. Bots stop when nobody is connected.
- `PORT=8093 docker compose up --build -d` changes the exposed port.

## Implemented rules

The initial ruleset is **Classic World Domination with True Random dice**, using the mobile publisher's documented draft, attack, fortification, and card rules. The visual style follows the mobile game's map, counter, and control conventions with independently created presentation and attributed open cartography.

| Rule | Behavior |
| --- | --- |
| Players | 2–6 human or AI commanders |
| Classic board | 42 territories, six continents, explicit symmetric borders including sea connections |
| Initial armies | 40 / 35 / 30 / 25 / 20 per player for 2 / 3 / 4 / 5 / 6 players |
| Setup | Automatic territory/army distribution, or round-robin claiming and one-at-a-time placement |
| Draft | `max(3, floor(owned territories / 3))` plus complete-continent bonuses |
| Continents | North America +5, South America +2, Europe +5, Africa +3, Asia +7, Australia +2 |
| Combat | Up to three attack dice versus up to two defense dice; descending comparisons, defender wins ties |
| Manual roll | One battle round with 1–3 selected attack dice; defense uses its maximum available dice |
| True Random blitz | Consecutive maximum-dice rounds until the committed army is lost or the territory is captured |
| Capture | Explicit occupation; move at least the final roll's attack-dice count and leave one behind |
| Cards | 42 territory cards and two wilds; one random card at turn end if any territory was conquered; traded cards return to the deck |
| Fixed sets | Infantry 4, cavalry 6, artillery 8, mixed 10; wild substitution |
| Progressive sets | Global trades: 4, 6, 8, 10, 12, 15, then +5 each |
| Territory bonus | Two troops on one owned territory depicted in the traded set |
| Forced trades | At five cards during Draft; inheriting more than five cards returns the attacker to Draft without a second base reinforcement award |
| Elimination | Capture the last territory to inherit that player's cards; game ends with one active commander |
| Fortification | One move between friendly territories connected entirely through friendly territory; leave one troop behind |

**Scope:** this release does not claim parity with every current mobile mode or modifier. Balanced Blitz, Capitals, Secret Missions, Secret Assassin, Zombies, Fog of War, Blizzards, Portals, ranked matchmaking, accounts, alliances, and turn timers are not implemented. Private tables are untimed. The publisher's public dice repository describes an older version and omits parts of its estimation logic; it is not used as an exact reproduction of today's Balanced Blitz.

The uniform shuffled initial distribution and territory-card symbol assignment are this implementation's explicit choices. The publisher does not document all current mobile setup and card-deck implementation details, so identical seeded outcomes or identical territory/symbol assignments are not promised.

### Rule references

- [SMG: official rules and Classic cartography](https://smgstudio.freshdesk.com/support/solutions/articles/11000025091-is-risk-global-domination-based-on-official-rules-)
- [SMG: Draft](https://smgstudio.freshdesk.com/support/solutions/articles/11000121587-wiki-draft)
- [SMG: Attack and blitz](https://smgstudio.freshdesk.com/support/solutions/articles/11000121588-wiki-attack-blitz-roll)
- [SMG: Manual dice](https://smgstudio.freshdesk.com/support/solutions/articles/11000121592-wiki-manual-roll)
- [SMG: Fortify](https://smgstudio.freshdesk.com/support/solutions/articles/11000121590-wiki-fortify)
- [SMG: Card trading](https://smgstudio.freshdesk.com/support/solutions/articles/11000121591-wiki-card-trading)
- [SMG: published legacy dice logic and limitations](https://github.com/smgstudio/risk-dice)

## AI opponents

| Difficulty | Implementation | Behavior |
| --- | --- | --- |
| Easy | `heuristic-easy` | Cautious army advantage thresholds |
| Normal | `heuristic-normal` | Frontier concentration and continent priorities |
| Hard | `monte-carlo-battles` | 96 sampled True Random battles per candidate, bounded to eight candidates |
| Expert | `expert-turn-planner` | Analytic battle odds, connected-capture planning, elimination bounties, and continent defense |

Hard uses battle-level Monte Carlo evaluation, rather than full-game Monte Carlo tree search. Large candidate armies are scaled to bound evaluation cost. No trained reinforcement-learning player is bundled.

Expert compares capture chains and troop allocations using exact battle odds for stacks up to 512, with a scale-aware approximation above that limit. It considers public opponent card counts without seeing their card identities. See [the implementation and evaluation limits](docs/expert-ai.md); beating the existing bots does not establish that it can beat every human.

In a 688-game, seat-balanced evaluation on new seeds, Expert won 89.8% of fixed-card duels and 93.8% of progressive-card duels against Hard. Six-player win rates were 52.1% and 70.8%; six progressive matches reached the round limit and count as non-wins. [Full results and methodology](docs/expert-ai.md#reproducible-evaluation) are included.

Run reproducible seat-balanced matches with `dotnet run --project tools/Risk.Arena -c Release -- --seeds 64 --first-seed 3000 --players 2 --candidate expert --opponent hard --cards fixed`. JSON reports include unfinished games and a conservative seed-level confidence interval.

`IPlayerStrategy` accepts a copied `GameObservation` and returns a `GameCommand`. Observations contain the public board and the acting player's cards, never opponents' hands or the live game RNG. Every proposed action passes through `Game.Apply`, exactly like human commands. `StrategyCatalog` maps lobby difficulty to an implementation; add future policy/model adapters there. Bots use their own RNG, so evaluating possibilities cannot advance or predict authoritative dice. The server schedules bot work after state changes and runs up to four workers, with a short delay for readable gameplay.

## Architecture

The [Docker training pipeline](docs/training.md) records Expert decisions, trains a small imitation policy, and continues with masked PPO and frozen-opponent self-play. Its initial scope is two-player Fixed-card games; learned checkpoints are evaluated offline before lobby integration.

The first run used 11,689 Expert demonstrations and 16,384 PPO steps. Across 512 fresh-seed evaluation matches, the PPO checkpoint won 61.7% of duels against Hard and 31.3% against Expert. The learned model remains experimental; Expert is still the strongest validated lobby opponent. [Training details and all results](docs/training.md#first-completed-experiment) are recorded.

A follow-up pilot gave the original 74,000-parameter policy and an eight-times-larger policy 32,768 PPO steps each. The larger network showed no benefit; on new seeds the original-size policy won 31.3% against Expert and 79.7% against Hard. [Model size results](docs/training.md#model-size-experiment) are recorded.

```text
React menus and turn controls ── commands ──► SignalR /play
Babylon.js 3D board          ◄── private snapshots ──┤
                                                  │
                            ASP.NET Core rooms ──► Risk.Sim
                                                  ▲
                               AI observations ───┘
```

- **.NET 10:** pure simulation library, SignalR room host, Newtonsoft.Json camelCase protocol, central NuGet versions, warnings as errors, xUnit v3 on Microsoft Testing Platform.
- **React + TypeScript + Vite:** Babylon.js 3D terrain and infantry, an original ocean chart, screen-sized labels, commander portraits, dice, self-hosted fonts, and responsive turn controls.
- **Shared content:** `content/classic-topology.json` defines legal borders; `content/cartography.json` contains attributed vector paths. `npm --prefix client run generate:map` produces `content/classic.json` with sampled coastlines, islands, and interior label anchors. The geometry retains [CC BY-SA 4.0](content/CARTOGRAPHY-LICENSE.md); the mobile app's assets are not bundled.
- **Authority:** one lock per room; server binds connections to seats; host-only lobby mutation; expected room revisions and bounded action IDs prevent stale or repeated mutation.
- **Privacy:** each recipient gets a separately built snapshot containing only their cards. Reconnect secrets appear only in the joining player's welcome response.
- **Bounds:** six commander seats and up to 16 spectators per AI-only room, 64 rooms, 512 connections, 4 KiB hub messages, 20 RPCs/second per connection, negotiation rate limiting, inactive room cleanup.
- **Docker:** multi-stage client/server build, non-root runtime, read-only filesystem, dropped capabilities, no production host deployment.

## Verify

```sh
dotnet test
npm --prefix client ci
npm --prefix client run generate:map
npm --prefix client test
npm --prefix client run build
docker compose up --build -d
npm --prefix client exec playwright install chromium
npm --prefix client run test:e2e
```

`RISK_BASE_URL` can point browser tests at a different Docker endpoint. Tests cover deterministic setup, draft counts, card schedules, dice ties, committed armies, mandatory capture movement, elimination chains, fortification paths, complete mixed-AI games, real SignalR fallback transport, private hands, host permissions, duplicate/stale commands, two browser clients, seat restoration, AI turns, phone manual combat, and six-player landscape layout. A browser fixture renders the real card panel with five cards and verifies scrolling, the +2 territory selector, and the submitted trade command; server trade validation is covered by the .NET tests.

## Project layout

```text
Risk/
├── AGENTS.md
├── content/
│   ├── classic-topology.json     authoritative territory borders
│   ├── cartography.json          attributed vector coastlines
│   ├── classic.json              generated geometry and label anchors
│   └── CARTOGRAPHY-LICENSE.md
├── src/
│   ├── Risk.Sim/                 rules, state, seeded randomness, AI strategies
│   └── Risk.Server/              SignalR, rooms, snapshots, bot scheduling
├── client/
│   ├── src/game/                shared types and move guidance
│   ├── src/net/                 reconnecting session
│   ├── src/render/              original 3D board
│   ├── src/ui/                  home, lobby, orders, cards, rules
│   ├── scripts/generate-map.mjs  offline geometry generation
│   └── e2e/                    multiplayer browser tests
├── tests/
│   ├── Risk.UnitTests/
│   └── Risk.IntegrationTests/
├── tools/
│   ├── Risk.Arena/              headless, reproducible AI evaluation
│   └── generate-map.py
├── docs/
│   ├── protocol.md
│   ├── expert-ai.md             planning policy and evaluation limits
│   └── ai-results.json          frozen-policy per-match results
├── Dockerfile
└── docker-compose.yml
```
