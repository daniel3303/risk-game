import json
import random
import time

import numpy as np
import torch
from sb3_contrib import MaskablePPO
from stable_baselines3.common.logger import configure

from .environment import RiskEnv
from .imitation import clone, metrics
from .policy import CandidatePolicy
from .records import load_records, provenance, sha256, write_json
from .validation import disjoint_training_seeds


def train(args):
    if any((args.output / name).exists() for name in ("imitation.zip", "rl.zip", "training.json")):
        raise ValueError("Use a new output directory to preserve existing checkpoints")
    args.output.mkdir(parents=True, exist_ok=True)
    random.seed(args.seed)
    np.random.seed(args.seed)
    torch.manual_seed(args.seed)
    torch.set_num_threads(2)
    torch.use_deterministic_algorithms(True)
    dataset = json.loads((args.dataset / "dataset.json").read_text())
    verify_dataset(args.dataset, dataset)
    disjoint_training_seeds(args.first_seed, dataset)
    training = load_records(args.dataset / "expert-train.jsonl.gz", dataset["training"]["schema"])
    validation = load_records(args.dataset / "expert-validation.jsonl.gz", dataset["validation"]["schema"])
    start = time.monotonic()
    with_env = RiskEnv(first_seed=args.first_seed)
    try:
        model = MaskablePPO(CandidatePolicy, with_env, n_steps=512, batch_size=64, n_epochs=4, learning_rate=1e-4,
                            gamma=0.995, ent_coef=0.005, target_kl=0.02, seed=args.seed, device="cpu", verbose=1,
                            policy_kwargs={"width": args.width, "depth": args.depth})
        policy = {"width": args.width, "depth": args.depth,
                  "parameters": sum(parameter.numel() for parameter in model.policy.parameters())}
        initial = metrics(model.policy, validation)
        history = clone(model, training, validation, args.epochs, args.output / "imitation")
        imitation = MaskablePPO.load(args.output / "imitation.zip", device="cpu")
        cloned = metrics(imitation.policy, validation)
        cloned_training = metrics(imitation.policy, training)
        del model, training, validation
        steps = reinforce(args, imitation)
        write_json(args.output / "training.json", {"provenance": provenance(), "datasetSha256": sha256(args.dataset / "dataset.json"),
                   "dataset": dataset,
                   "randomSeed": args.seed, "trainingSeeds": [args.first_seed, args.first_seed + 999], "requestedRlSteps": args.steps,
                   "actualRlSteps": steps, "algorithm": "MaskablePPO", "reward": "terminal win +1, loss -1, unfinished 0",
                   "ppo": {"nSteps": 512, "batchSize": 64, "epochs": 4, "learningRate": 0.0001, "gamma": 0.995, "entropyCoefficient": 0.005, "targetKl": 0.02},
                   "opponents": ["normal", "hard", "expert", "frozen imitation checkpoint"],
                   "policy": policy, "elapsedSeconds": time.monotonic() - start,
                   "untrainedValidation": initial, "imitationValidation": cloned, "imitationTraining": cloned_training,
                   "imitationHistory": history, "checkpoints": {name: sha256(args.output / (name + ".zip")) for name in ("imitation", "rl")}})
    finally:
        with_env.close()


def verify_dataset(directory, dataset):
    current = provenance()
    for key in ("rulesAssemblySha256", "bridgeAssemblySha256"):
        if current[key] != dataset["provenance"][key]:
            raise ValueError("The dataset's rules/schema differ from this training image; recollect it")
    for name in ("training", "validation"):
        filename = "expert-train.jsonl.gz" if name == "training" else "expert-validation.jsonl.gz"
        if sha256(directory / filename) != dataset[name]["sha256"]:
            raise ValueError("The recorded dataset checksum does not match")


def reinforce(args, imitation):
    env = RiskEnv(first_seed=args.first_seed, opponents=("normal", "hard", "expert", "external"), frozen=imitation)
    try:
        model = MaskablePPO.load(args.output / "imitation.zip", env=env, device="cpu")
        model.set_logger(configure(str(args.output), ["stdout", "csv"]))
        model.learn(total_timesteps=args.steps)
        model.save(args.output / "rl")
        return model.num_timesteps
    finally:
        env.close()
