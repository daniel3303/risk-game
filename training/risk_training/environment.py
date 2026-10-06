import gymnasium as gym
import numpy as np

from .bridge import Bridge


def pack(frame, schema):
    candidates = np.zeros((schema["capacity"], schema["candidateSize"]), np.float32)
    rows = frame["observation"]["candidates"]
    if rows:
        candidates[: len(rows)] = rows
    return {"state": np.asarray(frame["observation"]["state"], np.float32), "candidates": candidates}


def mask(frame, schema):
    return np.arange(schema["capacity"]) < len(frame["observation"]["candidates"])


class RiskEnv(gym.Env):
    metadata = {"render_modes": []}

    def __init__(self, first_seed=6000, opponents=("easy", "normal", "hard", "expert"), frozen=None, seed_count=1000):
        super().__init__()
        self.bridge = Bridge()
        self.schema = self.bridge.schema
        self.first_seed = first_seed
        self.seed_count = seed_count
        self.opponents = opponents
        self.frozen = frozen
        self.frame = None
        self.seat = 0
        self.episode = 0
        self.opponent_name = None
        self.action_space = gym.spaces.Discrete(self.schema["capacity"])
        self.observation_space = gym.spaces.Dict({
            "state": gym.spaces.Box(-np.inf, np.inf, (self.schema["stateSize"],), np.float32),
            "candidates": gym.spaces.Box(-np.inf, np.inf, (self.schema["capacity"], self.schema["candidateSize"]), np.float32),
        })

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        if seed is not None:
            self.episode = 0
        options = options or {}
        game_seed = options.get("game_seed", self.first_seed + (self.episode // 2) % self.seed_count)
        self.seat = options.get("seat", self.episode % 2)
        self.opponent_name = options.get("opponent", self.opponents[(self.episode // 2) % len(self.opponents)])
        self.episode += 1
        self.frame = self.bridge.request(operation="reset", seed=game_seed, seat=self.seat, opponent=self.opponent_name,
                                         maxRounds=options.get("max_rounds", 100), maxActions=options.get("max_actions", 5000))
        self._advance_external()
        return pack(self.frame, self.schema), self._info()

    def step(self, action):
        self.frame = self.bridge.request(operation="step", action=int(action))
        self._advance_external()
        return pack(self.frame, self.schema), self.frame["reward"], self.frame["terminated"], self.frame["truncated"], self._info()

    def _advance_external(self):
        if self.opponent_name != "external":
            return
        if self.frozen is None:
            raise ValueError("An external opponent requires a frozen policy")
        while not self.frame["terminated"] and not self.frame["truncated"] and self.frame["player"] != self.seat:
            observation = pack(self.frame, self.schema)
            action, _ = self.frozen.predict(observation, deterministic=False, action_masks=mask(self.frame, self.schema))
            self.frame = self.bridge.request(operation="step", action=int(action))

    def action_masks(self):
        return mask(self.frame, self.schema)

    def _info(self):
        return {key: self.frame[key] for key in ("winner", "round", "actions")}

    def close(self):
        self.bridge.close()
