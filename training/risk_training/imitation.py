import numpy as np
import torch
from torch.nn import functional as F


def batch(data, indices):
    observation, masks, labels, outcomes = data
    return ({key: torch.as_tensor(value[indices]) for key, value in observation.items()},
            torch.as_tensor(masks[indices]), torch.as_tensor(labels[indices]), torch.as_tensor(outcomes[indices]))


def metrics(policy, data, batch_size=128):
    policy.set_training_mode(False)
    loss, correct, count = 0.0, 0, 0
    with torch.no_grad():
        for start in range(0, len(data[2]), batch_size):
            observation, masks, labels, _ = batch(data, slice(start, start + batch_size))
            distribution = policy.get_distribution(observation, action_masks=masks)
            loss -= distribution.log_prob(labels).sum().item()
            correct += (distribution.get_actions(deterministic=True) == labels).sum().item()
            count += len(labels)
    return {"crossEntropy": loss / count, "accuracy": correct / count, "decisions": count}


def clone(model, training, validation, epochs, path):
    optimizer = torch.optim.Adam(model.policy.parameters(), lr=1e-3)
    history = []
    best = float("inf")
    for epoch in range(epochs):
        model.policy.set_training_mode(True)
        order = np.random.permutation(len(training[2]))
        for start in range(0, len(order), 128):
            observation, masks, labels, outcomes = batch(training, order[start : start + 128])
            values, log_prob, _ = model.policy.evaluate_actions(observation, labels, action_masks=masks)
            loss = -log_prob.mean() + 0.2 * F.mse_loss(values.flatten(), outcomes)
            optimizer.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.policy.parameters(), 1.0)
            optimizer.step()
        score = metrics(model.policy, validation)
        history.append({"epoch": epoch + 1, **score})
        print(f"Imitation epoch {epoch + 1}: validation accuracy={score['accuracy']:.3f}, loss={score['crossEntropy']:.3f}", flush=True)
        if score["crossEntropy"] < best:
            best = score["crossEntropy"]
            model.save(path)
    return history
