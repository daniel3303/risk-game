# Master AI

- Choose **Master · Warlord** when adding a player to a normal or AI-only spectator room.
- Master runs Expert's turn planner with a wider, deeper capture search and a stronger penalty for rival income in two-player games.
- It uses exactly Expert's information: the public board, public card counts, rules, trade counter, and its own hand.
- Every action passes through the same `Game.Apply` validation used for human moves.

## Why 50% against Expert is a high bar

- Two-player Classic games strongly favor the first player: in 1,000 Expert-versus-Expert duels on seeds 100000–100999, the first player won 787 (78.7%).
- A seat-balanced evaluation plays every seed from both seats, so Expert scores exactly 50% against itself.
- Beating Expert therefore means winning more often than Expert from the first seat, the second seat, or both.

## What changed

| Setting | Expert | Master | Effect |
| --- | ---: | ---: | --- |
| Rival income weight (duels) | 4 | 8 | Values breaking rival continents and territory income more |
| Beam width | 8 | 16 | Keeps more capture chains per search depth |
| Attacks expanded per position | 10 | 20 | Considers more targets from each planned position |
| Maximum chain depth | 6 | 10 | Plans longer capture chains |
| Search budget | 512 | 4,096 | Evaluates more planned positions per decision |
| Deployment candidates | 8 | 24 | Compares deploying on up to 24 border territories |

- All other weights and thresholds match Expert; `ExpertTuning.Default` reproduces Expert's published matches exactly.
- Decisions take a median of 0.1 ms; the slowest observed duel decision took 379 ms on a loaded development machine.

## How it was selected

- Development seeds 100000–100499 screened single-setting changes; seeds 100500–101499 confirmed each accepted change.
- Seeds 400000–400999 checked an intermediate preset once; 410000–410099, 420000–420199, and 430000–430099 were early Arena and multiplayer checks.
- Selection figures below come from development runs whose raw reports were not retained; only the final evaluation is recorded.
- Search size mattered most: more branches, beam width, deployment candidates, and depth each raised the confirmation win rate.
- Other single-weight changes stayed within about one point of their baseline or lowered the win rate; doubling own income weight fell to 45.7%.
- Rejected approaches:
  - Per-decision Monte Carlo rollouts with Expert continuations won 16 of 32 development games while taking about 2.5 minutes per game; single-decision differences were too small to detect with affordable rollouts.
  - A learned win-probability network replacing the planner's score won 43.2% of 600 development games.
  - Penalizing every weakly held frontier territory lowered the confirmation win rate at every tested strength, from 55.5% without it to between 54.7% and 49.6%.

## Evaluation

- The preset was frozen before these seeds were played; each seed was evaluated once.

| Players | Rules | Seeds | Wins / games | Win rate | 95% interval | Fair share | Unfinished |
| ---: | --- | --- | ---: | ---: | --- | ---: | ---: |
| 2 | Fixed cards, automatic setup | 500000–501999 | 2,326 / 4,000 | 58.2% | 55.1%–61.2% | 50.0% | 0 |
| 2 | Progressive cards, automatic setup | 530000–531999 | 2,328 / 4,000 | 58.2% | 55.2%–61.2% | 50.0% | 0 |
| 2 | Fixed cards, manual setup | 540000–541999 | 2,110 / 4,000 | 52.8% | 49.7%–55.8% | 50.0% | 0 |
| 3 | Fixed cards, automatic setup | 510000–510199 | 235 / 600 | 39.2% | 29.6%–48.8% | 33.3% | 0 |
| 6 | Fixed cards, automatic setup | 520000–520099 | 117 / 600 | 19.5% | 5.9%–33.1% | 16.7% | 0 |

- In fixed-card automatic-setup duels, Master won 1,754 of 2,000 games from the first seat (87.7%) and 572 of 2,000 from the second seat (28.6%).
- Expert mirrors on development seeds won 78.7% from the first seat and 21.3% from the second, so Master gains from both seats.
- In progressive-card duels, Master won 1,733 first-seat and 595 second-seat games of 2,000 each.
- Manual setup favors the first player even more: Master won 1,952 first-seat and 158 second-seat games; that batch's interval includes 50%, so it does not establish an advantage.
- Every opponent is Expert.
- Intervals use the conservative Hoeffding bound over seed blocks described in [Expert AI](expert-ai.md#reproducible-evaluation).
- Multiplayer batches are inconclusive: their intervals include results below and above the fair share.
- Every match is recorded in [master-results.json](master-results.json).

```sh
dotnet run --project tools/Risk.Arena -c Release -- \
  --seeds 2000 --first-seed 500000 --players 2 \
  --candidate master --opponent expert --cards fixed --setup automatic --parallel 8
```

- `--parallel` runs independent matches concurrently; each match keeps its own dice and strategy random sources, so results match a sequential run.

## Limits

- Master was tuned against Expert in two-player fixed-card automatic-setup games; it is not evidence of strength against skilled humans.
- Progressive cards and manual setup were not used for selection; each was evaluated once after freezing the preset.
- Master still plans one turn at a time and does not search opposing turns or hidden-card beliefs.
