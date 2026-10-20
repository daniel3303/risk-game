# Ultimate AI

- Choose **Ultimate · Conqueror** when adding a player to a normal or AI-only spectator room.
- Ultimate is Master's turn planner with a value network, trained by self-play, correcting its two-player evaluation.
- It uses exactly Master's information: the public board, public card counts, rules, trade counter, and its own hand.
- Every action passes through the same `Game.Apply` validation used for human moves.
- The correction applies whenever exactly two players remain, including the end of larger games.
- With more rivals, Ultimate adds [frontier defence and threat weighting](#several-rivals), [siding against the people at the table](#siding-against-people), and a [second network learned from three-player self-play](#learning-with-several-rivals) to Master's evaluation; its duel play is exactly the evaluated model's.

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
| Human rival weight (`HumanRivalWeight`) | 3 | While both a person and another bot are among the rivals, each human rival's weighted armies and income count three times; see [siding against people](#siding-against-people) |
| Bot rival weight (`BotRivalWeight`) | 0.5 | In the same games, each bot rival's armies and income count half |

- These terms are scored only while more than two players remain, and the two rival weights only while both a person and another bot are among the rivals; `ExpertTuning.Ultimate` holds them and `ExpertTuning.Master` is untouched, so Master and Expert reproduce their published matches.
- Selection used development seeds 960000–966999: 100 seeds per setting against the turtle (two candidates and one probe, the probe in every seat) and 100 against two Masters, then 150 more of each for the four leading pairs. Frontier risk alone took the probe from 33% to 13%–16%; threat weighting alone to 17%; together 10%–13%. Over both banks the chosen pair scored 53% against two Masters where the alternatives scored 43%–49%, with the probe at 10%–12% for all of them.
- Raising the attack thresholds against several rivals (0.75 to open, 0.85 to continue) or doubling rival income's weight cut the probe to 9%–13% but cost 2–5 points against two Masters, so neither shipped.
- A frontier weight of 2 or more cost the duel about 15 points on development seeds, which is why the weight is split by player count.
- With the hand-written terms alone, decisions stayed under 0.1 s single-threaded in three-player games; the frontier term costs about three times Master's average decision time.
- With the multiplayer model, three-player decisions on seeds 965000–965007 averaged 2.5 ms single-threaded (0.8 ms without it), the slowest 0.17 s.

### Siding against people

- Every generation learned only its own win, so two Ultimates fought each other as hard as they fought a person: generation 5 beat generation 4 in the gate, yet against generations 5 and 6 the turtle won 7.8% and 7.3% of seeds 1220000–1220499 where generation 4 allowed 5.5%.
- Bots now know which seats people play: the server flags them in each bot's observation, and a seat handed to a bot after its person leaves stops counting. The Arena flags probe seats, and `evaluate --defend true` flags the defended seat whatever its policy; self-play recording does not flag the league seat yet, so the networks were trained without siding.
- With both a person and another bot among its rivals, Ultimate counts each human rival's weighted armies and income more and each bot rival's less, after the learned correction, so the network keeps reading the score it was trained on. With people alone there is no bot to side with, and the weights stay off.
- It changes nothing in duels, in games without people (AI-only rooms, the Arena against bots, self-play), or once the last person is out.
- Screen on seeds 1230000–1230999, the turtle probe in every seat against two generation-4 Ultimates (3,000 games; lower is better):

| Human weight | Bot weight | Probe wins | Rate | Normal 95% | Unfinished |
| ---: | ---: | ---: | ---: | --- | ---: |
| 1 | 1 (generation 4 as it was) | 196 | 6.5% | 5.6%–7.4% | 0 |
| 2 | 1 | 59 | 2.0% | 1.4%–2.5% | 0 |
| 4 | 1 | 16 | 0.5% | 0.3%–0.8% | 23 |
| 3 | 0.5 | 13 | 0.4% | 0.2%–0.7% | 4 |

- Confirmation on unused seeds, with three stand-ins in the human seat, each in every seat against two generation-4 Ultimates:

| Human seat | Seeds | Generation 4 as it was | Weights 2 and 1 | Weights 3 and 0.5 |
| --- | --- | --- | --- | --- |
| Turtle probe | 1240000–1240999 | 169 / 3,000, 5.6% (4.8%–6.5%) | 67, 2.2% (1.7%–2.8%) | 9, 0.3% (0.1%–0.5%) |
| Master | 1250000–1250499 | 225 / 1,500, 15.0% (13.2%–16.8%) | — | 7, 0.5% (0.1%–0.8%) |
| Ultimate as it was | 1260000–1260499 | not finished | — | 4, 0.3% (0.0%–0.5%) |

- Intervals are the 95% normal interval over seed blocks. Unfinished games: 1 at weights 2 and 1, 5 at 3 and 0.5 on the turtle row, none in the other finished runs. The unweighted run with Ultimate in the human seat, three Ultimates that often stall, had not finished at publication.
- Weights 3 and 0.5 shipped. The screen and the confirmation passed the weights explicitly to a build where they were not yet the default; the evaluation below plays the default.
- The unfinished games are stand-offs at the 200-round limit. In the screen's four at weights 3 and 0.5, whose final boards were logged, the probe held 5 territories behind about 900 armies, one bot 30–34 territories, and the other was squeezed to 3–7 territories with a similar stack. The scripted probe never breaks out and no bot risks its stack; a person would break such a stand-off.
- `evaluate --opponent turtle --defend true` takes `--human-weight` and `--bot-weight` to screen other weights, and lists each unfinished game's final board.

### Multiplayer evaluation

- The bundled model (generation 4) and the rival weights were frozen before seeds 1270000–1276099 were played, from commit `ddc01be`; the probe or the candidate took every seat in turn, and the Arena seats the probe as the human.
- The generation-1 column is the previous release (generation 1, no rival weights), evaluated on seeds 980000–985149; the Master rows involve no Ultimate and keep their earlier seeds.

| Players | Match | Seeds | Wins / games | Rate | Hoeffding 95% | Generation 1 | Fair share |
| ---: | --- | --- | ---: | ---: | --- | ---: | ---: |
| 3 | turtle against two Masters | 970000–970299 | 291 / 900 | 32.3% | 24.5%–40.2% | — | 33.3% |
| 3 | turtle against two Ultimates | 1270000–1270999 | 8 / 3,000 | 0.3% | 0.0%–4.6% | 6.4% | 33.3% |
| 3 | Ultimate against two Masters | 1272000–1272299 | 824 / 900 | 91.6% | 83.7%–99.4% | 84.8% | 33.3% |
| 3 | Ultimate against two Experts | 1273000–1273149 | 411 / 450 | 91.3% | 80.2%–100.0% | 84.2% | 33.3% |
| 4 | turtle against three Masters | 972000–972149 | 214 / 600 | 35.7% | 24.6%–46.8% | — | 25.0% |
| 4 | turtle against three Ultimates | 1274000–1274149 | not finished | — | — | 14.5% | 25.0% |
| 4 | Ultimate against three Masters | 1275000–1275149 | 573 / 600 | 95.5% | 84.4%–100.0% | 89.0% | 25.0% |
| 6 | turtle against five Ultimates | 1276000–1276099 | not finished | — | — | 12.2% | 16.7% |

- The previous Ultimate decided exactly as Master until the first elimination, so the Master rows are close to its baseline; the exact previous preset (`ExpertTuning.Master with { Valuation = ValueModel.Ultimate }`, seated with the Arena's seeding and limits), replayed on the same seeds outside the Arena, gave the probe 31.7% (285 of 900) in three-player and 36.2% (217 of 600) in four-player games.
- In three-player games the probe fell from its fair share to under a hundredth of it, and Ultimate went from a third of the games against two Masters to eleven in twelve. Of the 3,000 probe games, 4 reached the round limit.
- The multiplayer model saw only three-player games in training. The four- and six-player probe runs for generation 4 had not finished at publication, on a shared machine; their seeds stay reserved for them, and the generation-1 column holds the previous release's rates.
- Each report's totals are recorded in [ultimate-results.json](ultimate-results.json) under `multiplayer`; the duel evaluation above is unchanged, and seeds 900000–900049 of its Master report replayed identically with this source (100 of 100 matches).

```sh
dotnet run --project tools/Risk.Arena -c Release -- \
  --seeds 1000 --first-seed 1270000 --players 3 \
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
- Generation 4 onward use `multi-board-v2` (706 features), which adds what happens next: per territory, whether the next player to move owns it, the chance the strongest adjacent enemy stack takes an own territory once its owner adds forecast reinforcements and card trades, and the same chance for an own stack against an enemy territory; then the next mover's totals and the summed forecast threat and opportunity. Like the rest, it reads public card counts, never the cards.
- `multi-board-v3` adds what each rival is building toward: territories that alone keep a rival from a continent, continents a rival may complete next turn, and the forecast threat to own complete continents. Fitted on generations 3 and 4's games, it won 21.2% of about 2,440 games against two generation-4 Ultimates (seeds from 1210000, 527 unfinished, stopped early), so it was not adopted.
- Training is the duel recipe applied to league self-play: every fourth game seats the current Ultimate everywhere, the others give one rotating seat to the turtle probe, Master or Expert; only Ultimate's afterstates are recorded while more than two players remain, labelled with its outcome and fitted by TD(λ) bootstrapped from the previous model (or the calibrated hand score for the first generation), with λ = 0.7 for generations 1–2 and 0.9 from generation 3.
- One command runs the loop; each promoted candidate becomes the next generation's learner and bootstrap, so the bot keeps improving until a candidate fails the gate:

```sh
dotnet run --project tools/Risk.Learning -c Release -- improve --players 3 --first-seed FIRST --games 20000 \
  --output artifacts/learning/genN --generations 4 --window 2 --lambda 0.9 --features multi-board-v2 --scale 0.75 \
  --reference src/Risk.Sim/Learning/ultimate-multiplayer-model.json --promote src/Risk.Sim/Learning/ultimate-multiplayer-model.json
```

- `--model` names the learner (default: the bundled model), whose encoding and scale the candidate keeps unless `--features` or `--scale` says otherwise; each generation goes to the next directory (gen3, gen4, …) and starts at the seeds after the previous one: first seed + games + 1,300 (recording, then the gate's 1,000 strength and 300 probe seeds).
- `--window W` fits each generation on its own games plus the previous W − 1 generations' recordings, as AlphaZero trains on a window of recent games; `--extra-data` supplies earlier recordings, all fitted with the first generation whatever the window.
- The gate plays the candidate against copies of the current model: it must win seat-balanced on unused seeds with the 95% normal lower bound over seed blocks above the fair share, and the turtle probe must not be demonstrably stronger against it than against the reference model on the same seeds (the probe's normal lower bound against the candidate stays at or below its rate against the reference). The probe plays the defended seat as the human, so both sides of that check side against it. A promoted candidate is copied to the promotion path; rebuild to play it.
- `--reference` fixes the probe's reference, normally the shipped model; without it each generation is compared with its current model, so small probe regressions can compound. `--probe-seeds` sets the probe's size (default 300; turtle games rarely stall, so 1,000 is cheap and narrows the check to about a point).
- `--league` sets the recorded games' opponent for each seed modulo four (default `learners,turtle,master,expert`, where `learners` seats Ultimate everywhere). Learner-only games stall more each generation — 3% of them with gen3 learning, 26% with gen4 and 47% with gen5, making 48.5% of the rows gen6 recorded — while games with a turtle almost never stall, so `turtle,turtle,master,expert` trains on the game the turtle probe measures, one turtle against two Ultimates, instead of on stalemates.
- Generations 1–3 were gated on the conservative Hoeffding bound instead. Neighbouring generations differ by a few points, inside its ±4.3-point margin at 1,000 seeds, so it rejected generation 3 at 36.0%; the normal interval is about 40% as wide, and published evaluations still report both.
- `improve` refuses an output directory that already holds a finished generation, and its data file is named by encoding, learner and seed range, so an interrupted run resumes only the same games played by the same model.
- Seeds from 1,000,000 upward are reserved for these generations; each generation's evaluation seeds follow its recording seeds, so they are never played before the candidate is frozen.
- What it cannot do: learn from the few games played against one person, invent plans the search never proposes, or escape the dice; gains per generation are expected to shrink, as the duel generations did.

### Generations

| Generation | Recorded games (seeds) | Afterstates | Held-out log-loss, hand score → model | Against two current Ultimates (seeds) | Turtle against candidate / current (seeds) | Promoted |
| ---: | --- | ---: | --- | --- | --- | --- |
| 1 | 20,000 (1000000–1019999) | 829,279 | 0.5782 → 0.5538 | 72.8% of 3,000, 68.5%–77.1% (1020000–1020999) | 6.9% / 11.2% of 900 (1021000–1021299) | yes |
| 2 | 20,000 (1021300–1041299) | 913,537 | 0.5886 → 0.5606 | 50.1% of 3,000, 45.8%–54.4% (1041300–1042299) | 5.0% / 6.9% of 900 (1042300–1042599) | yes |
| 3 | 10,000 (1042600–1052599) | 563,583 | 0.5946 → 0.5642 | 36.0% of 3,000, 31.7%–40.3% (1052600–1053599) | 5.0% / 7.0% of 900 (1053600–1053899) | no; adopted later |
| 4 | 10,000 (1054900–1064899) | 751,312 | 0.6167 → 0.5884 | 57.2% of 3,000, normal 55.4%–59.0% (1064900–1065899) | 5.3% / 6.4% of 900 (1065900–1066199) | yes; shipped |
| 5 | 10,000 (1066200–1076199) | 1,138,415 | 0.6566 → 0.6270 | 48.3% of 3,000, normal 46.6%–50.1% (1076200–1077199) | 6.1% / 5.4% of 900 (1077200–1077499) | yes; withdrawn |
| 6 | 10,000 (1077500–1087499) | 1,449,639 | 0.6568 → 0.6647 | gate stopped | — | no |

- Generation 1 ran from commit `8314694`, a build with no multiplayer model, so its learner and bootstrap were the hand-written terms alone; its command is recorded in [ultimate-results.json](ultimate-results.json).
- Its validation rows were every tenth seed, which by the league's rotation held out only all-Ultimate and Master games; later generations hold out every tenth block of four seeds per player, so each league opponent is represented in every seat.
- The log-loss is against final outcomes, so lower means the model predicts who wins better than the hand score does.
- Generation 2 ran from commit `1675a27` with generation 1 as learner and bootstrap, and is the learner of the chained run that follows; later generations start at seed 1042600.
- From generation 3 each fit also read the previous generation's recording (`--window 2`); the afterstates column counts the generation's own.
- Generations 1–3 report the Hoeffding interval, 4–5 the normal interval over seed blocks that gated them.
- Generation 3 failed the Hoeffding gate, but a confirmation on seeds 1053900–1054899 scored 35.5% (normal 33.8%–37.1%), and at correction scale 0.75 it won 37.2% against two copies of itself at 0.5 (seeds 1110000–1110599). That model became generation 4's learner, and generation 4 was the first fitted on `multi-board-v2`.
- Generation 5 passed, then was withdrawn by the fixed yardstick on seeds 1220000–1220499 (1,500 games each): the turtle won 7.8% against it and 7.3% against generation 6, against 5.5% for generation 4. The gate now checks the probe against a fixed reference (`--reference`) for that reason.
- Generations 5 and 6 learned largely from stalemates (see `--league` above): 23% of the rows fitted for generation 5 and 42% for generation 6 came from stalled games. Generation 6's network predicted outcomes worse than the hand score alone, and its gate was stopped.
- Tried after generation 4, each on seeds 1100000–1111999 with generation 4 as bootstrap and none adopted:
  - Recording with `--league turtle,turtle,master,expert`: the candidate scored 25.8% against two generation-4 Ultimates, 677 of 3,000 games unfinished and counted as losses (it won 774 of the 2,323 finished, 33.3%), and the turtle won 7.6% against it, 6.4% against generation 4.
  - Wider networks on the same games: 128 and 256 hidden units fitted slightly better (held-out log-loss 0.5637 and 0.5648 against 0.5661 for 64 and 0.6128 for the hand score), yet the turtle won 8.7% and 8.2% against them; three 64-unit fits averaged gave 6.9%.

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

## Mobile game rules

- The SMG bridge plays the mobile game's regular public rooms: four players, Fixed cards, [balanced blitz dice](balanced-blitz.md).
- On attack-phase boards from a ranked public game, Ultimate attacked at 8 of 15 decisions, while Master and Expert attacked at all 15. It refused captures with 92–100% odds.
- The cause is the generation 4 multiplayer model (trained on three-player true-dice games) together with frontier defence. The dice odds are not the cause: with the model removed, the same planner attacked at all 15.
- `smg` (`SmgStrategy`, `smg-learned-planner`) is Ultimate's planner and duel model with its own multiplayer model, trained under those rules. It is never offered in the lobby.
- The model trains with `improve --track smg --players 4 --model hand`: league self-play, the fit and both gate checks all run under balanced blitz, with a fair share of 25%.
- The bundled `src/Risk.Sim/Learning/smg-multiplayer-model.json` is generation 4 of that chain (20,000 games per generation from seed 2,000,000). Each generation passed the gate against the one before:

| Generation | Against the previous learner | Turtle against it | Against three Masters (seeds 3,000,000+200) |
|---|---|---|---|
| Hand-written terms | – | – | 39.0% |
| 1 | 71.9% (normal 70.6–73.2%) | 1.3% | 76.4% (normal 73.4–79.4%) |
| 2 | 48.5% (normal 47.0–50.1%) | 0.9% | – |
| 3 | 49.8% (normal 48.2–51.4%) | 0.1% | – |
| 4 | 47.6% (normal 46.0–49.2%) | 0.3% | 82.9% (normal 80.4–85.4%) |

- Against three Masters, generation 4 attacked in 99% of its turns, and in 99% of the turns that offered an attack with at least 80% odds. On the ranked game's boards it attacked at 14 of 15 decisions.
- Each generation plays longer games against copies of itself: 32 rounds for generation 1 and 99 for generation 4, with 9 of 40 such games unfinished at round 200. Against Masters its games stay short (14 rounds) and all finished.
- The chain was stopped during generation 5, whose recording had slowed to about 14 hours.

## Limits

- The duel model was trained and evaluated only on two-player Classic games with Fixed cards and automatic setup; the multiplayer model was trained only on three-player games with the same rules; generation 4 was evaluated in larger games only against three Masters, generation 1 also against the probe in four- and six-player games.
- Frontier defence and threat weighting were selected and evaluated in three-player Fixed-card automatic-setup games against the turtle probe and Master; four- to six-player games were evaluated once each, progressive cards and manual setup not at all.
- The rival weights were selected against the turtle probe in three-player games and confirmed with Master in the human seat (with an unweighted Ultimate there, only the weighted rate was measured); the bots visibly focus a person, and no real player has been measured.
- With the rival weights, 4–5 of 3,000 games against the scripted turtle stalled at the 200-round limit; real games have no limit.
- Results measure strength against the bundled bots and one scripted human style, not against skilled humans.
