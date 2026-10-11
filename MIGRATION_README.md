# MIGRATION_README

**Read this before your next `git pull` if you have ever edited a scene in
`Assets/Project3D/Scenes/`.**

Cloning for the first time? Skip it — the project you get is already in the new
form and opens standalone.

## What changed, in one paragraph

A room is no longer stored as a list of objects inside `Scenes/scene_01.unity`. It is
stored as a **prefab** — `Prefabs/scenes/scene_01.prefab`, the whole arrangement — and
the scene file holds a **single instance of that prefab**. The room is the same room,
with the same objects in the same places. The difference is that the pipeline can now
change the layout by rewriting the prefab, so the scene file you edit is never
rewritten again.

That matters when you pull: the new scene file replaces the one you have been working
in. Your objects are not in it. The fix is to keep your own copy of every scene you
touched and let the migration tool build the new form out of it.

The tool is part of this project: `Assets/Project3D/Editor/MigrateScene.cs`. It is
Unity-only and needs nothing from the pipeline — it runs in your checkout, on your
machine, and nothing of yours is sent anywhere.

## Step 1 — save your work

```bash
cd <your>/<copy>/of/this/project
git add -A && git commit -m "wip: before scene migration"   # or: git stash
git rev-parse HEAD > /tmp/migration-mine                     # remember your commit
```

## Step 2 — pull, keeping your own scene files

```bash
git fetch && git pull
```

Git will either conflict on `Scenes/scene_01.unity` or auto-merge it cleanly. Both are
normal, and **the clean merge is the one to watch for**: a merged scene can hold both
your objects and the pipeline's new prefab — the room twice.

Either way, the answer is the same: keep your copy of any scene you worked in, and take
the pipeline's version of everything else.

```bash
git checkout "$(cat /tmp/migration-mine)" -- Assets/Project3D/Scenes/scene_01.unity
git checkout "$(cat /tmp/migration-mine)" -- Assets/Project3D/Scenes/scene_02.unity
git add -A && git commit -m "keep my scenes for the migration"
```

Your scene file is still the **old inline form**. That is what the tool needs: it reads
that file and rebuilds it as the new thin form, carrying your work across. If git hands
you the pipeline's thin file instead, the tool will correctly report "already thin —
nothing to do", and your local work stays only in git history.

## Step 3 — run the migration

1. Open the project in the Unity version named in `ProjectSettings/ProjectVersion.txt`.
2. Close scenes 01 and 02 if they are open in the editor.
3. `Project3D ▸ Migrate Scene ▸ Dry Run (all pending)`. Read the Console — it writes
   nothing, it tells you what it would do.
4. `Project3D ▸ Migrate Scene ▸ Apply (all pending)`.
5. Read the reports it wrote:

   | File | What it says |
   |---|---|
   | `Assets/Project3D/Update/scene_01.migration.json` | what was wrapped, kept and carried |
   | `Assets/Project3D/Update/scene_01.changes.json` | what the publish moved, added or removed |
   | `Assets/Project3D/Update/pipeline.json` | the publish's own summary |

6. Open both scenes and check them, then commit — this is your migration commit:

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

- **Your objects are never touched.** Anything that is not part of the pipeline's room —
  plus its three rigs `Project3D Lighting`, `Project3D Probes` and `Project3D Post` — is
  yours by the same ownership rule the daily publish uses. Cameras, lights, colliders,
  scripts, terrain, VFX, your own prefabs: untouched.
- **A placement you moved goes back to the published position.** The published layout is
  the standard: where an object sits is what the update says, not what your checkout left
  it at. The move is *not* quietly kept — it is undone and named in the report, so a room
  re-modelled upstream never arrives with its furniture at last year's coordinates.
  Anything you moved **inside** a placement is kept: the layout speaks in placements, so
  a sub-object's transform is yours.
- **Your components come across.** A component you attached to a pipeline object is
  copied onto that object's copy as an *instance override* under the prefab instance,
  never baked into the prefab — so it cannot leak into anyone else's copy.
- **Nothing is renamed or re-GUIDed.** Every object keeps the name it had, and the
  scene's own GUID is preserved, so build settings, addressables and anything else
  pointing at the scene keep working.

## If the run cannot carry something

The tool **carries on and names what it could not do**. A pipeline object the published
room has no counterpart for is removed rather than left behind; an override it cannot
hold exactly is reported. Neither is a refusal. Nothing is dropped quietly — every
un-carried item, every one of your objects the swap removes, and every reference left
dangling (a camera you pointed at a room object, a child you added to one) is written to
`Update/<scene>.migration.json` and printed to the Console. The worst outcome is a scene
that changed with every such item *pointed at*, never a scene that changed in a way
nobody can see.

Only the cases with nothing to migrate stop the run, and they change nothing:

| Message | What it means |
|---|---|
| `... is open in the editor. Close it and run again` | Close that scene, re-run. |
| `already thin -- ... Nothing to do` | Not an error. You are done with that scene. |
| `no pipeline-owned roots found` | The scene is already thin, or was never pipeline-built. Do not force it. |
| `no Assets/Project3D/SceneManifests/ -- this project has not been synced by the pipeline` | You are not in the project this file is about. |
| `neither Assets/Project3D/SceneManifests/ nor Assets/Project3D/Update/pipeline.json exists` | Same thing. |

A placement the tool cannot match to the published prefab is no longer a refusal: it is
reported as `removed` or `drifted` in `Update/<scene>.migration.json` and the run
continues. Read that file rather than the Console if you want the full list.

The tool also removes scenes the pipeline has retired, after copying them to
`Assets/Project3D/Backups/<timestamp>/` (gitignored, local, safe to delete). A retired
scene still present in an old checkout of yours is deleted on the same run; a scene that
is already gone is simply nothing to do.

**`Assets/Project3D/Editor/MigrateScene.cs` ships with this project and stays here.** It
used to arrive only while a migration was live and be removed afterwards. It is now part
of every delivery, because the project is handed over wholesale and a tool that is present
on only some publishes cannot be relied on. It is still the pipeline's file, and a later
publish overwrites it — put your own work in your own scripts, not on top of this one.

<!-- Maintainers: this document describes MigrateScene.cs, whose source of truth is the
     pipeline repository (unity-scripts/Editor/Project3D/MigrateScene.cs). If you change
     the tool's behaviour, change this file in the same breath. -->
