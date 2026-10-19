# Balanced blitz dice

- The mobile game's public rooms mostly play **balanced blitz**: every attack is one all-in blitz, settled from a skewed version of the exact blitz odds instead of rolled dice.
- `DiceMode.BalancedBlitz` in `GameOptions.Dice` plays it in Risk.Sim; `DiceMode.TrueRandom` (the default) keeps real dice.
- Our lobby refuses balanced-blitz rooms. The rule exists for the SMG preset (`smg`), the Arena (`--dice balancedblitz`) and the learning tools (`--track smg`).

## Exact blitz odds

- `BlitzCalculator.Calculate(A, D)`: the distribution of an all-in blitz of A attackers against D defenders, with six-sided dice, up to three attacking and two defending, ties to the defender.
- `attackLoss[i < A]` is P(the attacker wins having lost i); `attackLoss[A]` is P(the defender wins).
- `defendLoss[j < D]` is P(the defender wins having lost j); `defendLoss[D]` is P(the attacker wins).
- Example: 3 v 2 gives the attacker a 65.6% win chance.

## Balancing

Four steps, in this order, on copies of both arrays. W is the attacker's win chance, L the defender's.

1. **Win chance cutoff (5%)**: a side whose win chance is at most 5% never wins. Its win chance becomes 0 and the other side's 1; the other side's outcomes are scaled to sum to 1.
2. **Win chance power (1.3)**: W and L are raised to 1.3 and renormalised, which pushes the favourite further ahead. Each side's outcomes are scaled to the other side's new win chance.
3. **Outcome cutoff (10%)**: the outcomes are laid on one line, from the attacker's best (won, lost nothing) to the defender's best. The first and last 10% of probability are removed from both ends. W and L are then each side's remaining share.
4. **Outcome power (1.8)**: every outcome is raised to 1.8, and each side is rescaled to its win chance, which concentrates each side on its likeliest results.

- The constants are single-precision values widened to double, and sums run left to right, so the result matches the game exactly.
- Example: 3 v 2 becomes a 74.8% win; 10 v 10 becomes 60.9% and can no longer end with the attacker losing fewer than three troops.

## Sampling

- One uniform u in [0, 1) settles the battle: 53 random bits from two draws of the game's random source.
- u ≤ W: the attacker wins. Its losses are the first outcome whose running total reaches u.
- Otherwise the defender wins, with u − W picked among its outcomes the same way.
- The attacker always fights to the end of its committed troops; no dice are shown (`BattleResult` dice arrays are empty).
- After a capture, the move takes in at least min(3, surviving committed attackers), since there is no final roll to size it.
- Battles of 2,001 or more units in total are estimated by the mobile game and keep real dice here.

## Planning odds

- `BattleOdds.Estimate(attackers, defenders, options)` reads the balanced distribution when the observation's rules use balanced blitz.
- Every planner and learned feature goes through `PositionEvaluator.Odds`, so bots and models see the rules they play.
- A model fitted on one dice rule describes boards through that rule's odds; train and play it under the same rule.

## Validation

- Golden values for 3 v 2, 2 v 1 and 10 v 10 match the SMG bridge's port, which was checked against an independent Python port.
- The SMG bridge also checked 99 attack results captured from two balanced-blitz public games. All 99 are possible under this rule, and they fit it far better than true random dice (log-likelihood about −76 against −91).
