using Risk.Sim.Models;
namespace Risk.Sim.Rules;

public static class CombatRules
{
    public static BattleResult Attack(GameState state, TerritoryState from, TerritoryState to, GameCommand command, IRandomSource random)
    {
        if (to.Owner == state.CurrentPlayer || to.Owner < 0 || from.Troops < 2) throw new RuleException("Attack an adjacent enemy with at least two troops.");
        var committed = command.Count == 0 ? from.Troops - 1 : command.Count;
        if (committed < 1 || committed >= from.Troops || command.Dice is < 1 or > 3)
            throw new RuleException("Choose valid troops and one to three attack dice.");
        if (!command.Blitz && command.Dice > committed) throw new RuleException("Each attacking die needs one committed troop.");
        var attackerLosses = 0;
        var defenderLosses = 0;
        int[] attackDice = [], defendDice = [];
        do
        {
            var dice = command.Blitz ? Math.Min(3, committed) : command.Dice;
            attackDice = Roll(dice, random);
            defendDice = Roll(Math.Min(2, to.Troops), random);
            for (var i = 0; i < Math.Min(attackDice.Length, defendDice.Length); i++)
            {
                if (attackDice[i] > defendDice[i]) { to.Troops--; defenderLosses++; }
                else { from.Troops--; committed--; attackerLosses++; }
            }
        } while (command.Blitz && committed > 0 && to.Troops > 0);
        if (to.Troops == 0)
        {
            state.Capture = new(from.Id, to.Id, attackDice.Length, from.Troops - 1);
            state.Phase = Phase.Occupy;
        }
        return new(from.Id, to.Id, attackerLosses, defenderLosses, attackDice, defendDice, to.Troops == 0);
    }

    public static int[] Roll(int count, IRandomSource random) => Enumerable.Range(0, count).Select(_ => random.Next(6) + 1).OrderDescending().ToArray();

    public static void Occupy(GameState state, int count)
    {
        var capture = state.Capture;
        if (capture == null || count < capture.Minimum || count > capture.Maximum) throw new RuleException("Move at least the final attack's dice count and leave one troop behind.");
        var from = state.Territories[capture.From];
        var to = state.Territories[capture.To];
        var defeated = state.Players[to.Owner];
        from.Troops -= count;
        to.Troops = count;
        to.Owner = state.CurrentPlayer;
        state.ConqueredThisTurn = true;
        state.Capture = null;
        state.Phase = Phase.Attack;
        if (state.Territories.Any(t => t.Owner == defeated.Id)) return;
        defeated.Eliminated = true;
        var player = state.Players[state.CurrentPlayer];
        player.Cards.AddRange(defeated.Cards);
        defeated.Cards.Clear();
        state.Record($"{player.Name} eliminates {defeated.Name} and inherits their cards.");
        if (TurnRules.CheckWinner(state)) return;
        if (player.Cards.Count > 5) { state.Phase = Phase.Draft; state.Reinforcements = 0; state.ResumeAttack = true; }
    }
}
