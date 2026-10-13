# Scene build merge — 2026-10-11

## Update — 2026-10-13

Merged `scene_build` checkpoint 8 (`3aee48dc`) after syncing interaction's new
prayer animation commit (`bd4be46e`). Scene 03 is restored at the user's request;
SceneTwoNarration now loads it after the existing narration and animation finish.
It uses a copy of the current VR rig on the fishing boat deck, with the existing
harbour and seagull clips looping. No new dialogue or character animation was
invented for scene 03. Scenes 01 and 02 keep their current narration, subtitles,
sound clips, character animations and incense props.

Conflicting unused legacy ocean models and prefabs were replaced as complete
model/meta/prefab sets from scene_build, keeping the new room's GUID references
consistent. Current character, sound, subtitle and SteamVR resource files are
unchanged from `bd4be46e`. All three scenes passed Unity 2022.3.62f3 batch checks
for missing scripts, serialized references, camera presence, audio clips and
animation bindings. The project's existing 2022.3.62f3c1 version and package
configuration are retained. Headset play-through and deck framing remain manual
checks. Caches are ignored and untracked. Pre-sync recovery branch:
`codex/interaction-before-scene-build-20261013`.

## Previous merge

Merged `origin/scene_build` at `85bcb3f4` into `interaction` at `d05294d1`.
The branches have unrelated histories. The merge retains interaction's resources
and project configuration, imports the published Project3D assets, and migrates
the existing scenes rather than replacing their interaction objects.

- Scenes 01 and 02 now instance the published room prefabs. Their scene GUIDs remain unchanged.
- Scene 01 retains all 8 consumer objects under 3 roots. Its right-hand controller
  now references the new room's censer collider; the censer's tag, BoxCollider and
  Rigidbody are preserved as scene overrides.
- Scene 02 retains all 118 consumer objects under 7 roots, including Father_Young,
  Daughter_Child, IncenseStoryDirector, the cameras, audio sequence and subtitles.
- Character assets, animation clips, Timelines, MP3 files, subtitles and SteamVR
  resources remain unchanged from interaction.
- Scene 03 is removed as requested. The upstream retired scene 04 is also removed.
  Scene 02 finishes its existing sequence and stays in scene 02, rather than trying
  to load scene 03. Third-scene supporting resources remain available.
- ProjectSettings and Packages retain interaction's configuration; Build Settings
  contains only the two remaining scenes.
- Library, Logs, Temp, UserSettings and generated IDE files are ignored and untracked.
  Git fetch transfers commit history; ignored working-tree caches are not checked out.

Unity 2022.3.62f3 batch validation passed for both scenes: no missing scripts or
visible serialized object references, valid audio sequence references, censer
controller bindings, animation Timeline bindings and nonempty scene 02 subtitles.
VR device input/output and visual framing still require a headset play-through.

The pipeline migration reports list the old room placements replaced by the new
layout. Their scene 01 reference warning is resolved by MuseumSceneMigration's
censer rebinding; the final validation checks the saved scenes after this repair.
The four removed lighting children are part of the superseded room arrangement.

Recovery: `codex/interaction-before-scene-build-20261011` preserves the original
interaction commit. Raw scene backups are also kept locally under the ignored
`Assets/Project3D/Backups/` directory.
