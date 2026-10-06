import gzip
import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
import torch
from sb3_contrib import MaskablePPO

from risk_training.environment import RiskEnv
from risk_training.evaluate import summarize
from risk_training.policy import CandidatePolicy
from risk_training.validation import disjoint_training_seeds, disjoint_evaluation_seeds
from risk_training.records import collect_split, sha256


class TrainingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        torch.set_num_threads(1)

    def test_policy_masks_padding_and_is_candidate_permutation_equivariant(self):
        env = RiskEnv()
        try:
            observation, _ = env.reset(options={"game_seed": 12, "seat": 0})
            model = MaskablePPO(CandidatePolicy, env, n_steps=8, batch_size=8, device="cpu")
            inputs, _ = model.policy.obs_to_tensor(observation)
            with torch.no_grad():
                original = model.policy.get_distribution(inputs, action_masks=env.action_masks()).distribution.probs
                count = int(env.action_masks().sum())
                order = np.arange(env.schema["capacity"])
                order[:count] = order[:count][::-1]
                swapped = {"state": observation["state"], "candidates": observation["candidates"][order]}
                inputs, _ = model.policy.obs_to_tensor(swapped)
                changed = model.policy.get_distribution(inputs, action_masks=env.action_masks()).distribution.probs
            self.assertTrue(torch.all(original[:, count:] == 0))
            torch.testing.assert_close(changed[:, :count], original[:, :count].flip(1))
        finally:
            env.close()

    def test_ppo_update_and_checkpoint_roundtrip_use_the_real_rules_process(self):
        env = RiskEnv(opponents=("easy",))
        try:
            model = MaskablePPO(CandidatePolicy, env, n_steps=8, batch_size=8, n_epochs=1, device="cpu", seed=9)
            before = {name: value.clone() for name, value in model.policy.state_dict().items()}
            model.learn(total_timesteps=16)
            self.assertTrue(any(not torch.equal(value, before[name]) for name, value in model.policy.state_dict().items()))
            observation, _ = env.reset(options={"game_seed": 13, "seat": 1})
            expected, _ = model.predict(observation, deterministic=True, action_masks=env.action_masks())
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "model.zip"
                model.save(path)
                loaded = MaskablePPO.load(path, device="cpu")
                actual, _ = loaded.predict(observation, deterministic=True, action_masks=env.action_masks())
                self.assertEqual(int(expected), int(actual))
        finally:
            env.close()

    def test_frozen_self_play_advances_the_opponent_and_returns_the_learners_turn(self):
        source = RiskEnv(opponents=("easy",))
        external = None
        try:
            frozen = MaskablePPO(CandidatePolicy, source, n_steps=8, batch_size=8, device="cpu")
            external = RiskEnv(opponents=("external",), frozen=frozen)
            external.reset(options={"game_seed": 14, "seat": 1})
            self.assertEqual(external.frame["player"], 1)
            self.assertGreater(external.frame["actions"], 0)
        finally:
            if external is not None:
                external.close()
            source.close()

    def test_time_limit_keeps_the_final_board_for_value_bootstrapping(self):
        env = RiskEnv()
        try:
            observation, _ = env.reset(options={"game_seed": 15, "seat": 0, "max_actions": 1})
            final, reward, terminated, truncated, _ = env.step(np.flatnonzero(env.action_masks())[0])
            self.assertFalse(terminated)
            self.assertTrue(truncated)
            self.assertEqual(reward, 0)
            self.assertFalse(np.array_equal(observation["state"], final["state"]))
        finally:
            env.close()

    def test_unfinished_games_count_as_nonwins_and_seats_share_one_seed_block(self):
        report = summarize([{"finished": True, "winner": 0, "seat": 0}, {"finished": False, "winner": -1, "seat": 1}], 1)
        self.assertEqual(report["wins"], 1)
        self.assertEqual(report["unfinished"], 1)
        self.assertEqual(report["winRate"], 0.5)
        self.assertEqual(report["lower95"], 0)

    def test_training_and_evaluation_cannot_reuse_heldout_seeds(self):
        dataset = {"training": {"firstSeed": 4000, "seeds": 48}, "validation": {"firstSeed": 4500, "seeds": 12}}
        with self.assertRaises(ValueError):
            disjoint_training_seeds(4000, dataset)
        with self.assertRaises(ValueError):
            disjoint_evaluation_seeds(6000, 32, {"trainingSeeds": [6000, 6999]}, dataset)
        with self.assertRaises(ValueError):
            disjoint_evaluation_seeds(4500, 32, {"trainingSeeds": [6000, 6999]}, dataset)
        disjoint_training_seeds(6000, dataset)
        disjoint_evaluation_seeds(8000, 32, {"trainingSeeds": [6000, 6999]}, dataset)

    def test_recorded_dataset_checksum_includes_the_closed_compressed_file(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "data.jsonl.gz"
            metadata = collect_split(path, 1, 16)
            self.assertEqual(metadata["sha256"], sha256(path))
            with gzip.open(path, "rt") as stream:
                rows = [json.loads(line) for line in stream]
            self.assertEqual(len(rows), metadata["decisions"])
            self.assertIn("kind", rows[0]["decision"])

    def test_reset_is_repeatable_and_each_opponent_appears_in_both_seats(self):
        env = RiskEnv(opponents=("easy", "normal"))
        try:
            first, _ = env.reset(seed=7)
            repeated, _ = env.reset(seed=7)
            np.testing.assert_array_equal(first["state"], repeated["state"])
            self.assertEqual((env.seat, env.opponent_name), (0, "easy"))
            env.reset()
            self.assertEqual((env.seat, env.opponent_name), (1, "easy"))
            env.reset()
            self.assertEqual((env.seat, env.opponent_name), (0, "normal"))
            env.reset()
            self.assertEqual((env.seat, env.opponent_name), (1, "normal"))
        finally:
            env.close()


if __name__ == "__main__":
    unittest.main()
