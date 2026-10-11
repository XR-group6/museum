# CLAUDE.md — read this before editing anything

## This repository is generated. Do not hand-edit it.

Everything under `Assets/Project3D/` is produced by a pipeline that lives in a
different repository. It is copied in wholesale and committed. Your changes here
will be overwritten by the next publish, without warning and without a merge
conflict to tell you it happened.

That does not mean you cannot change the models. It means the change has to be
made at the source.

| I want to... | Do this |
|---|---|
| change a model's shape | don't edit the FBX — see the pipeline repo |
| move a model's file path | don't — paths are identity, see below |
| rename a mesh or material | don't — names are identity, see below |
| place models in a scene | **fine**, but read "Scenes" below first |
| add a light, camera, or script | **fine** — that is your work, not the pipeline's |
| change a material's colour | not yet supported; ask |

## What the pipeline owns

`Assets/Project3D/Models/`, `Textures/`, `Materials/`, `Prefabs/` — and the
parts of `Scenes/` that reference them.

## What you own

Everything else. `Assets/Scripts/`, lights, cameras, terrain, your own scenes,
`ProjectSettings/`, `Packages/`. The pipeline does not write outside
`Assets/Project3D/`.

## The identity contract

`make publish` updates models **in place**. It relies on four things never
changing:

1. **the asset's file path** — `Assets/Project3D/Models/<category>/<name>.fbx`
2. **the mesh object's name** inside the FBX
3. **material slot names** — these are matched *by name, never by index*
4. **bounds and pivot** — an asset's bounding box and its origin do not move

Geometry is not on that list, and that is the point: a model can be rebuilt with
a completely different shape and every scene using it updates by itself. But if
you rename a material slot, or move an FBX, every reference to it breaks.

**So: never rename or move a file under `Assets/Project3D/`.** If something looks
like it needs renaming, that is a pipeline change.

## Scenes

Scenes are the team's version-control standard, and `make publish` prefers not to
touch them: because scenes reference *prefabs*, and prefabs reference the FBX,
rebuilding a model changes the FBX and the prefab and leaves the `.unity` file
alone.

You can edit scenes freely. Two things worth knowing:

- **Force Text serialization must stay on** (it is the default). With binary
  scenes, git stores an opaque blob and no merge or diff is possible at all.
- **A scene merge needs UnityYAMLMerge.** `.gitattributes` names it, but the
  driver must be registered in your local `.git/config` — see the comment at the
  top of `.gitattributes`. Until you do, a scene merge is a plain text merge and
  will produce a file Unity cannot open. If you hit that, the fix is to take one
  side wholesale and redo the other by hand.

## Do not

- **Commit editor automation into `Assets/Editor/Project3D/`.** It is gitignored
  on purpose and the pipeline deletes it. Teammates are meant to get a project
  they can open and play, with no build tooling in it.
- **Commit a `Library/` directory**, or anything else in `.gitignore`. Unity
  regenerates it.
- **Commit a Unity licence, serial, or `.ulf` file.** The pipeline uses the
  `UNITY_LICENSE` environment variable and never writes it to disk.
