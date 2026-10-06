import argparse
from pathlib import Path

from .records import collect
from .train import train
from .evaluate import evaluate


def positive(value):
    result = int(value)
    if result < 1:
        raise argparse.ArgumentTypeError("Use a positive integer")
    return result


def nonnegative(value):
    result = int(value)
    if result < 0:
        raise argparse.ArgumentTypeError("Use a nonnegative integer")
    return result


parser = argparse.ArgumentParser(description="Expert imitation and masked PPO; all games use the .NET rules engine")
commands = parser.add_subparsers(dest="command", required=True)
record = commands.add_parser("collect")
record.add_argument("--output", type=Path, default=Path("/artifacts/dataset"))
record.add_argument("--seeds", type=positive, default=48)
record.add_argument("--first-seed", type=nonnegative, default=4000)
record.add_argument("--validation-seeds", type=positive, default=12)
record.add_argument("--validation-first-seed", type=nonnegative, default=4500)
record.set_defaults(run=collect)
training = commands.add_parser("train")
training.add_argument("--dataset", type=Path, default=Path("/artifacts/dataset"))
training.add_argument("--output", type=Path, default=Path("/artifacts/run-1"))
training.add_argument("--epochs", type=positive, default=20)
training.add_argument("--steps", type=positive, default=16384)
training.add_argument("--seed", type=nonnegative, default=123)
training.add_argument("--first-seed", type=nonnegative, default=6000)
training.add_argument("--width", type=int, choices=(64, 128, 256, 512), default=64)
training.add_argument("--depth", type=int, choices=(1, 2, 3), default=1)
training.set_defaults(run=train)
evaluation = commands.add_parser("evaluate")
evaluation.add_argument("--model", type=Path, required=True)
evaluation.add_argument("--output", type=Path, required=True)
evaluation.add_argument("--opponent", choices=("easy", "normal", "hard", "expert"), default="expert")
evaluation.add_argument("--seeds", type=positive, default=32)
evaluation.add_argument("--first-seed", type=nonnegative, default=9000)
evaluation.add_argument("--random-seed", type=nonnegative, default=123)
evaluation.set_defaults(run=evaluate)
args = parser.parse_args()
if args.command == "collect" and (args.seeds > 500 or args.validation_seeds > 100):
    parser.error("Use at most 500 training seed blocks and 100 validation seed blocks per dataset")
if args.command == "evaluate" and args.seeds > 500:
    parser.error("Use at most 500 evaluation seed blocks per batch")
if args.command == "collect":
    training_range = set(range(args.first_seed, args.first_seed + args.seeds))
    if training_range.intersection(range(args.validation_first_seed, args.validation_first_seed + args.validation_seeds)):
        parser.error("Training and validation seed ranges must be disjoint")
args.run(args)
