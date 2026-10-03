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
  Models/<category>/    *.fbx — one per asset, 85 of them
  Textures/             *.png — shared trim sheet (Phase 2; currently empty)
  Materials/            *.mat — one per palette entry
  Prefabs/<category>/   *.prefab — what scenes actually reference
  Scenes/               *.unity
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

This repository is being set up. Phases 1–4 of the pipeline work — detail levels,
textures, and the Unity import itself — are still to land, so `Models/`,
`Materials/`, `Prefabs/` and `Scenes/` are all currently empty. The layout above
is the target the pipeline will publish into.
