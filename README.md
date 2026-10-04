# RougeLIke

A roguelike unit-builder battler made in Unity.

## The game

Inspired by Spore's creature creator, the player **builds their own units** by combining a **body** with **parts** (limbs, weapons, armor, sensors, and so on). Each body and part contributes stats and abilities, so how a unit is assembled decides how it fights.

A run is a sequence of battles:

1. **Build** units from the bodies and parts you own.
2. **Deploy** them into a battle against enemy units.
3. **Win** and pick a **reward**: a new body, a new part, or a buff.
4. Repeat, growing stronger, until the run ends.

Losing ends the run, roguelike style; each run starts fresh.

## Tech

- **Engine:** Unity 6.6 (`ProjectSettings/ProjectVersion.txt` pins 6000.6.0f1).
- **Language:** C#

## Getting started

1. Install Unity 6000.6.0f1 through Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and select this folder.
3. On first open Unity generates the remaining `ProjectSettings/`, `Packages/` and `.meta` files. Commit those so everyone shares the same setup.

## Project layout

```
Assets/
  Scripts/             C# gameplay code (units, parts, combat, rewards, run flow)
  Prefabs/             Bodies, parts and assembled unit prefabs
  Scenes/              Game scenes (menu, builder, battle, reward)
  ScriptableObjects/   Data assets: body/part definitions, enemy waves, reward tables
  Art/                 Sprites, models, materials
  Audio/               Music and sound effects
ProjectSettings/       Unity project settings
```

The `.gitkeep` files only exist so Git keeps the empty folders; delete them once a folder has real content.
