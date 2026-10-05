using Risk.Sim.Models;
using Risk.Sim.Rules;
namespace Risk.Sim;

public sealed class Game
{
    private readonly IRandomSource random;
    public WorldMap Map { get; }
    public GameOptions Options { get; }
    public GameState State { get; }

    public Game(WorldMap map, string[] players, GameOptions options, IRandomSource random)
    {
        Map = map;
        Options = options;
        this.random = random;
        State = GameSetup.Create(map, players, options, random);
        if (State.Phase == Phase.Draft) TurnRules.Begin(Map, State);
    }

    public void Apply(int player, GameCommand command)
    {
        if (command == null || !Enum.IsDefined(command.Kind)) throw new RuleException("Unknown command.");
        if (player < 0 || player >= State.Players.Length || State.Players[player].Eliminated || State.Phase == Phase.Finished)
            throw new RuleException("This player cannot act.");
        if (command.Kind == CommandKind.Surrender) { Surrender(player); return; }
        if (player != State.CurrentPlayer) throw new RuleException("Wait for your turn.");
        switch (command.Kind)
        {
            case CommandKind.Claim:
                GameSetup.Claim(State, Territory(command.To));
                break;
            case CommandKind.Place:
                Place(command);
                break;
            case CommandKind.Trade:
                Require(Phase.Draft);
                CardRules.Trade(State, Options, command);
                break;
            case CommandKind.Attack:
                Attack(command);
                break;
            case CommandKind.Occupy:
                Require(Phase.Occupy);
                CombatRules.Occupy(State, command.Count);
                break;
            case CommandKind.EndAttack:
                Require(Phase.Attack);
                State.Phase = Phase.Fortify;
                break;
            case CommandKind.Fortify:
                Require(Phase.Fortify);
                Fortification.Move(Map, State, Owned(command.From), Owned(command.To), command.Count);
                TurnRules.End(Map, State);
                break;
            case CommandKind.EndTurn:
                Require(Phase.Fortify);
                TurnRules.End(Map, State);
                break;
            default:
                throw new RuleException("Unknown command.");
        }
    }

    private void Place(GameCommand command)
    {
        var territory = Owned(command.To);
        if (State.Phase == Phase.Setup)
        {
            GameSetup.Place(State, territory, command.Count);
            if (State.Phase == Phase.Draft) TurnRules.Begin(Map, State);
            return;
        }
        Require(Phase.Draft);
        TurnRules.Place(State, territory, command.Count);
    }

    private void Attack(GameCommand command)
    {
        Require(Phase.Attack);
        var from = Owned(command.From);
        var to = Territory(command.To);
        if (!Map.Territories[from.Id].Neighbors.Contains(to.Id)) throw new RuleException("These territories do not share a border.");
        State.Battle = CombatRules.Attack(State, from, to, command, random);
        State.Record($"{State.Players[State.CurrentPlayer].Name} attacks {Map.Territories[to.Id].Name}: −{State.Battle.AttackerLosses} / −{State.Battle.DefenderLosses}.");
    }

    private void Surrender(int player)
    {
        if (State.Phase is Phase.Claim or Phase.Setup or Phase.Occupy) throw new RuleException("Finish setup or capture before surrendering.");
        State.Players[player].Eliminated = true;
        foreach (var card in State.Players[player].Cards) State.Deck.Enqueue(card);
        State.Players[player].Cards.Clear();
        // Surrendered armies remain passive and can be conquered normally.
        State.Record($"{State.Players[player].Name} surrenders.");
        if (TurnRules.CheckWinner(State)) return;
        if (State.CurrentPlayer == player) { TurnRules.Advance(State); TurnRules.Begin(Map, State); }
    }

    private TerritoryState Territory(int id) => id >= 0 && id < State.Territories.Length ? State.Territories[id] : throw new RuleException("Choose a valid territory.");
    private TerritoryState Owned(int id)
    {
        var territory = Territory(id);
        if (territory.Owner != State.CurrentPlayer) throw new RuleException("Choose your own territory.");
        return territory;
    }
    private void Require(Phase phase)
    {
        if (State.Phase != phase) throw new RuleException($"This action requires the {phase} phase.");
    }
}
