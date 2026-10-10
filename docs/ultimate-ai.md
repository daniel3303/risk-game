# Ultimate AI

- Choose **Ultimate · Conqueror** when adding a player to a normal or AI-only spectator room.
- Ultimate is Master's turn planner with a value network, trained by self-play, correcting its two-player evaluation.
- It uses exactly Master's information: the public board, public card counts, rules, trade counter, and its own hand.
- Every action passes through the same `Game.Apply` validation used for human moves.
- The correction applies whenever exactly two players remain, including the end of larger games.
- With more rivals, Ultimate adds [frontier defence and threat weighting](#several-rivals) and a [second network learned from three-player self-play](#learning-with-several-rivals) to Master's evaluation; its duel play is exactly the evaluated model's.

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

- The model was frozen before these seeds were played; each seed was played from both seats through the Arena (`--candidate ultimate`).
- The run was repeated once, unchanged in model and seeds, after a review fix restored Master's exact score arithmetic; 10 of 12,000 matches changed. The tables show the repeated run.

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

## Several rivals

- A human playing one turtle against two Ultimates reported easy wins: hold one continent, keep one stack on its border, take one cheap territory per turn for a card, trade every set, and strike once the bots have bled each other.
- The bots lost that way because Master's one-turn planner scores armies wherever they stand: its capture chains left trails of one-army territories that the turtle swept for free cards, and it fought the other bot at marginal odds while the turtle grew.
- `TurtleStrategy` (`probe-turtle`, Arena name `turtle`) scripts that style, handing over to Expert's planner once its army is half again the strongest rival's. Probes are Arena opponents only; the lobby never offers them.

| Term | Ultimate | Effect |
| --- | --- | --- |
| Frontier risk (`FrontierRiskValue`) | 1 | Each own border territory's chance of capture by the strongest adjacent enemy stack, forecast with that rival's income and card trades, times the territory, a third of a territory's income and its defenders; the worst chance also costs the card the capturer would draw |
| Safe occupations | with frontier risk | Besides moving everything forward or guarding a complete continent, captures may leave the source the smallest garrison, up to twelve, that the strongest adjacent enemy stack takes less than half the time |
| Threat weighting (`ThreatWeighting`) | 1.5 | Each rival's armies and income count in proportion to its armies, three times its income and its cards, relative to the average rival, clamped to 0.25–3 |
| Duel frontier risk (`DuelFrontierRiskValue`) | 0 | Off: in duels the same term scored 42%–47% against Master on 200 development seeds, so duels keep the evaluated model's exact decisions |

- Both terms are scored only while more than two players remain; `ExpertTuning.Ultimate` holds them and `ExpertTuning.Master` is untouched, so Master and Expert reproduce their published matches.
- Selection used development seeds 960000–966999: 100 seeds per setting against the turtle (two candidates and one probe, the probe in every seat) and 100 against two Masters, then 150 more of each for the four leading pairs. Frontier risk alone took the probe from 33% to 13%–16%; threat weighting alone to 17%; together 10%–13%. Over both banks the chosen pair scored 53% against two Masters where the alternatives scored 43%–49%, with the probe at 10%–12% for all of them.
- Raising the attack thresholds against several rivals (0.75 to open, 0.85 to continue) or doubling rival income's weight cut the probe to 9%–13% but cost 2–5 points against two Masters, so neither shipped.
- A frontier weight of 2 or more cost the duel about 15 points on development seeds, which is why the weight is split by player count.
- With the hand-written terms alone, decisions stayed under 0.1 s single-threaded in three-player games; the frontier term costs about three times Master's average decision time.
- With the multiplayer model, three-player decisions on seeds 965000–965007 averaged 2.5 ms single-threaded (0.8 ms without it), the slowest 0.17 s.

### Multiplayer evaluation

- The bundled multiplayer model (generation 1, below) was frozen before seeds 980000–985149 were played; the probe or the candidate took every seat in turn.
- The hand-terms column is the same preset without that model, evaluated once on seeds 970000–975149 before it was trained; the Master rows involve no Ultimate and keep their earlier seeds.

| Players | Match | Seeds | Wins / games | Rate | Hoeffding 95% | Hand terms only | Fair share |
| ---: | --- | --- | ---: | ---: | --- | ---: | ---: |
| 3 | turtle against two Masters | 970000–970299 | 291 / 900 | 32.3% | 24.5%–40.2% | — | 33.3% |
| 3 | turtle against two Ultimates | 980000–980299 | 58 / 900 | 6.4% | 0.0%–14.3% | 11.2% | 33.3% |
| 3 | Ultimate against two Masters | 981000–981299 | 763 / 900 | 84.8% | 76.9%–92.6% | 50.4% | 33.3% |
| 3 | Ultimate against two Experts | 984000–984149 | 379 / 450 | 84.2% | 73.1%–95.3% | 59.1% | 33.3% |
| 4 | turtle against three Masters | 972000–972149 | 214 / 600 | 35.7% | 24.6%–46.8% | — | 25.0% |
| 4 | turtle against three Ultimates | 982000–982149 | 87 / 600 | 14.5% | 3.4%–25.6% | 21.5% | 25.0% |
| 4 | Ultimate against three Masters | 985000–985149 | 534 / 600 | 89.0% | 77.9%–100.0% | 42.0% | 25.0% |
| 6 | turtle against five Ultimates | 983000–983099 | 73 / 600 | 12.2% | 0.0%–25.7% | 20.3% | 16.7% |

- The previous Ultimate decided exactly as Master until the first elimination, so the Master rows are close to its baseline; the exact previous preset (`ExpertTuning.Master with { Valuation = ValueModel.Ultimate }`, seated with the Arena's seeding and limits), replayed on the same seeds outside the Arena, gave the probe 31.7% (285 of 900) in three-player and 36.2% (217 of 600) in four-player games.
- In three-player games the probe fell from its fair share to under a fifth of it, and Ultimate went from a third of the games against two Masters to five in six.
- The multiplayer model saw only three-player games in training; four- and six-player games were evaluated once each, and the probe fell below its fair share in both.
- Each report's totals are recorded in [ultimate-results.json](ultimate-results.json) under `multiplayer`; the duel evaluation above is unchanged, and 100 of its recorded matches replayed identically with this source.

```sh
dotnet run --project tools/Risk.Arena -c Release -- \
  --seeds 300 --first-seed 980000 --players 3 \
  --candidate turtle --opponent ultimate --cards fixed --setup automatic --parallel 8
```

- The probe is a script; a thoughtful human turtle is stronger, so these rates bound the exploit rather than promise its absence.

## Learning with several rivals

- Beyond the two hand-written terms, Ultimate can carry a second value model, `ultimate-multiplayer-model.json`, that corrects the hand score whenever more than two players remain; the duel model stays untouched.
- Its encoding (`multi-board-v1`, 572 features) exposes what a human strategist looks at, so the weights can express those patterns rather than discover them from raw ownership alone:
  - per territory: mine / the strongest rival's / another rival's, army thresholds, scaled armies, the outright capture chance of the strongest adjacent enemy stack, whether an own territory is a border or interior, whether it is a gate of an own complete continent, and how many of its neighbours lie outside its continent;
  - position shape: own border and interior counts, the largest own stack, the share of own armies standing on borders, each continent's share held by me and by the strongest rival, and which continents are complete for me or for any rival;
  - finishing a player: the weakest rival's territories, armies and cards, and the worst and mean chance of capturing its territories outright from adjacent own stacks;
  - totals: incomes, territories, armies and cards for me and the strongest rival, the sum over rivals, the number of rivals, trades, and the hand score.
- Training is the duel recipe applied to league self-play: every fourth game seats the current Ultimate everywhere, the others give one rotating seat to the turtle probe, Master or Expert; only Ultimate's afterstates are recorded while more than two players remain, labelled with its outcome and fitted by TD(λ = 0.7) bootstrapped from the previous model (or the calibrated hand score for the first generation).
- One command runs the loop; each promoted candidate becomes the next generation's learner and bootstrap, so the bot keeps improving until a candidate fails the gate:

```sh
dotnet run --project tools/Risk.Learning -c Release -- improve --players 3 --first-seed FIRST --games 20000 \
  --output artifacts/learning/genN --generations 4 --window 2 --lambda 0.9 \
  --promote src/Risk.Sim/Learning/ultimate-multiplayer-model.json
```

- `--model` names the learner (default: the bundled model); each generation goes to the next directory (gen3, gen4, …) and starts at the seeds after the previous one: first seed + games + 1,300 (recording, then the gate's 1,000 strength and 300 probe seeds).
- `--window W` fits each generation on its own games plus the previous W − 1 generations' recordings, as AlphaZero trains on a window of recent games; `--extra-data` supplies earlier recordings to the first generation.
- The gate plays the candidate against copies of the current model: it must win seat-balanced on unused seeds with the 95% normal lower bound over seed blocks above the fair share, and the turtle probe must not be demonstrably stronger against it than against the current model on the same seeds (the probe's normal lower bound against the candidate stays at or below its rate against the current model). A promoted candidate is copied to the promotion path; rebuild to play it.
- Generations 1–3 were gated on the conservative Hoeffding bound instead. Neighbouring generations differ by a few points, inside its ±4.3-point margin at 1,000 seeds, so it rejected generation 3 at 36.0%; the normal interval is about a third as wide, and published evaluations still report both.
- `improve` refuses an output directory that already holds a finished generation, and its data file is named by encoding, learner and seed range, so an interrupted run resumes only the same games played by the same model.
- Seeds from 1,000,000 upward are reserved for these generations; each generation's evaluation seeds follow its recording seeds, so they are never played before the candidate is frozen.
- What it cannot do: learn from the few games played against one person, invent plans the search never proposes, or escape the dice; gains per generation are expected to shrink, as the duel generations did.

### Generations

| Generation | Recorded games (seeds) | Afterstates | Held-out log-loss, hand score → model | Against two current Ultimates (seeds) | Turtle against candidate / current (seeds) | Promoted |
| ---: | --- | ---: | --- | --- | --- | --- |
| 1 | 20,000 (1000000–1019999) | 829,279 | 0.5782 → 0.5538 | 72.8% of 3,000, 68.5%–77.1% (1020000–1020999) | 6.9% / 11.2% of 900 (1021000–1021299) | yes |
| 2 | 20,000 (1021300–1041299) | 913,537 | 0.5886 → 0.5606 | 50.1% of 3,000, 45.8%–54.4% (1041300–1042299) | 5.0% / 6.9% of 900 (1042300–1042599) | yes |

- Generation 1 ran from commit `8314694`, a build with no multiplayer model, so its learner and bootstrap were the hand-written terms alone; its command is recorded in [ultimate-results.json](ultimate-results.json).
- Its validation rows were every tenth seed, which by the league's rotation held out only all-Ultimate and Master games; later generations hold out every tenth block of four seeds per player, so each league opponent is represented in every seat.
- The log-loss is against final outcomes, so lower means the model predicts who wins better than the hand score does.
- Generation 2 ran from commit `1675a27` with generation 1 as learner and bootstrap, and is the learner of the chained run that follows; later generations start at seed 1042600.

### Search on top of the learned value

- Two search changes were screened against two generation-1 Ultimates on development seeds 1100000–1100099 (300 games each, fair share 33.3%), and against the turtle on 1101000–1101099:

| Change | Result | Adopted |
| --- | --- | --- |
| None (control; identical play) | 33.3% | — |
| Best-reply lookahead ([Schadd and Winands 2011](https://dke.maastrichtuniversity.nl/m.winands/documents/BestReplySearch.pdf)): the four best attack plans and fortifications re-scored after the most damaging rival's forecast reinforcements and capture chain, weight 1 | 29.0% | no |
| The same lookahead at weight 0.5 | 33.7% | no |
| Split deployment: part of the reinforcements offered to the four most threatened borders | 29.7%; turtle 9.0% against it, 7.3% against the control | no |

- Neither change helped: the value network was fitted on positions its own search reaches, so a wider or deeper search without retraining mostly finds the network's blind spots. AlphaZero avoids this by training the network on the searching policy's own games, so such changes have to enter through a generation rather than be bolted on.
- Related work: TD(λ) with a graph network and game-tree search for Risk ([Carr 2020](https://arxiv.org/abs/2009.06355)); expert iteration with graph networks for Risk ([Gnecco Heredia and Cazenave](https://www.lamsade.dauphine.fr/~cazenave/papers/RiskConferencePaper.pdf)); AlphaZero-style search for Risk ([Blomqvist 2020](https://forums.triplea-game.org/assets/uploads/files/1637032761173-fulltext01.pdf)). The latter two report that the attack phases remain the hard part for tree search.

## Limits

- The duel model was trained and evaluated only on two-player Classic games with Fixed cards and automatic setup; the multiplayer model was trained only on three-player games with the same rules and evaluated once in four- and six-player games.
- Frontier defence and threat weighting were selected and evaluated in three-player Fixed-card automatic-setup games against the turtle probe and Master; four- to six-player games were evaluated once each, progressive cards and manual setup not at all.
- Results measure strength against the bundled bots and one scripted human style, not against skilled humans.
