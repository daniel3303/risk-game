# Ultimate AI

- Choose **Ultimate · Conqueror** when adding a player to a normal or AI-only spectator room.
- Ultimate is Master's turn planner with a value network, trained by self-play, correcting its two-player evaluation.
- It uses exactly Master's information: the public board, public card counts, rules, trade counter, and its own hand.
- Every action passes through the same `Game.Apply` validation used for human moves.
- The correction applies whenever exactly two players remain, including the end of larger games; otherwise Ultimate plays exactly like Master.

## Reserved name

- **Ultimate always carries the strongest trained model.** The name is not tied to one network or method.
- A new model replaces the bundled one only after it beats the current Ultimate seat-balanced on at least 2,000 unused seeds, with the lower end of the 95% Hoeffding interval (the project's standard) above 50%.
- The replacement keeps the `ultimate` difficulty and the `ultimate-learned-planner` identifier; record its evaluation here and in [ultimate-results.json](ultimate-results.json).

## How it decides

- Master searches capture chains and placements and scores each planned board with a hand-written formula: armies, territories, income, cards, and continent exposure.
- Ultimate adds a learned correction to that score for every board the search evaluates, unless the board is already won.
- The correction comes from a network that predicts the mover's chance of winning from that board.

| Part | Value |
| --- | --- |
| Inputs | 621 features: per territory, owner, army thresholds, scaled armies, and the outright capture chance of the strongest adjacent enemy stack; plus incomes, totals, card counts, continent ownership, trades, and Master's score |
| Network | 621 → 64 ReLU units → 1, on top of a calibrated logistic term for Master's score |
| Correction | Half the learned term, converted into Master's score units, clamped to ±60 |
| Size | 39,874 parameters in [ultimate-model.json](../src/Risk.Sim/Learning/ultimate-model.json), embedded in `Risk.Sim` |
| Latency | About 4× Master's thinking time over six games; slowest decision 0.36 s against Master's 0.06 s, on a development machine busy with other work |

- Half strength was chosen on development seeds: at full strength the learned networks scored 44.0%–50.8% against Master, at half strength 50.0%–53.7%.

## How it was trained

- Two-player Classic games with Fixed cards and automatic setup only.
- **Self-play data:** 30,000 Master-versus-Master games on seeds 760000–789999, recording 436,083 afterstates (each board where a player ends its attacks), each labelled with the game's winner.
- **Targets:** TD(λ = 0.7), as in TD-Gammon. Each afterstate's target blends the predicted value of the same player's next afterstate with that afterstate's own target; the last afterstate uses the result. The first bootstrap is Master's score calibrated to win probability.
- **Fit:** cross-entropy against the targets, Adam, learning rate 0.001 decayed in the second half, L2 0.00001, 8 epochs, training seed 3. Seeds divisible by ten were held out; the epoch with the lowest held-out loss against real outcomes is kept.
- Held-out log-loss against outcomes fell from 0.216 for calibrated Master scores to 0.186.
- The fit is deterministic: the same data, options, and seed reproduce the same model on any thread count.

```sh
dotnet run --project tools/Risk.Learning -c Release -- record --output artifacts/learning/gen0.bin --first-seed 760000 --games 30000
dotnet run --project tools/Risk.Learning -c Release -- fit --data artifacts/learning/gen0.bin --output artifacts/learning/gen0-net.json --seed 3
dotnet run --project tools/Risk.Learning -c Release -- bundle --members artifacts/learning/gen0-net.json --scale 0.5 --output src/Risk.Sim/Learning/ultimate-model.json
dotnet run --project tools/Risk.Learning -c Release -- evaluate --model src/Risk.Sim/Learning/ultimate-model.json --first-seed 806000 --seeds 500
```

- An interrupted recording resumes: the last saved game, which may be partial, is replayed. Training data stays under ignored `artifacts/`.
- `record --model <bundle>` plays the next generation with a learned model, and `fit --bootstrap <bundle>` bootstraps targets from it.

## Evaluation

- The model was frozen before these seeds were played; each seed was evaluated once, from both seats, through the Arena (`--candidate ultimate`).

| Opponent | Seeds | Wins / games | Win rate | Hoeffding 95% | Per-seed normal 95% | First seat | Second seat | Unfinished |
| --- | --- | ---: | ---: | --- | --- | ---: | ---: | ---: |
| Master | 900000–903999 | 4,142 / 8,000 | 51.8% | 49.6%–53.9% | 51.0%–52.5% | 3,356 / 4,000 | 786 / 4,000 | 0 |
| Expert | 910000–911999 | 2,379 / 4,000 | 59.5% | 56.4%–62.5% | 58.4%–60.6% | 1,803 / 2,000 | 576 / 2,000 | 0 |

- Against Master the edge is 1.8 points. The project's standard, the conservative Hoeffding bound over seed blocks used for Expert and Master, does not exclude 50%; the ordinary normal interval over seed blocks does.
- Ultimate is therefore the strongest bot by point estimate, not a proven winner against Master by the project's standard.
- Against Expert, Ultimate scored 59.5%; Master scored 58.2% on a different seed bank.
- Every match is recorded in [ultimate-results.json](ultimate-results.json).

```sh
dotnet run --project tools/Risk.Arena -c Release -- \
  --seeds 4000 --first-seed 900000 --players 2 \
  --candidate ultimate --opponent master --cards fixed --setup automatic --parallel 8
dotnet run --project tools/Risk.Learning -c Release -- luck --first-seed 920000 --deals 200 --replays 20
```

## Why the gain is small

- **Dice decide most duels.** Master against itself, each of 200 starting deals (seeds 920000–920199) replayed 20 times with new dice:
  - The first player won 81.8%.
  - In 42% of deals the first player won at least 90% of the replays; only 24.5% of deals were close (30%–70%).
  - The dice explain 91% of the variation in results; the deal explains 9%.
- **Master's search is saturated.** On development seeds, larger placement and fortification searches, and splitting reinforcements between two borders, all scored 48.9%–50.7% against Master.
- **Strength gains shrink near the ceiling.** Expert → Master gained 8 points by searching more; Master → Ultimate gains about 2 points with a learned evaluation.

## What was tried

All rows are against Master on development seeds, 1,000 games each unless noted; Master against itself scores exactly 50% on the same seeds.

| Approach | Result | Why |
| --- | ---: | --- |
| Value network on 31 board totals, trained on Expert outcomes (earlier experiment) | 43.2% vs Expert, 600 games | Board totals cannot see threats; one fit, no iteration |
| Value network without threat features, on raw game outcomes of 40,000 Master games | 46.9% | Its correction varied across the boards compared for one move by 41% as much as Master's score, uncorrelated with it: noise the search exploited |
| The same at 30% / 10% strength | 50.1% / 52.2% | Shrinking toward Master |
| Network trained to rank boards by a simulated opponent reply (8,800 turns) | 51.4% | Labels are Master's score one turn later, so it learns consistency with Master, not winning |
| Playing out each attack option through the opponent's reply (6 options × 4 dice samples) | 44.0%, 300 games | Four dice samples cannot separate options that differ by a few points |
| Larger placement or fortification search; split reinforcements | 48.9%–50.7% | Search already saturated |
| TD network, full strength (linear / 64 units) | 44.0% / 48.6% | Over-corrects |
| **TD network, half strength (64 units)** | **52.6%; 52.9% on 2,000 more** | Shipped design |
| Shipped model: the same recipe refitted deterministically with the committed tool | 52.2% | Same design, new seeds |
| Second generation (trained on its own self-play), half strength | 53.7% and 51.4% on two sets | No reliable gain over the first generation |
| Four second-generation networks averaged | 50.0%–53.0% | Same |
| Third generation | 51.1%–51.7% | Same |
| Correction only in one phase (placement, attack, occupation, or fortification) | 49.4%–50.6% | The small gain is spread across phases |
| Planner maximising win probability instead of score | 52.7% | No change |

- Selection evidence for these rows came from prototype runs whose raw match lists were not retained; only the final evaluation is recorded.
- Development used seeds 700000–889999. The evaluation seeds, 900000 and above, were never used before the final run.

## Limits

- Trained and evaluated only on two-player Classic games with Fixed cards and automatic setup.
- Progressive cards, manual setup, and three to six players have not been evaluated for Ultimate.
- Results measure strength against the bundled bots, not against skilled humans.
