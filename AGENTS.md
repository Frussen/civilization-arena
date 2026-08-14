# Civilization Arena — Agent Guidelines

Civilization Arena is an open-source 3D civilization sandbox and evaluation environment where a small number of LLM-controlled individuals interact inside larger rule-based societies.

## Development principles

* Keep changes small, incremental, and understandable.
* Prefer simple solutions before sophisticated ones.
* Do not introduce DOTS/ECS unless profiling shows a concrete need and the change has been explicitly discussed.
* Keep simulation logic separate from presentation and rendering where practical.
* The simulation is authoritative: visual state must represent simulation state, not define it.
* Prefer deterministic and seedable behavior for simulation logic that affects experiments.
* Add tests for non-visual simulation logic when appropriate.
* Avoid unnecessary dependencies and architectural complexity.

## Unity project rules

* Do not manually edit Unity-generated scene, prefab, Animator, or other serialized YAML files unless explicitly required.
* Prefer using the Unity Editor for scene composition, prefab configuration, animation setup, and other visual authoring tasks.
* Do not commit generated folders such as `Library`, `Temp`, `Logs`, `Obj`, or build outputs.
* Preserve Unity `.meta` files for tracked assets.

## Third-party dependencies and assets

* Do not add any third-party asset, library, package, model, texture, animation, sound, or other material until its license and redistribution terms have been verified.
* Free-of-charge does not imply open-source or redistributable.
* Any third-party material distributed with the repository must be documented in `THIRD_PARTY_NOTICES.md`.
* Prefer well-maintained dependencies with clear licenses.
* Avoid making the project fundamentally dependent on poorly maintained or ambiguously licensed software.

## Project scope

* Do not expand the project into unrelated systems prematurely.
* Avoid premature additions such as combat, complex politics, reproduction, neural-network NPCs, reinforcement learning, large-scale procedural generation, or multiplayer unless explicitly discussed.
* Build and validate small milestones before scaling population, simulation complexity, or visual complexity.

## Learning and collaboration

* Explain important architectural or technical decisions clearly.
* Do not replace understandable code with unnecessary abstraction.
* When a change introduces a new Unity or C# concept, prefer a concrete and readable implementation.
