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
| Deployment candidates | 8 | 24 | Compares deploying on every border territory |

- All other weights and thresholds match Expert; `ExpertTuning.Default` reproduces Expert's published matches exactly.
- Decisions take a median of 0.1 ms; the slowest observed duel decision took 379 ms on a loaded development machine.

## How it was selected

- Development seeds 100000–100499 screened single-setting changes; seeds 100500–101499 confirmed each accepted change.
- Search size mattered most: more branches, beam width, deployment candidates, and depth each raised the confirmation win rate.
- Other single-weight changes stayed within about one point of their baseline or lowered the win rate; doubling own income weight fell to 45.7%.
- Rejected approaches:
  - Per-decision Monte Carlo rollouts with Expert continuations won 16 of 32 development games while taking about 2.5 minutes per game; single-decision differences were too small to detect with affordable rollouts.
  - A learned win-probability network replacing the planner's score won 43.2% of 600 development games.
  - Penalizing every weakly held frontier territory lowered the confirmation win rate at every tested strength, from 55.5% without it to between 54.7% and 49.6%.

## Evaluation

- The preset was frozen before these seeds were played; each seed was evaluated once.

| Players | Seeds | Wins / games | Win rate | 95% interval | Fair share | Unfinished |
| ---: | --- | ---: | ---: | --- | ---: | ---: |
| 2 | 500000–501999 | 2,326 / 4,000 | 58.2% | 55.1%–61.2% | 50.0% | 0 |
| 3 | 510000–510199 | 235 / 600 | 39.2% | 29.6%–48.8% | 33.3% | 0 |
| 6 | 520000–520099 | 117 / 600 | 19.5% | 5.9%–33.1% | 16.7% | 0 |

- In duels, Master won 1,754 of 2,000 games from the first seat (87.7%) and 572 of 2,000 from the second seat (28.6%).
- Expert mirrors on development seeds won 78.7% from the first seat and 21.3% from the second, so Master gains from both seats.
- Opponents are all Expert; fixed cards and automatic setup were used in every batch.
- Intervals use the conservative Hoeffding bound over seed blocks described in [Expert AI](expert-ai.md#reproducible-evaluation).
- Multiplayer intervals are wide; those batches show no loss against Expert rather than a proven gain.
- Every match is recorded in [master-results.json](master-results.json).

```sh
dotnet run --project tools/Risk.Arena -c Release -- \
  --seeds 2000 --first-seed 500000 --players 2 \
  --candidate master --opponent expert --cards fixed --setup automatic --parallel 8
```

- `--parallel` runs independent matches concurrently; each match keeps its own dice and strategy random sources, so results match a sequential run.

## Limits

- Master was tuned against Expert in two-player fixed-card games; it is not evidence of strength against skilled humans.
- Progressive cards and manual setup were not part of the selection or final evaluation.
- Master still plans one turn at a time and does not search opposing turns or hidden-card beliefs.
