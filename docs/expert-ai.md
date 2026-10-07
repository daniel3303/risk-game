# Expert AI

- Choose **Expert · Commander** when adding a player to a normal or AI-only spectator room.
- Expert uses the public board, public card counts, rules, trade counter, and its own hand. It cannot read opposing card identities, the deck order, reconnect credentials, or authoritative random state.
- Every action passes through the same `Game.Apply` validation used for human moves.
- Expert is a deterministic planning policy; it is not a trained reinforcement-learning model or full-game Monte Carlo tree search.
- Results against bundled bots measure this implementation's strength, not performance against expert humans.
- [Master](master-ai.md) runs this planner with a wider, deeper search and beats Expert in seat-balanced duels.

## Decisions

- Claim compact regions and connected territory groups during manual setup.
- Select the largest useful card bonus and an owned card territory for the two-troop bonus.
- Compare reinforcement placements using future connected conquests and defensive continent value.
- Search up to six consecutive captures with a beam of eight positions and ten prioritized attacks per position.
- Value continent income, army preservation, denying rival income, first-conquest cards, and elimination bounties.
- Weight successful and failed battle branches by their probabilities; successful planning states use conditional expected surviving armies rounded down.
- Compare forward occupation with a guarded source territory, obeying the real capture's minimum and maximum movement; projected captures conservatively reserve up to three surviving armies for the final maximum-dice roll.
- Move interior or surplus frontier armies toward connected useful borders during fortification.
- Estimate counterattacks using visible forces, part of the next reinforcement award, and possible card trades inferred from public counts.

## Battle odds and limits

- Enumerate every possible dice combination for each maximum-dice round, including defender-favored ties.
- Use an absorbing Markov recurrence to compute victory probability and surviving-army expectations exactly for armies of at most 512 committed attackers and 512 defenders.
- Larger armies use a renewal/normal approximation derived from the three-versus-two dice distribution; it retains the absolute army scale rather than shrinking armies to 80 units.
- Attack search evaluates at most 512 positions, plus one iterator lookahead; deployment divides that budget across up to eight borders. Occupation compares at most two allocations, and fortification tests up to 24 moves with smaller searches.
- Search uses approximate future board values and a bounded success-path beam. It does not solve opponent turns, hidden-card beliefs, multiplayer diplomacy, or the complete game tree.
- Roll probabilities are derived independently; no publisher code or external battle-solver implementation is included.
- The Markov approach is described in [Glass and Neller, *Optimal Defensive Strategies in One-Dimensional RISK*](https://cs.gettysburg.edu/~tneller/papers/math.mag.88.3.217.pdf).
- Risk-specific search has also been studied in [Gibson, Desai, and Zhao, *An Automated Technique for Drafting Territories in the Board Game Risk*](https://ojs.aaai.org/index.php/AIIDE/article/view/12388); its drafting results do not establish this policy's strength against humans.

## Reproducible evaluation

- The frozen policy played 688 new-seed games against Hard, including every candidate starting seat.

| Configuration | Wins / games | Win rate | Unfinished |
| --- | ---: | ---: | ---: |
| 2 players, fixed cards | 115 / 128 | 89.8% | 0 |
| 2 players, progressive cards | 120 / 128 | 93.8% | 0 |
| 6 players, fixed cards | 100 / 192 | 52.1% | 0 |
| 6 players, progressive cards | 136 / 192 | 70.8% | 6 |
| 3 players, fixed cards, manual setup | 32 / 48 | 66.7% | 0 |

- Full parameters, conservative intervals, policy source hash, and per-match results are in [ai-results.json](ai-results.json).
- These are results against the bundled Hard policy, with no human or external strong-policy matches; the three-player manual batch has only 16 seed blocks and a wide interval.

```sh
dotnet run --project tools/Risk.Arena -c Release -- \
  --seeds 64 --first-seed 3000 --players 2 \
  --candidate expert --opponent hard --cards fixed --setup automatic
```

- Each seed is played once with the candidate in every starting seat; all opponents use the selected baseline difficulty.
- Game dice and each stochastic policy have separate seeded random sources.
- Reports include every seed, candidate seat, winner, round count, and action count.
- Unfinished games at the configured 200-round/20,000-action default limits are reported separately and count as non-wins; no material-based winner is invented.
- The conservative 95% interval uses Hoeffding's bound on seed-level win fractions, treating matches sharing a seed as one block. It assumes independent sampled starting seeds, and measures the benchmark population only.
- The CLI validates options and supports `--help`, `--max-rounds`, and `--max-actions`.
- Development and pre-review batches used seeds 0–11, 1000–1063, and 2000–2063. The final published batch uses previously uninspected seeds starting at 3000, after freezing the corrected decision policy.
- Human matches, stronger independent opponents, and self-play training are the next evidence needed for a claim of human-level or stronger play.
