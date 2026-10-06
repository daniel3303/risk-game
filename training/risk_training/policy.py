import torch
from torch import nn
from sb3_contrib.common.maskable.policies import MaskableActorCriticPolicy
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor


class CandidateFeatures(BaseFeaturesExtractor):
    def __init__(self, observation_space):
        state = observation_space["state"].shape[0]
        actions, size = observation_space["candidates"].shape
        super().__init__(observation_space, state + actions * size)

    def forward(self, observation):
        return torch.cat((observation["state"], observation["candidates"].flatten(1)), dim=1)


def hidden_layers(input_size, width, depth):
    layers = []
    for _ in range(depth):
        layers.extend((nn.Linear(input_size, width), nn.Tanh()))
        input_size = width
    return layers


class CandidateNetworks(nn.Module):
    """Shared action scoring keeps candidate order from becoming an Expert oracle."""

    def __init__(self, state_size, action_size, capacity, width=64, depth=1):
        super().__init__()
        self.state_size = state_size
        self.action_size = action_size
        self.capacity = capacity
        self.latent_dim_pi = capacity
        self.latent_dim_vf = width
        self.context = nn.Sequential(*hidden_layers(state_size, width, depth))
        self.scorer = nn.Sequential(*hidden_layers(width + action_size, width, depth), nn.Linear(width, 1))
        self.critic = nn.Sequential(*hidden_layers(state_size, width, depth + 1))

    def forward_actor(self, features):
        state = features[:, : self.state_size]
        candidates = features[:, self.state_size :].reshape(-1, self.capacity, self.action_size)
        context = self.context(state).unsqueeze(1).expand(-1, self.capacity, -1)
        return self.scorer(torch.cat((context, candidates), dim=2)).squeeze(-1)

    def forward_critic(self, features):
        return self.critic(features[:, : self.state_size])

    def forward(self, features):
        return self.forward_actor(features), self.forward_critic(features)


class CandidatePolicy(MaskableActorCriticPolicy):
    def __init__(self, *args, width=64, depth=1, **kwargs):
        if not 16 <= width <= 512 or not 1 <= depth <= 3:
            raise ValueError("Policy width must be 16–512 and depth must be 1–3")
        self.width = width
        self.depth = depth
        kwargs["features_extractor_class"] = CandidateFeatures
        kwargs["ortho_init"] = False
        super().__init__(*args, **kwargs)

    def _build_mlp_extractor(self):
        state_size = self.observation_space["state"].shape[0]
        capacity, action_size = self.observation_space["candidates"].shape
        self.mlp_extractor = CandidateNetworks(state_size, action_size, capacity, self.width, self.depth).to(self.device)

    def _build(self, lr_schedule):
        super()._build(lr_schedule)
        # The shared scorer already produces logits; a slot-specific layer would break permutation equivariance.
        self.action_net = nn.Identity()
        self.optimizer = self.optimizer_class(self.parameters(), lr=lr_schedule(1), **self.optimizer_kwargs)
