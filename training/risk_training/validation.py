def overlaps(first, count, other_first, other_count):
    return first < other_first + other_count and other_first < first + count


def disjoint_training_seeds(first, dataset):
    for split in ("training", "validation"):
        info = dataset[split]
        if overlaps(first, 1000, info["firstSeed"], info["seeds"]):
            raise ValueError("RL seeds must be disjoint from imitation training and validation seeds")


def disjoint_evaluation_seeds(first, count, training, dataset):
    if overlaps(first, count, training["trainingSeeds"][0], 1000):
        raise ValueError("Evaluation seeds overlap the RL training bank")
    for split in ("training", "validation"):
        info = dataset[split]
        if overlaps(first, count, info["firstSeed"], info["seeds"]):
            raise ValueError("Evaluation seeds overlap the imitation dataset")
