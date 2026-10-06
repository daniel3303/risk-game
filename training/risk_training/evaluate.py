import math
import json

import torch
from sb3_contrib import MaskablePPO

from .environment import RiskEnv
from .records import provenance, sha256, write_json
from .validation import disjoint_evaluation_seeds


def evaluate(args):
    torch.set_num_threads(2)
    torch.manual_seed(args.random_seed)
    training = json.loads((args.model.parent / "training.json").read_text())
    disjoint_evaluation_seeds(args.first_seed, args.seeds, training, training["dataset"])
    model_hash = sha256(args.model)
    if model_hash not in training["checkpoints"].values():
        raise ValueError("This checkpoint does not match the recorded training run")
    for key in ("rulesAssemblySha256", "bridgeAssemblySha256"):
        if training["provenance"][key] != provenance()[key]:
            raise ValueError("Evaluation must use the same rules and feature schema as training")
    model = MaskablePPO.load(args.model, device="cpu")
    matches = []
    env = RiskEnv(opponents=(args.opponent,))
    try:
        for seed in range(args.first_seed, args.first_seed + args.seeds):
            for seat in range(2):
                observation, _ = env.reset(options={"game_seed": seed, "seat": seat, "opponent": args.opponent})
                terminated = truncated = False
                while not terminated and not truncated:
                    action, _ = model.predict(observation, deterministic=True, action_masks=env.action_masks())
                    observation, _, terminated, truncated, info = env.step(action)
                matches.append({"seed": seed, "seat": seat, "winner": info["winner"], "finished": terminated,
                                "rounds": info["round"], "actions": info["actions"]})
            print(f"Evaluated seed {seed}", flush=True)
    finally:
        env.close()
    report = summarize(matches, args.seeds)
    report.update({"modelSha256": sha256(args.model), "provenance": provenance(), "opponent": args.opponent,
                   "firstSeed": args.first_seed, "seeds": args.seeds, "players": 2, "cards": "fixed", "setup": "automatic",
                   "maxRounds": 100, "maxActions": 5000, "limitBoundary": "next learner decision", "opponentSafetyActions": 1000,
                   "deterministicPolicy": True, "humanOpponentsEvaluated": False, "matches": matches})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    write_json(args.output, report)
    print(f"Wins {report['wins']}/{report['games']}; unfinished {report['unfinished']}", flush=True)


def summarize(matches, seeds):
    wins = sum(match["finished"] and match["winner"] == match["seat"] for match in matches)
    unfinished = sum(not match["finished"] for match in matches)
    rate = wins / len(matches)
    margin = math.sqrt(math.log(40) / (2 * seeds))
    return {"games": len(matches), "wins": wins, "losses": len(matches) - wins - unfinished, "unfinished": unfinished,
            "winRate": rate, "lower95": max(0, rate - margin), "upper95": min(1, rate + margin),
            "intervalMethod": "Hoeffding bound on seed blocks; unfinished games are non-wins"}
