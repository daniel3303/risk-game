# Expert imitation and reinforcement learning

- Train a candidate-scoring policy from Expert decisions, then masked PPO against Normal, Hard, Expert, and a frozen imitation checkpoint.
- Initial scope: Classic World Domination, two players, Fixed cards, Automatic setup, and True Random dice.
- The learned policy is an offline experiment; Expert remains the strongest validated lobby opponent.
- Every move uses the authoritative .NET `Risk.Sim` rules; Python handles the neural network through a persistent JSONL subprocess.
- Observations include the public board, the acting player's cards, public card counts, phase, and capture information. Opposing card identities, deck order, credentials, and live RNG state are excluded.

## Run in Docker

- Run from the repository root; no host Python packages or virtual environment are needed.
- The CPU-only container has three cores, 2 GiB RAM, no network, and a read-only filesystem.
- All datasets, checkpoints, and logs remain in ignored `artifacts/rl/`; `--rm` removes each finished container.

```sh
docker compose -f compose.training.yml build trainer
docker compose -f compose.training.yml run --rm trainer collect
docker compose -f compose.training.yml run --rm trainer train
docker compose -f compose.training.yml run --rm trainer evaluate \
  --model /artifacts/run-1/imitation.zip \
  --output /artifacts/run-1/imitation-vs-expert.json
docker compose -f compose.training.yml run --rm trainer evaluate \
  --model /artifacts/run-1/rl.zip \
  --output /artifacts/run-1/rl-vs-expert.json
docker compose -f compose.training.yml down --rmi local
```

- Collection defaults to 48 training seeds and 12 separate validation seeds, with each seed played from both learner seats; opposing difficulties cycle through Normal, Hard, and Expert.
- Compressed JSONL records contain encoded inputs, legal candidates, the Expert label, original command, match seed/seat, and terminal outcome.
- Imitation defaults to 20 epochs; the checkpoint with lowest validation cross-entropy is retained.
- PPO defaults to 16,384 learner decisions, with +1 for winning, −1 for losing, and zero for unfinished games.
- At time limits, the final learner-perspective observation is retained for critic bootstrapping.
- PPO uses 512-step rollouts, batches of 64, four optimization epochs, learning rate 0.0001, discount 0.995, entropy coefficient 0.005, and target KL 0.02.
- `training.json` records parameters, validation history, package versions, seed banks, elapsed time, dataset checksums, assembly checksums, and checkpoint checksums; `progress.csv` records PPO metrics.
- Dataset checksums are verified before training; evaluation checks the checkpoint and rules/feature assemblies against the recorded run.
- Only the best imitation and final RL checkpoints are retained; checkpoint files contain Python serialization and must come from trusted runs.
- CLI commands support `--help`; training accepts `--epochs`, `--steps`, and `--seed`; evaluation accepts `--opponent`, `--seeds`, and `--first-seed`.

## Policy and action limits

- A 481-feature state embedding and shared scorer rank up to 128 candidate moves, each with 68 features including the identities of traded cards; padding is masked and candidate permutations preserve predictions.
- The actor has a 64-unit state embedding and 64-unit scoring layer; the critic has two 64-unit layers. Activations are tanh.
- Inference does not query Expert for the learner's decision. Expert supplies demonstration labels or plays as a configured opposing player.
- Candidates include all boundary attacks using full-army blitz, valid duel card sets and owned-territory bonuses, all/half reinforcement placement, and minimum/middle/maximum/guarded occupation.
- Fortification considers the six largest sources, four connected frontier targets per source, and full/half/guarded amounts; ending attack or the turn is available in the corresponding phase.
- These candidates cover Expert's duel commands and several alternatives, rather than every legal troop amount or manual dice choice.
- Analytic dice odds and public continent/defense features provide inputs; the network learns their use in decisions.
- Multiplayer, Progressive cards, manual setup, diplomacy, historical memory, and browser inference have not been evaluated in this experiment.
- PPO and action masking use the maintained [Stable Baselines3 implementation](https://sb3-contrib.readthedocs.io/en/master/modules/ppo_mask.html).

## Evaluation

- Default seed banks: imitation 4000–4047, validation 4500–4511, RL 6000–6999, evaluation starting at 9000. The pre-review experiment used evaluation seeds 8000–8063.
- Overlapping training, validation, and evaluation seed banks are rejected.
- Evaluation uses a deterministic learned policy, both starting seats, and a separate RNG for each stochastic opponent.
- Soft limits are 100 rounds and 5,000 simulation commands; truncation during the opponent turn is deferred to the next learner decision. An additional 1,000-command safety bound fails loudly if the opponent never returns control.
- Unfinished games count as non-wins, without an invented winner.
- Reports retain each seed, seat, winner, round count, and action count.
- Conservative 95% Hoeffding intervals group both seats of one seed into one block and assume independent starting seeds.
- Winning against Expert on unseen seeds is the first promotion criterion; skilled-human strength needs separate validation.

## First completed experiment

- Recorded 11,689 Expert decisions in 96 training games and 2,905 decisions in 24 validation games.
- The retained imitation checkpoint matched 81.1% of validation decisions, including forced and optional choices; this metric measures teacher imitation rather than winning strength.
- Completed 20 imitation epochs and 16,384 PPO learner decisions in the CPU-only Docker container.
- Evaluated both checkpoints against Hard and Expert on seed blocks 9000–9063 from both starting seats, totaling 512 matches.

| Checkpoint | Opponent | Wins / games | Win rate | Unfinished |
| --- | --- | ---: | ---: | ---: |
| Imitation | Hard | 81 / 128 | 63.3% | 0 |
| Imitation | Expert | 23 / 128 | 18.0% | 0 |
| PPO | Hard | 79 / 128 | 61.7% | 0 |
| PPO | Expert | 40 / 128 | 31.3% | 0 |

- PPO improved the observed win count against Expert in this batch and slightly reduced it against Hard; these point estimates do not establish a statistically reliable improvement across opponents.
- Both checkpoints remain weaker than Expert. Neither was promoted to a lobby difficulty, and no human-level strength claim is established.
- [rl-results.json](rl-results.json) retains parameters, source/assembly/checkpoint hashes, validation history, conservative intervals, and every evaluation outcome.
- Larger demonstration sets, longer training, and stronger network/search combinations are future experiments; each needs a separate untouched evaluation bank before promotion.

## Verification

```sh
dotnet test
docker compose -f compose.training.yml run --rm --entrypoint python trainer \
  -m unittest discover -s tests -v
```

- Tests cover complete Expert decision coverage, privacy, legal indices, protocol defaults, learner-perspective truncation, candidate permutation, action masking, real PPO updates, checkpoint roundtrips, frozen self-play, dataset integrity, and seed separation.
