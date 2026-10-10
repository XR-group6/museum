# Project3D — Unity project

The Unity half of Project3D. This repository is what the team consumes: the
models and the scenes, under version control, without the tooling that generates
them.

**The models here are generated.** `Assets/Project3D/` is written by a pipeline
that lives in a separate, private repository, and is copied in wholesale. Editing
those files by hand does not work — the next publish overwrites them.

If you are an AI agent working in this repo, read [`CLAUDE.md`](CLAUDE.md) first.
It is short and it will save you from making a change that silently disappears.

## Layout

```
Assets/Project3D/
  Models/<category>/    *.fbx — one per asset
  Textures/             *.png — one per palette key
  Materials/            *.mat — one per palette key
  Prefabs/<category>/   *.prefab — one per asset, what scenes reference
  Prefabs/scenes/       *.prefab — a whole room's arrangement (see below)
  Scenes/               *.unity — scene_01, scene_02
  SceneManifests/       *.json — what a scene means to contain, per scene
  Update/               *.json — what the last publish changed
  Editor/               MigrateScene.cs — the one-time fat->thin migration tool
  Backups/              written by the migration tool; gitignored, local only
Assets/Editor/          your own editor scripts. Project3D/ inside it is
                        gitignored — the pipeline injects and removes it.
Packages/               Unity package manifest
ProjectSettings/        Unity project settings — version pinned deliberately
```

## Getting started

1. Install the Unity version named in `ProjectSettings/ProjectVersion.txt`. Do
   not upgrade it casually — the pipeline is pinned to it, and a version bump is
   a coordinated change, not a local one.
2. Clone this repository.
3. Register the Unity YAML merge driver — one command, once per machine. Without
   it, a merge on a `.unity` or `.prefab` file produces something Unity cannot
   open. Copy the two `git config` lines from the top of `.gitattributes`.
4. Open the project. Unity will build `Library/` on first open; that takes a few
   minutes and is not committed.

## Scenes are thin, and one migration is pending

A room is delivered as `Prefabs/scenes/<scene>.prefab` — the arrangement — with
`Scenes/<scene>.unity` holding a single instance of it. The pipeline can therefore
update a room's layout without rewriting the scene file you edit.

**If you already had this project and edited scene 01 or 02, read
[`MIGRATION_README.md`](MIGRATION_README.md) before you pull.** It is a one-time,
in-your-own-checkout step that wraps your scene in the published prefab and carries
your cameras, components and nudges across. Scenes 03 and 04 have been retired and
are gone from this delivery; there is nothing to migrate for them.

## Working in it

**Placing models in a scene is normal work here.** Rebuilding a model does not
disturb your scene: scenes reference prefabs, prefabs reference the FBX, and the
pipeline changes only the FBX. So a model can be entirely remodelled and
republished and your `.unity` file will not change by a single byte.

**Never rename or move a file under `Assets/Project3D/`.** Four things are frozen
— the file path, the mesh name inside the FBX, the material slot names, and the
bounds and pivot. Those are how the pipeline finds what to update. Renaming a
material slot or moving an FBX breaks every reference to it, and the breakage is
silent. See `CLAUDE.md` for the full contract.

## What is not here

The generator: the Blender build scripts, the specs, the validation suite, the
scene-layout checks, the Unity import automation. All of that is private and
lives in the pipeline repository. If a model is wrong, the fix goes there — not
here.

## Status

Scenes 01 and 02 are published and playable, in the thin form described above.
Scenes 03 and 04 are retired: the pipeline no longer builds them and they are gone
from this project; they will come back as new scenes when they are rebuilt. The
layout above is what the pipeline publishes into.
