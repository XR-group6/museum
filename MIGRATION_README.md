# MIGRATION_README

**Read this once, before your next `git pull`, if you have ever edited
`Assets/Project3D/Scenes/` in this project.**

If you are cloning for the first time: you can ignore this file. The scenes you get are
already in the new form and open standalone.

- [What changed](#what-changed)
- [Before you pull](#before-you-pull)
- [Pull, and keep your scenes](#pull-and-keep-your-scenes)
- [Run the migration](#run-the-migration)
- [What is kept, and how](#what-is-kept-and-how)
- [What is deleted, and the backups](#what-is-deleted-and-the-backups)
- [If something goes wrong](#if-something-goes-wrong)

## What changed

- **Scenes 01 and 02 are delivered *thin* now.** The whole room's arrangement lives in
  `Assets/Project3D/Prefabs/scenes/scene_01.prefab`, and
  `Assets/Project3D/Scenes/scene_01.unity` holds a single instance of that prefab.
  It is the same room, the same objects, the same positions — but the pipeline can now
  change the *layout* by rewriting the prefab, and the file you edit is never rewritten
  again. Anything you add to the scene survives as a prefab-instance override.
- **Scenes 03 and 04 are retired.** The pipeline no longer builds them, so they are
  deleted from the delivery rather than left to rot against a `Models/` that keeps
  moving. They will be rebuilt later; if you need your edits, they are in your git
  history and the migration backs them up on your machine before deleting.
- **The models nothing references are gone too** (60 files: scene 03/04's, plus a few
  dropped elsewhere), and they are listed in
  `Assets/Project3D/Update/orphans.json` so the deletion is auditable rather than
  mysterious.

Nothing else about the project changes: same paths, same mesh names, same material
slots, same scene GUIDs.

## Before you pull

Commit or stash, so nothing is lost and you can always get your scene back:

```bash
cd <your>/<copy>/of/this/project
git add -A && git commit -m "wip: before scene migration"   # or: git stash
git rev-parse HEAD > /tmp/migration-mine                     # remember your own commit
```

If you have never touched `Assets/Project3D/Scenes/`, you can skip straight to pulling:
git will fast-forward and there is nothing to migrate.

## Pull, and keep your scenes

```bash
git fetch
git pull
```

Expect one of two things on `Assets/Project3D/Scenes/scene_01.unity` (and `scene_02`):

1. **A merge conflict.** Normal: both sides rewrote the file.
2. **A clean auto-merge.** Also normal, and *also* not what you want — a merged scene can
   end up holding both your objects and the pipeline's new prefab, i.e. the room twice.

Either way, the fix is the same: **keep your own copy of every scene you worked in, and
take the pipeline's changes to everything else.**

```bash
MINE=$(cat /tmp/migration-mine)
git checkout "$MINE" -- Assets/Project3D/Scenes/scene_01.unity
git checkout "$MINE" -- Assets/Project3D/Scenes/scene_02.unity
git add -A && git commit
```

Why keep yours? Because the tool in the next step *builds the new thin scene out of your
inline one*, carrying your objects and components onto the prefab instance. If git hands
you the pipeline's thin file instead, the tool will correctly report "already thin —
nothing to do" and your local work in that scene is only in history.

And if you have your own edits in scene 03 or 04 and want a backup before they go, pull
them back too — the retirement pass copies a file it is about to delete:

```bash
git checkout "$MINE" -- Assets/Project3D/Scenes/scene_03.unity
git checkout "$MINE" -- Assets/Project3D/Scenes/scene_04.unity
```

## Run the migration

1. Open the project in the Unity version named in `ProjectSettings/ProjectVersion.txt`.
2. **Close scene 01–04 if any of them is open** — the tool refuses to delete a scene
   that is open in the editor.
3. `Project3D ▸ Migrate Scene ▸ Dry Run (all pending)`. Read the Console. Nothing is
   written; this is what it would do.
4. `Project3D ▸ Migrate Scene ▸ Apply (all pending)`.
5. Read the Console (`[Project3D] migrate:` and `[Project3D] retire:` lines), then the
   reports:

   - `Assets/Project3D/Update/scene_01.migration.json` (and `scene_02`) — what was
     wrapped, what was kept, what was carried.
   - `Assets/Project3D/Update/retired.json` — what was deleted, and anything that was
     kept because one of your scenes still uses it.
   - `Assets/Project3D/Update/pipeline.json` and `Update/scene_01.changes.json` — the
     publish's own summary of what moved, appeared or disappeared per scene.
6. Open both scenes and check them. Then commit — this is your migration commit:

   ```bash
   git add -A && git commit -m "migrate scene_01 and scene_02 to the thin form"
   ```

Headless, if you would rather script it:

```bash
Unity -batchmode -nographics -quit -projectPath <path/to/this/project> \
      -executeMethod Project3D.MigrateScene.Migrate -project3d-dry-run
# drop -project3d-dry-run to apply
# -project3d-scene scene_01   migrates just that one scene
```

## What is kept, and how

- **Your objects are never touched.** Anything outside `Assets/Project3D/Prefabs/` —
  plus the three pipeline roots `Project3D Lighting`, `Project3D Probes` and
  `Project3D Post` — is yours by the same ownership rule the daily publish uses.
  Cameras, lights, colliders, scripts, terrain, VFX, your own prefabs: untouched.
- **Your nudges stay yours.** If you moved a pipeline object, the tool writes its
  position onto the prefab's copy as an **instance override**, never into the prefab.
  Your tweak therefore cannot leak into anyone else's copy, and it survives future
  publishes.
- **Your components come across.** A component you attached to a pipeline object is
  copied onto that object's copy under the prefab instance.
- **Nothing moves, nothing is renamed, no GUID changes.** Every object's world
  transform is checked before and after, and the scene's own GUID is preserved — so
  build settings, addressables and anything else referencing the scene keep working.

## What is deleted, and the backups

- `Scenes/scene_03.unity`, `Scenes/scene_04.unity`, and their scene prefabs and
  manifests.
- The model files the pipeline retired, but only those **no scene in your project still
  references**. Anything yours still uses is kept and named in `Update/retired.json`.
- Everything deleted is copied first to `Assets/Project3D/Backups/<timestamp>/` as
  `<name>.bak` plus `.meta.bak`. That folder is gitignored — it is your local undo, not
  something to commit, and you can delete it once your migration commit is pushed.

## If something goes wrong

The migration is **fail-closed**. If any object would move, if a placement cannot be
found in the published prefab, or if a component would be dropped, it restores the
backup and stops with a named reason — a refused migration changes nothing and can be
re-run once the cause is dealt with. The reasons you are most likely to meet:

| Message | What to do |
|---|---|
| `... is open in the editor. Close it and run again` | Close that scene, re-run. |
| `no pipeline-owned roots found` | The scene is already thin (fine, nothing to do) or was never pipeline-built. Do not force it. |
| `no child named '...' in .../scenes/scene_01.prefab` | The pipeline's prefab and your scene disagree. Take the pipeline's scene file for that scene and re-apply your work as overrides, or ask. |
| `already thin -- ... Nothing to do` | Not an error. You are done with that scene. |
| `neither Assets/Project3D/SceneManifests/ nor .../Update/pipeline.json exists` | This project has not been synced — you are not in the project this file is about. |

`Project3D ▸ Migrate Scene ▸ Retire -- dry run (what would be deleted)` shows the
retirement half on its own, without touching anything.

**Finally:** `Assets/Project3D/Editor/MigrateScene.cs` is shipped only while there is
something to run. Once no scene is pending and nothing is retired, a later publish
removes it — so do not build anything of your own on top of it.

<!-- Maintainers: this document describes MigrateScene.cs, whose source of truth is the
     pipeline repository (unity-scripts/Editor/Project3D/MigrateScene.cs). If you change
     the tool's behaviour, change this file in the same breath. -->
