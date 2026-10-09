# Fairy Beer — portfolio snapshot

An educational 2D platformer made with Unity 6 and C# for Windows x64. The fairy crosses a procedurally varied island with reversed movement controls.

![Gameplay](Portfolio/gameplay.png)

- [Download the Windows build (2.2.0)](https://github.com/neonkitts/Probnyy-proekt-Unity/releases/tag/v2.2.0)
- [Read the current project overview and source guide](README.md)
- [See more screenshots](Portfolio)

**Contribution:** Zlatoslava Manko defined the concept, gameplay and visual requirements, selected references and reviewed the result. Codex assisted with code implementation, checks and materials. This is a learning project, not independently developed commercial work.

**Technical highlights:** seeded obstacle generation, fixed-step movement, inverted input, enemies, checkpoints, JSON saves and editor validation. The code is concentrated in [ChibiGame.cs](Assets/Scripts/ChibiGame.cs) and [ChibiPresentation.cs](Assets/Scripts/ChibiPresentation.cs); splitting these systems into smaller components is a next step.

The [validation log](QA/validation.txt) records automated checks over 100 seeds. The released Windows build was launched and an accelerated route to the ending was checked. A full manual playthrough and physical gamepad input still need testing.
