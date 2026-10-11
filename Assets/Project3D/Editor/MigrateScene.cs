using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project3D
{
    /// <summary>
    /// The one-time fat -> thin scene migration, run by each consumer in their own copy.
    ///
    /// Why this is a shipped Unity tool and not a `make` target
    /// ---------------------------------------------------------
    /// Delivery here is `git pull`, and consumers do not sit at the pipeline's terminal.
    /// The first four scenes (01-04) were built and shipped in the **fat** form, where
    /// `UpdateScenes` writes one prefab instance per placement straight into
    /// `Scenes/&lt;scene&gt;.unity` -- the file a consumer edits. That means the pipeline can
    /// never apply a *layout* change to such a room without rewriting their file, which is
    /// the one thing never to risk (a Unity YAML merge, on the file they care about most).
    ///
    /// The fix is the **thin** form: the whole arrangement lives in
    /// `Prefabs/scenes/&lt;scene&gt;.prefab` and the `.unity` holds a single instance of it, so
    /// a layout edit rewrites only the prefab -- a file the consumer does not touch. New
    /// scenes are thin from birth. A *legacy* scene a consumer's checkout still holds in
    /// the inline form is wrapped by this file, in that consumer's own copy, and that is
    /// the one-time cost: it needs Unity (serialized-reference remapping is Unity's own
    /// format), never this pipeline's agent and never `make`.
    ///
    /// So this file is written to `Assets/Project3D/Editor/MigrateScene.cs` -- a
    /// pipeline-owned path, so it is unmistakably generated rather than a consumer's file
    /// -- and committed for as long as a checkout may still be holding the old shape. Each
    /// consumer pulls it, runs it in their own checkout, and commits **their own**
    /// `scene.unity` as a single-purpose commit. The owner's only act is the normal publish
    /// that ships the prefab; no consumer's work leaves their machine.
    ///
    /// **Which scenes it offers to migrate is decided from the scene, not from a flag.**
    /// The steady delivered form is `prefab`, and a consumer who has not yet pulled -- or
    /// who has been working on an old inline copy -- has exactly the same job to do. So a
    /// scene whose manifest is a delivered assembly *and* whose prefab is published is
    /// offered, and one that is already thin is a no-op when it is opened. The old
    /// `prefab-pending` transient is still honoured, for a checkout that pulls mid-wave.
    ///
    /// It is deliberately **self-contained**. The injected build scripts
    /// (`Assets/Editor/Project3D/`) are deleted after every `make unity` run, so when a
    /// consumer runs this months later those classes are not present to call into. What
    /// is duplicated from `UpdateScenes.cs` is called out where it happens.
    ///
    /// What it does, and what it promises
    /// ----------------------------------
    /// The prefab it wraps the scene around is generated from the **same manifest** the
    /// loose objects were, so an unmoved scene migrates as a pure reparent: every object
    /// keeps the name it had, every GUID is unchanged, and the only structural difference is
    /// that the pipeline-owned roots become children of one instance of the scene prefab.
    /// A consumer's cameras, VFX, NPCs and their own prefabs are never touched.
    ///
    /// A component the consumer attached to a placement is copied onto the prefab's copy as
    /// an **instance override**, never baked into the prefab, because baking would leak one
    /// consumer's work into everyone else's copy. **A nudge is not carried, and that is
    /// deliberate: the published layout is the standard.** Where the object sits is what the
    /// update says, not what the checkout left it at, so a moved placement is put back on
    /// the published pose and the move is named as a finding rather than silently kept -- a
    /// layout that only some consumers have is not a layout the pipeline can reason about,
    /// and a room re-modelled upstream would otherwise arrive with its furniture at
    /// last-year's coordinates. Components still come across, and so does anything the
    /// consumer moved *inside* a placement -- the layout speaks in placements, so a nudged
    /// sub-object is not the pipeline's business. It is the placement's own transform that
    /// the published room owns.
    ///
    /// It **carries on per-object, and reports what it could not do**. Every owned object's
    /// transform is re-asserted and read back, and every override it carries is checked --
    /// but a discrepancy is a *finding*, not a refusal: an object the published room has no
    /// counterpart for is named and the run continues, and so is one that would not hold the
    /// published pose. **The published room is the standard**: an owned object the update no
    /// longer carries is removed rather than left behind, and a nudge is undone rather than
    /// honoured. Only the cases with nothing to migrate -- no scene, no prefab, no owned
    /// object, or the scene open in the editor -- stop the run. It never deletes a consumer's
    /// own *top-level* object, and everything it could not carry, plus everything the swap
    /// removes or leaves dangling (a camera you pointed at a room object, an added child of
    /// one), is named in `Update/&lt;scene&gt;.migration.json` and in the Console. The worst outcome
    /// is therefore a scene that changed with every un-carried item *pointed at*, never a
    /// scene that quietly changed in a way nobody can point to.
    ///
    ///     Project3D &gt; Migrate Scene &gt; Dry Run (all pending)   -- computes and reports,
    ///                                                          writes nothing
    ///     Project3D &gt; Migrate Scene &gt; Apply (all pending)     -- does the swap
    ///
    /// or, for one scene, in batchmode:
    ///
    ///     Unity -batchmode -nographics -quit -projectPath shared \
    ///           -executeMethod Project3D.MigrateScene.Migrate -project3d-scene scene_01
    ///
    /// add `-project3d-dry-run` to stop short of writing.
    ///
    /// It also **retires** scenes, and that is the other half of the same job
    /// ---------------------------------------------------------------------------
    /// A scene the pipeline no longer builds cannot be kept in step with the kit, so it
    /// is dropped from the delivery rather than left to rot -- and the drop has to reach a
    /// checkout that already holds the scene, which a `git pull` cannot do for anyone whose
    /// copy of it has moved on. Scenes 03 and 04 are retired that way.
    ///
    /// Which scenes those are, and which models nothing references any more, arrives in
    /// `Update/` -- written by the pipeline's own `change_report.py` and
    /// `prune_published.py`, so this file reads the decision rather than making it. A
    /// retired scene is backed up and then deleted, and so is each unreferenced model --
    /// except one a scene *you* still have depends on, which is named in the report and
    /// left alone. That check is why this is a Unity tool and not a script: only Unity can
    /// answer "is anything still using this" without guessing.
    /// </summary>
    public static class MigrateScene
    {
        const string ScenesRoot        = "Assets/Project3D/Scenes";
        const string PrefabsRoot       = "Assets/Project3D/Prefabs";
        const string ScenePrefabsFolder = "scenes";
        const string ManifestsRoot     = "Assets/Project3D/SceneManifests";
        const string UpdateRoot        = "Assets/Project3D/Update";
        const string BackupsRoot       = "Assets/Project3D/Backups";
        const string AssetsRoot        = "Assets/Project3D";

        // Written by the pipeline's `unity-sync`, and the *only* source of the retired
        // set: a scene's own files cannot say that they are retired, and by the time a
        // consumer looks, the manifest that described the scene may be gone with it.
        const string PipelineLabelPath = UpdateRoot + "/pipeline.json";
        const string OrphanRecordPath  = UpdateRoot + "/orphans.json";

        // The assembly states this tool cares about, from the manifest -- see
        // `approval.ASSEMBLY_MODES` in the pipeline. `fat` is the one this tool does *not*
        // act on: a `fat` manifest is a scene the pipeline still writes inline, so there is
        // no prefab to wrap it in and nothing for a consumer to do.
        const string FatAssembly     = "fat";
        const string ThinAssembly    = "prefab";
        const string PendingAssembly = "prefab-pending";

        // The three roots `UpdateScenes` creates as *plain* GameObjects, with no prefab
        // behind them, so provenance cannot identify them the way it identifies a
        // placement. These strings are mirrored from `UpdateScenes.cs` (its
        // `LightingRootName`, `ProbesRootName`, `PostRootName`); MigrateScene ships
        // alone and cannot reference them, so `tests/test_geometry.py` asserts the two
        // files still agree rather than trusting that a rename happened in both.
        const string LightingRootName = "Project3D Lighting";
        const string ProbesRootName   = "Project3D Probes";
        const string PostRootName     = "Project3D Post";

        // Two poses are the same when they are within these. A tenth of a millimetre of
        // position, a hair of scale, a hundredth of a degree of yaw -- tight enough that a
        // hit is a real finding and not YAML float text round-tripping, which is why the
        // comparison is a tolerance rather than an equality. They are used twice: to decide
        // whether the consumer had moved an object at all (so the move can be named as
        // dropped), and to check the published pose actually took.
        const float PositionEpsilon = 1e-8f;   // squared metres
        const float ScaleEpsilon    = 1e-8f;   // squared factor
        const float AngleEpsilon    = 0.01f;   // degrees

        // --- the manifest, minimally -------------------------------------------------

        /// <summary>
        /// The fields this tool reads. Only two are needed: which scene, and what shape
        /// it is in. The placements are not read at all -- the prefab is already the
        /// canonical layout, generated from the same manifest by `UpdateScenes`, so
        /// re-deriving placement boxes here would be a second copy of a decision.
        /// </summary>
        [Serializable]
        class Manifest
        {
            public string scene;
            public string assembly;
        }

        /// <summary>
        /// `Update/pipeline.json`, written by `change_report.py`. Only `retired` is read:
        /// the per-scene change summaries are for the person who pulls.
        /// </summary>
        [Serializable]
        class PipelineLabel
        {
            public string[] retired;
        }

        /// <summary>
        /// `Update/orphans.json`, written by `prune_published.py`. `paths` is what the
        /// pipeline deleted from its own copy of this project -- relative to
        /// `Assets/Project3D/` -- and is offered to this checkout on the same terms.
        /// </summary>
        [Serializable]
        class OrphanRecord
        {
            public string[] retired_scenes;
            public string[] orphaned_models;
            public string[] paths;
        }

        // --- the report --------------------------------------------------------------

        [Serializable]
        class Entry
        {
            public string name;
            public string kind;     // "placement" | "root" | "component" | "kept"
                                    // | "removed" | "nudge-dropped" | "drifted"
                                    // | "destroyed" | "reference" | "animation"
            public string detail;   // human detail, e.g. the component type name
        }

        [Serializable]
        class Report
        {
            public string scene;
            public string kind;         // "migrated" | "retired"
            public string stamp;
            public bool dry_run;
            public bool applied;        // false when refused (see `reason`)
            public string reason;       // the named cause of a refusal
            public Entry[] kept;        // consumer objects, left exactly where they were
            public Entry[] migrated;    // pipeline roots wrapped into the prefab instance
            public Entry[] carried;     // components moved from a loose root onto its copy
            public Entry[] not_carried; // what did not carry while the rest applied -- a removed
                                        // object, a move the layout overrode, a pose that would
                                        // not take. Named, never silently dropped
            public Entry[] broken;      // objects the swap removes (a removed pipeline object, a
                                        // consumer child under one) and the references it orphans
            public bool partial;        // true when not_carried or broken is non-empty
        }

        // --- entry points ------------------------------------------------------------

        /// <summary>The batchmode entry: one scene with `-project3d-scene`, or all pending.</summary>
        public static void Migrate()
        {
            var args = Environment.GetCommandLineArgs();
            var dryRun = args.Contains("-project3d-dry-run");
            string only = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-project3d-scene") only = args[i + 1];

            var bad = Run(only, dryRun);

            if (Application.isBatchMode) EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        [MenuItem("Project3D/Migrate Scene/Dry Run (all pending)")]
        static void DryRunAll() => Run(null, dryRun: true);

        [MenuItem("Project3D/Migrate Scene/Apply (all pending)")]
        static void ApplyAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "Migrate and retire scenes",
                    "For every scene this project still holds in the old inline form, " +
                    "whose scene prefab the pipeline has published: reparent its pipeline " +
                    "objects under one instance of that prefab. Your own cameras, " +
                    "components and prefabs are carried across. A placement you moved goes " +
                    "back to the position the published layout gives it: the layout is the " +
                    "standard, so a nudge is dropped rather than kept, and named when it is. " +
                    "Each .unity is backed up first, and the run " +
                    "carries on past anything it cannot place exactly -- naming every such " +
                    "object, every consumer object the swap removes, and every reference it " +
                    "leaves dangling, in Update/<scene>.migration.json rather than refusing " +
                    "to move at all.\n\n" +
                    "For every scene the pipeline has retired: delete it, after backing it " +
                    "up. Models that nothing in the project references go too. Anything of " +
                    "yours that still uses one is left alone and named in the report.\n\n" +
                    "Continue?",
                    "Migrate", "Cancel"))
                return;
            Run(null, dryRun: false);
        }

        [MenuItem("Project3D/Migrate Scene/Retire -- dry run (what would be deleted)")]
        static void RetireDryRun() => RunRetirement(null, dryRun: true);

        /// <summary>
        /// The whole job: migrate what can be migrated, then retire what has been retired.
        ///
        /// The order is not cosmetic. Retirement deletes a scene, and the dependency scan
        /// that decides whether a model may go looks at the scenes that are left -- so a
        /// retired scene still on disk would hold its own models in place and the pass
        /// would find nothing it was allowed to delete.
        /// </summary>
        static int Run(string only, bool dryRun)
        {
            // "Was this project ever synced by the pipeline?" -- asked once, here, because
            // both passes depend on it and neither can tell on its own: a checkout with no
            // `Update/` and no manifests is somebody else's project, and silently
            // succeeding on it is the one outcome that would be read as "all good".
            var synced = AssetDatabase.IsValidFolder(ManifestsRoot) ||
                         File.Exists(PipelineLabelPath);
            if (!synced)
            {
                var reason = $"neither {ManifestsRoot}/ nor {PipelineLabelPath} exists -- " +
                             "this project has not been synced by the pipeline, so there " +
                             "is nothing to migrate and nothing that has been retired.";
                Debug.LogError($"[Project3D] migrate: {reason}");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Migrate Scene", reason, "OK");
                return 1;
            }

            var bad = RunMigrations(only, dryRun);
            bad += RunRetirement(only, dryRun);
            return bad;
        }

        static int RunMigrations(string only, bool dryRun)
        {
            var pending = PendingScenes(only, out var reason);
            if (pending == null)
            {
                // Not fatal on its own: the retirement pass reads its own file, and a
                // checkout that has pulled the retirement has every right to be missing a
                // manifest directory. `Run` has already refused the case where neither is
                // there at all.
                Debug.LogWarning($"[Project3D] migrate: {reason}");
                return 0;
            }
            if (pending.Count == 0)
            {
                Debug.Log("[Project3D] migrate: nothing to migrate -- no manifest in " +
                          $"{ManifestsRoot}/ is a delivered assembly with a published " +
                          "prefab, so there is no scene here to wrap.");
                return 0;
            }

            var bad = 0;
            foreach (var name in pending)
            {
                if (!MigrateOne(name, dryRun, out var note, out var partial))
                {
                    bad++;
                    Debug.LogError($"[Project3D] migrate {name}: REFUSED -- {note}");
                }
                else if (partial)
                {
                    // Applied, but not whole. Loud on purpose: the report is the worklist,
                    // and a run that quietly left objects behind is the outcome this tool
                    // exists to prevent.
                    Debug.LogWarning($"[Project3D] migrate {name}: applied WITH FINDINGS -- " +
                                     $"{note}  See {UpdateRoot}/{name}.migration.json.");
                }
                else
                {
                    Debug.Log($"[Project3D] migrate {name}: {note}");
                }
            }
            return bad;
        }

        /// <summary>
        /// Scenes that have a published prefab to be wrapped in, or null with `reason`
        /// set if the manifest set cannot be read at all.
        ///
        /// **Both delivered assemblies qualify, and the second one is the point.** A
        /// `prefab-pending` manifest is the owner mid-wave: the prefab has crossed and the
        /// consumer's scene has not. A `prefab` manifest is the steady state -- the prefab
        /// is published and the pipeline will never write the `.unity` again -- and a
        /// consumer whose checkout *still holds the inline form* of that scene, built
        /// before the wave, has exactly the same thing to do: wrap their own scene in the
        /// published prefab, so their cameras, colliders and components are carried onto
        /// the copies instead of being lost to a `git pull` that replaces the file. The
        /// trigger is therefore "the prefab is delivered, and this checkout may still hold
        /// the old shape", which is a question the manifest can answer.
        ///
        /// Whether a given scene still *needs* it is not answered here, because the
        /// manifest cannot see a consumer's file: `MigrateOne` opens the scene, finds the
        /// prefab instance already a root, and reports that as the steady state reached --
        /// a no-op, not a refusal. A scene whose prefab was never published, or which was
        /// never delivered at all, is not listed.
        ///
        /// `only` narrows to a single scene and is an error if that scene is not a
        /// delivered one -- naming the requested scene is more useful than silently
        /// migrating nothing.
        /// </summary>
        static List<string> PendingScenes(string only, out string reason)
        {
            reason = null;
            if (!AssetDatabase.IsValidFolder(ManifestsRoot))
            {
                reason = $"no {ManifestsRoot}/ -- this project has not been synced by the " +
                         "pipeline, so there is nothing to migrate.";
                return null;
            }

            var pending = new List<string>();
            var present = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { ManifestsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json")) continue;
                var manifest = ReadManifest(path);
                if (manifest == null || string.IsNullOrEmpty(manifest.scene)) continue;
                present.Add(manifest.scene);
                if (manifest.assembly == PendingAssembly ||
                    manifest.assembly == ThinAssembly) pending.Add(manifest.scene);
            }

            if (only != null)
            {
                if (!present.Contains(only))
                {
                    reason = $"no manifest for scene '{only}' under {ManifestsRoot}/.";
                    return null;
                }
                var manifest = ReadManifest($"{ManifestsRoot}/{only}.json");
                if (manifest?.assembly != PendingAssembly && manifest?.assembly != ThinAssembly)
                {
                    reason = $"scene '{only}' is `{manifest?.assembly ?? "unknown"}`, which " +
                             "is not a delivered assembly, so it has no published prefab " +
                             "to wrap it in. Migrate only a scene whose prefab the owner " +
                             "has published -- see `make migrate ASSET=<scene>`.";
                    return null;
                }
                return new List<string> { only };
            }
            return pending;
        }

        // --- the migration of one scene ----------------------------------------------

        static bool MigrateOne(string name, bool dryRun, out string note, out bool partial)
        {
            note = null;
            partial = false;
            var scenePath = $"{ScenesRoot}/{name}.unity";
            var prefabPath = $"{PrefabsRoot}/{ScenePrefabsFolder}/{name}.prefab";

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                note = $"{scenePath} does not exist.";
                return false;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                note = $"{prefabPath} does not exist -- the owner has not published the " +
                       "scene prefab yet (see `make migrate ASSET=<scene>`).";
                return false;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Already migrated: an instance of the scene prefab is already a root. Doing
            // it twice would reset the instance, so refuse rather than re-wrap.
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == prefabPath))
            {
                note = "already thin -- the scene already holds an instance of " +
                       $"{prefabPath}. Nothing to do.";
                // Already migrated is not a failure; it is the steady state reached.
                return true;
            }

            // Ours, by the same rule `UpdateScenes` uses: a root whose nearest prefab
            // instance root is under Prefabs/. A plain root is ours only if it is one of
            // the three named roots the pipeline creates without a prefab.
            var owned = roots.Where(IsOurs).ToList();
            var keptRoots = roots.Where(r => !IsOurs(r)).ToList();
            var kept = keptRoots.Select(r => r.name).ToList();
            if (owned.Count == 0)
            {
                note = "no pipeline-owned roots found. Either the scene is already thin " +
                       "(with a differently-named prefab) or it was never built by the " +
                       "pipeline -- nothing to wrap.";
                Abort(scene, scenePath, null);
                return false;
            }

            var report = new Report
            {
                scene = name,
                kind = "migrated",
                stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                dry_run = dryRun,
                applied = false,
                kept = kept.Select(n => new Entry { name = n, kind = "kept" }).ToArray(),
                migrated = new Entry[0],
                carried = new Entry[0],
                not_carried = new Entry[0],
                broken = new Entry[0],
            };

            // Back up the raw .unity bytes *before* touching it -- belt to git's braces.
            // A `.bak` file, not a `.unity`: Unity ignores an extension it does not
            // import, so the backup cannot become a second scene in the project.
            string backupPath = dryRun ? null : Backup(scenePath, report.stamp);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var migrated = new List<Entry>();
            var carried = new List<Entry>();
            var notCarried = new List<Entry>();
            var doomed = new List<GameObject>();
            // The prefab copy each doomed root was matched to, so the breakage scan can
            // tell the consumer's additions from the room's own objects by name.
            var matched = new Dictionary<GameObject, Transform>();

            foreach (var loose in owned)
            {
                var child = FindChild(instance.transform, loose.name);
                if (child == null)
                {
                    // The published room has no counterpart for this object, so the update
                    // is the standard and the loose object goes rather than lingering as
                    // content the pipeline removed. Nothing is left duplicated: the room
                    // carries no copy of it. A consumer object that merely *looks*
                    // pipeline-owned -- a placement they renamed, a paste of one -- is
                    // restored to the published form, because the prefab's own copy (under
                    // its published name) is a child of the instance and stays. Anything
                    // the consumer attached *under* the doomed root is named by the
                    // breakage scan below, and the scene file is backed up first.
                    notCarried.Add(new Entry
                    {
                        name = loose.name,
                        kind = "removed",
                        detail = $"the published room has no '{loose.name}'; removed to match " +
                                 $"{prefabPath} (dropped by the update, or renamed)",
                    });
                    doomed.Add(loose);
                    continue;
                }
                matched[loose] = child;

                // Where the published room puts it, read *before* anything is carried, so
                // nothing the consumer set can have moved it. This is the pose the object
                // ends at: the published layout is the standard.
                var published = PoseOf(child.gameObject);

                // The consumer's components and overrides, replayed onto the prefab copy.
                // A nudge lives in that same override list -- the loose placement is itself
                // a prefab instance, so its root transform override is one of the
                // modifications -- which is why the pose is written back afterwards rather
                // than before. Mirrors `UpdateScenes.CarryComponents`.
                foreach (var component in CarryComponents(loose, child.gameObject))
                    carried.Add(new Entry { name = loose.name, kind = "component", detail = component });

                // ... and then the published pose is re-asserted, so the replay cannot drag
                // the object to where the consumer left it. Writing the published value
                // stores no override at all -- an override is a difference from the prefab,
                // and this is now exactly the prefab's value. The nudge being undone is
                // named, so it is a worklist item rather than a silent move.
                if (!SamePose(published, PoseOf(loose)))
                    notCarried.Add(new Entry
                    {
                        name = loose.name,
                        kind = "nudge-dropped",
                        detail = "you moved this object; the published layout is the " +
                                 "standard, so it sits where the update puts it",
                    });
                Apply(child.gameObject, published);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                if (!SamePose(published, PoseOf(child.gameObject)))
                {
                    // The object survives -- on the prefab copy -- so only its transform is
                    // in question. Recorded and carried on; the loose original is still
                    // retired below, so it does not linger as a duplicate.
                    notCarried.Add(new Entry
                    {
                        name = loose.name,
                        kind = "drifted",
                        detail = "the prefab copy would not hold the published transform; " +
                                 "check it in the opened scene",
                    });
                }

                migrated.Add(new Entry
                {
                    name = loose.name,
                    kind = IsPlainRoot(loose) ? "root" : "placement",
                    detail = null,
                });
                doomed.Add(loose);
            }

            // There is deliberately no *before/after* comparison of the whole room, which is
            // what the old whole-scene abort was. It would now be a comparison against a
            // state the migration is meant to leave: a nudge is undone on purpose, so
            // "this object is not where it started" is the intended outcome and not a
            // finding. What is checked instead is per object and against the published
            // pose, in the loop above.

            // What the swap is about to take away or orphan, computed while those objects
            // still exist, so a dry run reports it on the same terms as an apply.
            var broken = CollectBreakage(instance, doomed, matched, keptRoots);

            report.migrated = migrated.ToArray();
            report.carried = carried.ToArray();
            report.not_carried = notCarried.ToArray();
            report.broken = broken.ToArray();
            report.partial = notCarried.Count > 0 || broken.Count > 0;
            partial = report.partial;

            if (dryRun)
            {
                report.applied = false;
                report.reason = "dry run -- nothing written";
                WriteReport(name, report);
                EditorSceneManager.CloseScene(scene, removeScene: true);
                note = $"dry run: would wrap {migrated.Count} object(s) under " +
                       $"{prefabPath}, keeping {kept.Count} consumer object(s)" +
                       Findings(notCarried.Count, broken.Count) + ".";
                return true;
            }

            foreach (var go in doomed) UnityEngine.Object.DestroyImmediate(go);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);

            report.applied = true;
            report.reason = null;
            WriteReport(name, report);
            note = $"migrated: {migrated.Count} object(s) now under {prefabPath}; " +
                   $"{kept.Count} consumer object(s) untouched; " +
                   $"{carried.Count} component(s) carried" +
                   Findings(notCarried.Count, broken.Count) + "; backup at {backupPath}.";
            return true;
        }

        /// <summary>
        /// Discard the in-memory changes and put the raw bytes back. Used on any refusal
        /// after the scene was opened, so a failed migration leaves no trace. The scene
        /// is closed *without* saving, so the in-memory edits are dropped; the byte copy
        /// then restores the file to the state it had before this run -- a scene the run
        /// never reached the backup for is left exactly as it was.
        /// </summary>
        static void Abort(Scene scene, string scenePath, string backupPath)
        {
            EditorSceneManager.CloseScene(scene, removeScene: true);
            if (backupPath != null && File.Exists(backupPath))
            {
                File.Copy(backupPath, scenePath, overwrite: true);
                AssetDatabase.ImportAsset(scenePath);
            }
        }

        // --- ownership and matching --------------------------------------------------

        /// <summary>
        /// Is this root one of ours? A placement carries a prefab under
        /// <see cref="PrefabsRoot"/>; the lighting, probe and post roots are plain
        /// GameObjects the pipeline creates and names. Mirrors `UpdateScenes.IsOwned`
        /// plus the three root names.
        /// </summary>
        static bool IsOurs(GameObject go)
        {
            var src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(src) && IsOwned(src)) return true;
            return IsPlainRoot(go);
        }

        static bool IsPlainRoot(GameObject go) =>
            go.name == LightingRootName || go.name == ProbesRootName || go.name == PostRootName;

        /// <summary>A prefix test on path segments -- see <c>UpdateScenes.IsOwned</c> for
        /// why a raw <c>StartsWith</c> would accept a sibling folder by mistake.</summary>
        static bool IsOwned(string prefabPath) =>
            prefabPath == PrefabsRoot || prefabPath.StartsWith(PrefabsRoot + "/");

        static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == name) return root.GetChild(i);
            for (int i = 0; i < root.childCount; i++)
            {
                var deep = FindChild(root.GetChild(i), name);
                if (deep != null) return deep;
            }
            return null;
        }

        // --- where an object is -------------------------------------------------------

        struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        static Pose PoseOf(GameObject go) => new Pose
        {
            position = go.transform.position,
            rotation = go.transform.rotation,
            scale = go.transform.lossyScale,
        };

        /// <summary>
        /// Put an object back on a pose it was measured at. Position and rotation are world
        /// and scale is local, which is only the same thing because the prefab instance the
        /// object sits under is forced to identity -- the parent chain contributes nothing,
        /// so a world pose and a local one coincide.
        /// </summary>
        static void Apply(GameObject go, Pose pose)
        {
            go.transform.position = pose.position;
            go.transform.rotation = pose.rotation;
            go.transform.localScale = pose.scale;
        }

        static bool SamePose(Pose a, Pose b) =>
            (a.position - b.position).sqrMagnitude < PositionEpsilon &&
            (a.scale - b.scale).sqrMagnitude < ScaleEpsilon &&
            Quaternion.Angle(a.rotation, b.rotation) < AngleEpsilon;

        // --- components the consumer attached ----------------------------------------

        /// <summary>
        /// Move a consumer's additions from a loose root onto its prefab copy: the
        /// overrides they made to the prefab's own components and children (a moved
        /// sub-object, a resized collider), and any component the prefab does not already
        /// provide.
        ///
        /// The consumer's component is the reason the object existed to them, so it
        /// follows the object across. A component the prefab already provides is left
        /// alone -- the prefab's is authoritative, and pasting the loose one over it
        /// would fight the layout. Uses Unity's own copy/paste rather than reflecting
        /// over fields, so a component type this tool has never heard of still moves.
        ///
        /// The two halves mirror `UpdateScenes.CarryComponents`: `from` and `to` are
        /// instances of the *same* source prefab, so the property modifications replay
        /// exactly. **The replay carries a nudge to the root as well as everything else**,
        /// because a loose placement is itself an instance and its root transform override
        /// is one of the modifications -- which is why the caller writes the published pose
        /// back *after* this returns, undoing it. Do not carry the pose here: the layout is
        /// the standard, and this is the code that would quietly restore last-year's
        /// coordinates to a re-modelled room.
        /// </summary>
        static List<string> CarryComponents(GameObject from, GameObject to)
        {
            var moved = new List<string>();

            // The consumer's overrides on the placement's own components and children --
            // a moved sub-object, a resized collider. Empty for a plain root, which is not
            // a prefab instance and carries none. The list is replayed whole, transform
            // entries and all, because the caller writes the published pose back over the
            // top; reporting the raw count rather than a filtered one keeps this honest
            // about what was replayed.
            var modifications = PrefabUtility.GetPropertyModifications(from);
            if (modifications != null && modifications.Length > 0)
            {
                PrefabUtility.SetPropertyModifications(to, modifications);
                moved.Add($"{modifications.Length} override(s) replayed");
            }

            foreach (var component in from.GetComponents<Component>())
            {
                if (component == null || component is Transform) continue;
                var type = component.GetType();
                if (to.GetComponent(type) != null) continue;
                UnityEditorInternal.ComponentUtility.CopyComponent(component);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(to);
                moved.Add(type.Name);
            }
            return moved;
        }

        // --- what the swap takes away, and what it orphans ---------------------------
        //
        // Two failures used to be silent, and are why this part of the report exists: a
        // consumer's object *under* a pipeline root is destroyed with its parent (the whole
        // subtree goes at the `DestroyImmediate` below, and `CarryComponents` never walks
        // descendants), and a reference from a consumer object to a pipeline object dangles
        // once the loose object is destroyed and the prefab's own copy takes its place. The
        // scan runs while the loose objects still exist, so a dry run reports it on the same
        // terms as an apply, and it names rather than counts.

        /// <summary>
        /// The consumer's objects the swap will destroy, and the references it will orphan.
        /// </summary>
        static List<Entry> CollectBreakage(
            GameObject instance, IList<GameObject> doomed,
            IDictionary<GameObject, Transform> matched, IList<GameObject> keptRoots)
        {
            var found = new List<Entry>();

            // Every object about to cease to exist: each doomed root and its whole subtree,
            // inactive included. This is what a surviving reference can dangle *onto*.
            var doomedSet = new HashSet<int>();
            foreach (var root in doomed)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    doomedSet.Add(t.gameObject.GetInstanceID());

            // The consumer's objects among them: present under a doomed root, absent from
            // the room's copy of it. Compared by relative path, so a *renamed* pipeline
            // child reads as the consumer's addition (which it is to this tool -- the
            // migration does not carry a rename) and an object added at any depth is caught.
            foreach (var root in doomed)
            {
                // The room's copy of this root, or the empty set when the published room
                // has no counterpart for it at all -- every object under a root the update
                // dropped is one the update does not carry, so all of them are named.
                var room = matched.TryGetValue(root, out var copy) && copy != null
                    ? RelativePaths(copy)
                    : new HashSet<string>();
                foreach (var path in RelativePaths(root.transform))
                    if (!room.Contains(path))
                        found.Add(new Entry
                        {
                            name = path,
                            kind = "destroyed",
                            // The root's own path is absent only when the room has no copy
                            // of the root at all -- it was removed, not emptied. Every
                            // deeper path is a child the room's copy does not have.
                            detail = path == root.name
                                ? $"the pipeline's '{root.name}' is not in the update; removed"
                                : $"under '{root.name}'; not in the published room",
                        });
            }

            // The references that cross from a survivor to a casualty. A reference among
            // survivors is fine; a reference among casualties dies with them. The instance's
            // own components are scanned too, because a component the consumer attached to a
            // placement was carried onto its copy -- and a carried reference to a doomed
            // object dangles exactly the same way.
            var survivors = new List<GameObject>(keptRoots) { instance };
            foreach (var root in survivors)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    foreach (var component in t.GetComponents<Component>())
                        CollectDangling(component, doomedSet, found);

            return found;
        }

        /// <summary>
        /// One component's serialized object references that point into <paramref name="doomed"/>.
        ///
        /// Walks the whole serialized tree with `Next(true)`, not `NextVisible(true)`: the
        /// references that matter most here are array *elements* -- a `UnityEvent`'s
        /// `m_Target`, a `PlayableDirector`'s `m_SceneBindings[].value` -- and `NextVisible`
        /// does not descend into an array that is not expanded in the inspector. `so.Update()`
        /// first, so effective (override-correct) values are read.
        ///
        /// `objectReferenceValue` is used rather than the raw instance ID: it resolves to the
        /// live object, which is what the membership test needs, and the cost is trivial at
        /// this size. `EditorUtility.IsPersistent` filters a reference to a project asset (a
        /// prefab, a mesh, a clip), which never dangles -- checked before the membership test
        /// so an ordinary asset reference is never reported.
        /// </summary>
        static void CollectDangling(Component component, HashSet<int> doomed, List<Entry> found)
        {
            if (component == null) return;   // a missing script leaves a null slot
            var type = component.GetType();

            try
            {
                var so = new SerializedObject(component);
                so.Update();
                var prop = so.GetIterator();
                var guard = 0;
                while (prop.Next(true) && guard++ < 100000)
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = prop.objectReferenceValue;
                    if (target == null || EditorUtility.IsPersistent(target)) continue;
                    var go = target as GameObject ?? (target as Component)?.gameObject;
                    if (go == null || !doomed.Contains(go.GetInstanceID())) continue;
                    found.Add(new Entry
                    {
                        name = component.gameObject.name,
                        kind = "reference",
                        detail = $"{type.Name}.{prop.propertyPath} -> '{go.name}'",
                    });
                }
            }
            catch (Exception)
            {
                // A component with no serializable form, or a transient one, is skipped
                // rather than allowed to stop the scan.
            }

            // A legacy `Animation` binds by transform *path*, which the walk above cannot see
            // -- and a camera pan or a moving prop is exactly the case the owner raised. The
            // path is resolved against this object as the clip's root.
            if (component is Animation animation && animation.GetClipCount() > 0)
            {
                foreach (var clip in EnumerateClips(animation))
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        var animated = AnimationUtility.GetAnimatedObject(animation.gameObject, binding);
                        if (animated == null || EditorUtility.IsPersistent(animated)) continue;
                        var go = animated as GameObject ?? (animated as Component)?.gameObject;
                        if (go == null || !doomed.Contains(go.GetInstanceID())) continue;
                        found.Add(new Entry
                        {
                            name = component.gameObject.name,
                            kind = "animation",
                            detail = $"{clip.name}: {binding.path} -> '{go.name}'",
                        });
                    }
            }
        }

        /// <summary>The clips a legacy `Animation` component holds.</summary>
        static IEnumerable<AnimationClip> EnumerateClips(Animation animation)
        {
            foreach (AnimationState state in animation)
                if (state != null && state.clip != null)
                    yield return state.clip;
        }

        /// <summary>Every object's `/`-joined path below <paramref name="root"/>, root included.</summary>
        static HashSet<string> RelativePaths(Transform root)
        {
            var paths = new HashSet<string>();
            void Walk(Transform t, string prefix)
            {
                var path = prefix.Length == 0 ? t.name : prefix + "/" + t.name;
                paths.Add(path);
                for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path);
            }
            Walk(root, "");
            return paths;
        }

        /// <summary>The "with findings" tail of a summary line, empty when there are none.</summary>
        static string Findings(int notCarried, int broken)
        {
            if (notCarried == 0 && broken == 0) return "";
            var parts = new List<string>();
            if (notCarried > 0) parts.Add($"{notCarried} not carried");
            if (broken > 0) parts.Add($"{broken} broken (see the report)");
            return "; " + string.Join(", ", parts);
        }

        // --- retiring a scene, and the models nothing uses any more -------------------

        /// <summary>
        /// The pass-level record: one document for the whole retirement, beside the
        /// per-scene `Update/&lt;scene&gt;.migration.json` this pass also writes.
        /// </summary>
        [Serializable]
        class RetirePass
        {
            public string stamp;
            public bool dry_run;
            public Entry[] scenes;      // retired scenes
            public Entry[] removed;     // asset files deleted (or that would be)
            public Entry[] kept;        // offered, and left because a scene still uses it
            public int failed;
        }

        /// <summary>
        /// Delete the scenes the pipeline has retired, and then the models nothing in this
        /// project references, from **this** checkout.
        ///
        /// Two sources, read rather than derived: `Update/pipeline.json` names the retired
        /// scenes, `Update/orphans.json` lists the files the pipeline removed from its own
        /// copy. Both are written by the publish that made the decision, so this tool
        /// carries out a decision instead of making one -- which is the only honest
        /// arrangement, because a consumer cannot see the pipeline's scene modules and the
        /// pipeline cannot see a consumer's scenes.
        ///
        /// **The scene goes first, and the asset scan depends on it.** Deciding whether a
        /// model may be deleted means asking every *remaining* scene whether it depends on
        /// it, and a retired scene still on disk depends on exactly the models being
        /// retired -- so it would hold all of them in place and the pass would report
        /// nothing it was allowed to do.
        ///
        /// Nothing is deleted without a copy in `Backups/&lt;stamp&gt;/`, and nothing a scene
        /// you still have depends on is deleted at all -- it is named in `kept` instead.
        /// That check is the reason this runs in Unity and not as a script in the pipeline:
        /// only the asset database can answer "is anything still using this".
        /// </summary>
        static int RunRetirement(string only, bool dryRun)
        {
            var label = ReadJson<PipelineLabel>(PipelineLabelPath);
            var record = ReadJson<OrphanRecord>(OrphanRecordPath);

            var retired = new List<string>();
            if (label?.retired != null) retired.AddRange(label.retired);
            if (record?.retired_scenes != null)
                foreach (var name in record.retired_scenes)
                    if (!retired.Contains(name)) retired.Add(name);
            if (only != null) retired = retired.Where(n => n == only).ToList();
            retired.Sort();

            var offered = record?.paths ?? new string[0];
            if (retired.Count == 0 && offered.Length == 0)
            {
                Debug.Log("[Project3D] migrate: nothing retired -- " +
                          $"{PipelineLabelPath} names no scene to delete and " +
                          $"{OrphanRecordPath} offers no asset.");
                return 0;
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var done = new List<Entry>();
            var bad = 0;
            foreach (var name in retired)
            {
                if (!RetireOne(name, stamp, dryRun, out var note))
                {
                    bad++;
                    Debug.LogError($"[Project3D] retire {name}: REFUSED -- {note}");
                    continue;
                }
                Debug.Log($"[Project3D] retire {name}: {note}");
                done.Add(new Entry { name = name, kind = "retired", detail = note });
                // Under the same name and in the same folder as a migration report, so a
                // reader does not have to know which pass wrote the file they are holding.
                WriteReport(name, new Report
                {
                    scene = name,
                    kind = "retired",
                    stamp = stamp,
                    dry_run = dryRun,
                    applied = !dryRun,
                    kept = new Entry[0],
                    migrated = new Entry[0],
                    carried = new Entry[0],
                    not_carried = new Entry[0],
                    broken = new Entry[0],
                    partial = false,
                });
            }

            // Ignore the retired scenes explicitly rather than relying on their having
            // been deleted: in a dry run they are still on disk, and the answer must not
            // change because the run was a rehearsal.
            var used = UsedAssetPaths(retired.Select(n => $"{ScenesRoot}/{n}.unity"));
            var removed = new List<Entry>();
            var kept = new List<Entry>();
            foreach (var relative in offered)
            {
                var path = $"{AssetsRoot}/{relative}";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                {
                    // Already gone, and that is the ordinary case rather than a problem:
                    // a consumer who pulled this publish had the file deleted by git.
                    continue;
                }
                if (used.Contains(path))
                {
                    kept.Add(new Entry { name = relative, kind = "kept",
                                         detail = "a scene in this project references it" });
                    continue;
                }
                if (!dryRun)
                {
                    Backup(path, stamp);
                    AssetDatabase.DeleteAsset(path);
                }
                removed.Add(new Entry { name = relative,
                                        kind = dryRun ? "would-remove" : "removed" });
            }

            foreach (var entry in kept)
                Debug.Log($"[Project3D] retire: kept {entry.name} -- {entry.detail}");

            var pass = new RetirePass
            {
                stamp = stamp,
                dry_run = dryRun,
                scenes = done.ToArray(),
                removed = removed.ToArray(),
                kept = kept.ToArray(),
                failed = bad,
            };
            WriteJson($"{UpdateRoot}/retired.json", pass);

            Debug.Log($"[Project3D] retire: {done.Count} scene(s), {removed.Count} asset " +
                      $"file(s){(dryRun ? " to remove" : " removed")}, {kept.Count} kept" +
                      (bad > 0 ? $", {bad} refused" : ""));
            if (!dryRun && (done.Count > 0 || removed.Count > 0))
                Debug.Log($"[Project3D] retire: backups under {BackupsRoot}/{stamp}/");
            return bad;
        }

        static bool RetireOne(string name, string stamp, bool dryRun, out string note)
        {
            note = null;
            var scenePath = $"{ScenesRoot}/{name}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                note = $"{scenePath} is already gone -- nothing to retire.";
                return true;   // the steady state, reached
            }

            var open = SceneManager.GetSceneByPath(scenePath);
            if (open.IsValid() && open.isLoaded)
            {
                note = $"{scenePath} is open in the editor. Close it and run again -- " +
                       "deleting the file of a scene that is open leaves the editor " +
                       "holding an asset that is not there.";
                return false;
            }

            if (!dryRun)
            {
                Backup(scenePath, stamp);
                // The scene prefab and the manifest are the room's own furniture, and
                // neither is any use to anything else. The prefab only exists for a thin
                // scene, so its absence is expected rather than an error.
                BackupAndDelete($"{PrefabsRoot}/{ScenePrefabsFolder}/{name}.prefab", stamp);
                BackupAndDelete($"{ManifestsRoot}/{name}.json", stamp);
                AssetDatabase.DeleteAsset(scenePath);
            }
            note = dryRun
                ? $"would delete {scenePath}, with its scene prefab and manifest if present"
                : $"deleted {scenePath}'s scene file; backup under {BackupsRoot}/{stamp}/";
            return true;
        }

        static void BackupAndDelete(string path, string stamp)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null) return;
            Backup(path, stamp);
            AssetDatabase.DeleteAsset(path);
        }

        /// <summary>
        /// Copy an asset's bytes, and its `.meta`, into the run's backup folder; return the
        /// path of the copy.
        ///
        /// A `.bak` suffix rather than a rename, and the extension is load-bearing: Unity
        /// imports by extension, so `scene_03.unity.bak` is not a second scene to find in a
        /// scene scan and `bookcase.fbx.bak` is not a second model to import. The `.meta`
        /// travels with it because it carries the GUID -- without it a restored file comes
        /// back as a *different* asset, which only matters if someone has to restore it,
        /// which is exactly when it matters.
        /// </summary>
        static string Backup(string assetPath, string stamp)
        {
            var relative = assetPath.Substring(AssetsRoot.Length + 1);
            var folder = Path.Combine(BackupsRoot, stamp, Path.GetDirectoryName(relative));
            Directory.CreateDirectory(folder);
            var file = Path.GetFileName(relative);
            var dest = Path.Combine(folder, file + ".bak");
            File.Copy(assetPath, dest, overwrite: true);
            var meta = assetPath + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, Path.Combine(folder, file + ".meta.bak"), overwrite: true);
            return dest;
        }

        /// <summary>
        /// Every asset any scene outside <paramref name="excludeScenes"/> depends on,
        /// transitively.
        ///
        /// Asked of the asset database rather than of the file text, because a Unity scene
        /// does not name what it uses -- it names fileIDs and GUIDs, and a dependency can be
        /// two hops away (scene -&gt; prefab -&gt; model). Walking `.unity` files with
        /// `Directory.GetFiles` rather than `FindAssets("t:Scene")` is deliberate: the asset
        /// filter's spelling is a Unity-version detail, while "a file ending in `.unity`
        /// under Assets/" is not -- and the `.bak` extension on a backup is exactly why
        /// this cannot pick one up.
        /// </summary>
        static HashSet<string> UsedAssetPaths(IEnumerable<string> excludeScenes)
        {
            var skip = new HashSet<string>(excludeScenes);
            var used = new HashSet<string>();
            foreach (var file in Directory.GetFiles("Assets", "*.unity",
                                                    SearchOption.AllDirectories))
            {
                var path = file.Replace(Path.DirectorySeparatorChar, '/');
                if (skip.Contains(path)) continue;
                foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                    used.Add(dependency);
            }
            return used;
        }

        static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void WriteJson(string path, object payload)
        {
            EnsureFolder(UpdateRoot);
            File.WriteAllText(path, JsonUtility.ToJson(payload, prettyPrint: true));
            AssetDatabase.ImportAsset(path);
        }

        // --- files and the manifest --------------------------------------------------

        static Manifest ReadManifest(string path)
        {
            try
            {
                return JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void WriteReport(string name, Report report)
        {
            EnsureFolder(UpdateRoot);
            var path = $"{UpdateRoot}/{name}.migration.json";
            File.WriteAllText(path, JsonUtility.ToJson(report, prettyPrint: true));
            AssetDatabase.ImportAsset(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
