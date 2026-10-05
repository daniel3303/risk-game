using Risk.Sim.Models;
namespace Risk.Sim.Rules;

public static class CardRules
{
    public static TerritoryCard Card(int id) => new(id, id < 42 ? id : -1, id < 42 ? (CardSymbol)(id % 3) : CardSymbol.Wild);
    public static int ProgressiveBonus(int trades) => trades < 5 ? 4 + trades * 2 : 15 + (trades - 5) * 5;

    public static int FixedBonus(IEnumerable<int> cards)
    {
        var symbols = cards.Select(id => Card(id).Symbol).ToArray();
        if (symbols.Length != 3) return 0;
        var regular = symbols.Where(s => s != CardSymbol.Wild).ToArray();
        if (regular.Distinct().Count() == regular.Length) return 10;
        if (regular.Distinct().Count() != 1) return 0;
        return 4 + (int)regular[0] * 2;
    }

    public static int[] FindSet(IEnumerable<int> hand)
    {
        var cards = hand.ToArray();
        for (var i = 0; i < cards.Length - 2; i++)
            for (var j = i + 1; j < cards.Length - 1; j++)
                for (var k = j + 1; k < cards.Length; k++)
                    if (FixedBonus([cards[i], cards[j], cards[k]]) > 0) return [cards[i], cards[j], cards[k]];
        return [];
    }

    public static void Trade(GameState state, GameOptions options, GameCommand command)
    {
        var hand = state.Players[state.CurrentPlayer].Cards;
        var cards = command.Cards;
        if (cards == null || cards.Length != 3 || cards.Distinct().Count() != 3 || cards.Any(id => !hand.Contains(id)))
            throw new RuleException("Select three cards from your hand.");
        var fixedBonus = FixedBonus(cards);
        if (fixedBonus == 0) throw new RuleException("Trade three matching symbols, one of each, or a set with a wild.");
        var owned = cards.Select(Card).Where(c => c.Territory >= 0 && state.Territories[c.Territory].Owner == state.CurrentPlayer).ToArray();
        var territory = command.BonusTerritory;
        if (territory >= 0 && !owned.Any(c => c.Territory == territory)) throw new RuleException("Choose an owned territory depicted in this set.");
        if (territory < 0 && owned.Length > 0) territory = owned[0].Territory;
        var bonus = options.Cards == CardMode.Fixed ? fixedBonus : ProgressiveBonus(state.Trades);
        foreach (var card in cards) { hand.Remove(card); state.Deck.Enqueue(card); }
        if (territory >= 0) state.Territories[territory].Troops += 2;
        state.Reinforcements += bonus;
        state.Trades++;
        state.Record($"{state.Players[state.CurrentPlayer].Name} trades cards for {bonus} troops.");
    }
}
