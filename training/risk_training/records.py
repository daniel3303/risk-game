import gzip
import hashlib
import importlib.metadata
import json
from pathlib import Path

import numpy as np

from .bridge import Bridge
from .environment import pack, mask


def sha256(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n")


def provenance():
    source = hashlib.sha256()
    for path in sorted(Path("/training").rglob("*.py")):
        source.update(str(path.relative_to("/training")).encode())
        source.update(path.read_bytes())
    return {"pythonSourceSha256": source.hexdigest(), "rulesAssemblySha256": sha256("/bridge/Risk.Sim.dll"),
            "bridgeAssemblySha256": sha256("/bridge/Risk.Training.dll"),
            "packages": {name: importlib.metadata.version(name) for name in ("torch", "sb3-contrib", "stable-baselines3", "gymnasium", "numpy")}}


def collect_split(path, seeds, first_seed):
    rows = 0
    outcomes = []
    opponents = ("normal", "hard", "expert")
    with Bridge() as bridge, gzip.open(path, "wt") as output:
        for seed in range(first_seed, first_seed + seeds):
            for seat in range(2):
                opponent = opponents[(seed - first_seed) % len(opponents)]
                frame = bridge.request(operation="reset", seed=seed, seat=seat, opponent=opponent)
                game_rows = []
                while not frame["terminated"] and not frame["truncated"]:
                    decision = bridge.request(operation="teacher")
                    game_rows.append({"seed": seed, "seat": seat, "observation": frame["observation"],
                                      "teacher": decision["teacher"], "decision": decision["decision"]})
                    frame = bridge.request(operation="step", action=decision["teacher"])
                for row in game_rows:
                    row["outcome"] = frame["reward"]
                    output.write(json.dumps(row, separators=(",", ":")) + "\n")
                rows += len(game_rows)
                outcomes.append({"seed": seed, "seat": seat, "opponent": opponent, "winner": frame["winner"], "finished": frame["terminated"]})
            print(f"Collected seed {seed}: {rows} Expert decisions", flush=True)
    return {"firstSeed": first_seed, "seeds": seeds, "games": seeds * 2, "decisions": rows,
            "schema": bridge.schema, "sha256": sha256(path), "outcomes": outcomes}


def collect(args):
    args.output.mkdir(parents=True, exist_ok=True)
    training = collect_split(args.output / "expert-train.jsonl.gz", args.seeds, args.first_seed)
    validation = collect_split(args.output / "expert-validation.jsonl.gz", args.validation_seeds, args.validation_first_seed)
    write_json(args.output / "dataset.json", {"provenance": provenance(), "training": training, "validation": validation})


def load_records(path, schema):
    observations, masks, labels, outcomes = [], [], [], []
    with gzip.open(path, "rt") as stream:
        for line in stream:
            row = json.loads(line)
            frame = {"observation": row["observation"]}
            observations.append(pack(frame, schema))
            masks.append(mask(frame, schema))
            labels.append(row["teacher"])
            outcomes.append(row["outcome"])
    if not labels:
        raise ValueError("The decision dataset is empty")
    return ({key: np.stack([obs[key] for obs in observations]) for key in ("state", "candidates")},
            np.stack(masks), np.asarray(labels), np.asarray(outcomes, np.float32))
