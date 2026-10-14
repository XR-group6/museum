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
    /// The geometry consistency check: pipeline-owned objects a room may not lose, restored when a
    /// consumer edits or deletes them.
    ///
    /// What it protects, and what it does not
    /// --------------------------------------
    /// A scene's manifest may mark a placement with `guard: true` and carry a
    /// `guard_meshes` fingerprint beside it -- the mesh name, triangle count, local mesh
    /// bounds and material names of the geometry as the pipeline built it (written by
    /// `run_scene.write_manifest`; see `lib.fingerprint.boundary`). This file compares
    /// the delivered object against that fingerprint and puts the canonical geometry back
    /// when it does not match:
    ///
    ///   * the checked object is missing from the scene prefab -- it is re-instantiated
    ///     from the model prefab at the manifest pose;
    ///   * the checked object is missing from the open scene's instance of that prefab --
    ///     the same, as an instance override;
    ///   * its mesh was repointed or edited (ProBuilderized, faces deleted) -- the mesh
    ///     reference is put back to the model prefab's canonical mesh, or the whole
    ///     object is replaced if it will not heal.
    ///
    /// **Geometry only.** The check never reads, compares or rewrites an existing
    /// object's transform: a consumer who moves a checked object has moved it, and the
    /// check leaves the move alone. Pose math exists solely to place a brand-new
    /// replacement when the old object is gone.
    ///
    /// **The consumer's own objects are never touched.** Ownership is decided the way
    /// `MigrateScene` decides it: by prefab provenance under `Prefabs/`, plus the check's
    /// own placement list from the manifest. A consumer's camera, their props and their
    /// own top-level objects are invisible to this file.
    ///
    /// **The model prefab is the canonical copy, and it is checked first.** A restore
    /// reads the canonical mesh from `Prefabs/&lt;category&gt;/&lt;asset&gt;.prefab`. If that
    /// prefab itself fails the fingerprint -- someone edited the FBX-derived prefab -- there
    /// is no locally canonical copy to restore *from*, so the finding is named and no
    /// restore is attempted: the next publish rewrites it. This is a check against casual
    /// editing, not a security boundary: the manifest and this script both sit in the
    /// consumer's project, and a person determined to edit the house can edit the check.
    ///
    /// When it runs
    /// ------------
    /// Automatically: after a domain load (the prefab pass), when a scene is opened, and
    /// on entering play mode. The automatic lane is silent when everything is intact, logs
    /// one warning per restore, writes `Update/&lt;scene&gt;.check.json` when it acted, and
    /// never saves the consumer's `.unity` -- its fixes to the open scene are in memory
    /// and marked dirty. The scene prefab (`Prefabs/scenes/`) is pipeline-owned, so the
    /// prefab surface *is* written. It is batchmode-gated, so `make unity` is unaffected.
    ///
    ///     Project3D &gt; Geometry Consistency Check &gt; Verify &amp; Restore (open scene)
    ///     Project3D &gt; Geometry Consistency Check &gt; Verify &amp; Restore (all scenes)
    ///
    /// or, for one scene, in batchmode:
    ///
    ///     Unity -batchmode -nographics -quit -projectPath shared \
    ///           -executeMethod Project3D.GeometryCheck.Run -project3d-scene scene_06
    ///
    /// add `-project3d-dry-run` to stop short of writing.
    ///
    /// It is deliberately **self-contained**, for the same reason `MigrateScene` is: the
    /// injected build scripts are deleted after every `make unity` run, so nothing here
    /// may call into them. What is duplicated from `UpdateScenes.cs` is called out where
    /// it happens.
    /// </summary>
    public static class GeometryCheck
    {
        // Mirrored from `UpdateScenes.cs` / `MigrateScene.cs`; this file ships alone and
        // cannot reference either, so the copies are named where they are duplicated.
        const string ManifestsRoot     = "Assets/Project3D/SceneManifests";
        const string PrefabsRoot       = "Assets/Project3D/Prefabs";
        const string ScenePrefabsFolder = "scenes";
        const string ScenesRoot        = "Assets/Project3D/Scenes";
        const string UpdateRoot        = "Assets/Project3D/Update";
        const string ReportSuffix      = ".check.json";

        const string FatAssembly = "fat";       // mirrored from MigrateScene.cs

        // The tolerance a mesh box is compared with. A millimetre -- tight enough that a
        // hit is a real edit and not float noise from the import round-trip.
        const float BoundsEpsilon = 1e-3f;

        // --- the manifest ----------------------------------------------------------

        /// <summary>
        /// The manifest fields this check reads, bound by name exactly as
        /// `write_manifest` writes them. A renamed key on either side parses to
        /// false/null and the check silently stops firing, so
        /// `tests/test_geometry.py` line-scans this block against
        /// `fingerprint.BOUNDARY_FIELDS` -- change one, change the other.
        /// </summary>
        [Serializable]
        class Box
        {
            public float[] min;
            public float[] max;
        }

        [Serializable]
        class CheckMesh
        {
            // Named for the check that consumes it; its *fields* are the fingerprint's,
            // and the class name itself is not on the wire.
            public string name;
            public int tris;
            public float[] min;
            public float[] max;
            public string[] materials;
        }

        [Serializable]
        class Placement
        {
            public string asset;
            public string name;
            public float[] at;      // creation pose only -- never compared against
            public float rz;        // creation pose only -- never compared against
            public Box bounds;      // carried, not compared
            public bool guard;      // bound to the manifest key `guard`
            public CheckMesh[] guard_meshes;
        }

        [Serializable]
        class Manifest
        {
            public string scene;
            public string assembly;
            public Placement[] placements;
        }

        // --- the report --------------------------------------------------------------

        [Serializable]
        class Entry
        {
            public string name;
            public string kind;     // "removed" | "mesh-changed" | "replaced" | "restored"
                                    // | "model-tampered" | "model-missing" | "prefab-missing"
                                    // | "skipped" | "note"
            public string detail;
        }

        [Serializable]
        class Report
        {
            public string scene;
            public string kind;         // "check"
            public string stamp;
            public bool dry_run;
            public bool applied;        // false under -project3d-dry-run
            public Entry[] findings;
            public Entry[] restored;
        }

        // --- entry points ------------------------------------------------------------

        /// <summary>
        /// The automatic lane: prefab pass after a domain load, instance pass on scene
        /// open, and a last check on entering play mode. Gated on batchmode so the
        /// headless `make unity` pass -- which opens and creates scenes itself -- is
        /// never tangled with, and on compile/update state so the deferred pass does
        /// not run against a half-loaded asset database.
        /// </summary>
        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (Application.isBatchMode) return;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += DeferredPrefabPass;
        }

        static void DeferredPrefabPass()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            RunOnce(null, interactive: false, save: false, dryRun: false, openScenes: false);
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
                RunOnce(scene.name, interactive: false, save: false, dryRun: false,
                        openScenes: false);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Restoring here is legal and is the "on play" half of the promise: the
            // fixes land in memory, so play runs canonical geometry even though the
            // consumer's file on disk is not rewritten.
            if (state == PlayModeStateChange.ExitingEditMode &&
                !Application.isBatchMode)
            {
                var scene = SceneManager.GetActiveScene();
                RunOnce(scene.IsValid() ? scene.name : null, interactive: false,
                        save: false, dryRun: false, openScenes: false);
            }
        }

        /// <summary>The batchmode entry: one scene with `-project3d-scene`, or all checked.</summary>
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var dryRun = args.Contains("-project3d-dry-run");
            string only = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-project3d-scene") only = args[i + 1];

            // Batchmode opens each checked scene in turn, so the instance surface is
            // checked and its fixes can be saved -- the automatic lane never runs here.
            var bad = RunOnce(only, interactive: true, save: true, dryRun: dryRun,
                              openScenes: true);

            if (Application.isBatchMode) EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        [MenuItem("Project3D/Geometry Consistency Check/Verify & Restore (open scene)")]
        static void VerifyOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            var bad = RunOnce(scene.IsValid() ? scene.name : null, interactive: true,
                              save: true, dryRun: false, openScenes: false);
            Summarise(bad);
        }

        [MenuItem("Project3D/Geometry Consistency Check/Verify & Restore (all scenes)")]
        static void VerifyAllScenes()
        {
            var bad = RunOnce(null, interactive: true, save: true, dryRun: false,
                              openScenes: false);
            Summarise(bad);
        }

        static void Summarise(int bad)
        {
            if (bad > 0)
                EditorUtility.DisplayDialog(
                    "Geometry Consistency Check",
                    $"The check found {bad} placement(s) it could not restore -- their " +
                    "canonical copy is missing or tampered. See the Console and " +
                    $"{UpdateRoot}/ for the named findings.",
                    "OK");
        }

        // --- the one pass ------------------------------------------------------------

        /// <summary>
        /// Verify and restore every checked placement this project carries.
        ///
        /// The prefab surface runs before the instance surface, and the instance pass
        /// re-measures before acting -- that ordering is what prevents a healed prefab
        /// from being "restored" twice, and what lets each surface fix only what is
        /// genuinely broken in it. Do not reorder.
        /// </summary>
        static int RunOnce(string only, bool interactive, bool save, bool dryRun,
                           bool openScenes)
        {
            var manifests = CheckedManifests();
            if (only != null) manifests = manifests.Where(m => m.scene == only).ToList();

            if (manifests.Count == 0)
            {
                if (interactive)
                    Debug.Log("[Project3D] check: nothing checked here -- no manifest " +
                              "under " + ManifestsRoot + "/ carries a checked placement.");
                return 0;
            }

            var bad = 0;
            foreach (var manifest in manifests)
            {
                var findings = new List<Entry>();
                var restored = new List<Entry>();
                var report = new Report
                {
                    scene = manifest.scene,
                    kind = "check",
                    stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                    dry_run = dryRun,
                    applied = !dryRun,
                    findings = new Entry[0],
                    restored = new Entry[0],
                };

                // Surface A: the scene prefab. Pipeline-owned, so a restore here is
                // written back into the asset rather than left for the consumer to save.
                bad += CheckScenePrefab(manifest, dryRun, interactive, findings, restored);

                // Surface B: an open instance of the scene. The consumer's file, so the
                // automatic lane fixes it in memory and marks it dirty; explicit runs
                // and batchmode save it.
                var scene = SceneFor(manifest.scene, openScenes);
                if (scene.IsValid())
                    bad += CheckOpenScene(scene, manifest, save, dryRun, interactive,
                                          findings, restored);

                report.findings = findings.ToArray();
                report.restored = restored.ToArray();

                if (interactive || findings.Count > 0)
                    WriteReport(manifest.scene, report);

                if (restored.Count > 0)
                    Debug.LogWarning($"[Project3D] check {manifest.scene}: restored " +
                                     $"{restored.Count} -- {Names(restored)}. " +
                                     $"See {UpdateRoot}/{manifest.scene}{ReportSuffix}.");
                else if (interactive)
                    Debug.Log($"[Project3D] check {manifest.scene}: intact, nothing to do.");
            }
            return bad;
        }

        static Scene SceneFor(string name, bool openScenes)
        {
            var open = SceneManager.GetSceneByName(name);
            if (open.IsValid()) return open;
            if (!openScenes) return default;
            var path = $"{ScenesRoot}/{name}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                return default;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        static string Names(List<Entry> entries)
            => string.Join(", ", entries.Select(e => e.name));

        // --- the prefab surface --------------------------------------------------------

        static int CheckScenePrefab(Manifest manifest, bool dryRun, bool interactive,
                                    List<Entry> findings, List<Entry> restored)
        {
            if (manifest.assembly == FatAssembly)
            {
                // The placements live inline in the consumer's .unity -- the form
                // MigrateScene is for. The prefab does not carry the room, so there is
                // nothing here to check; say so on explicit runs and leave it.
                if (interactive)
                    Debug.Log($"[Project3D] check {manifest.scene}: assembly is `fat` -- " +
                              "the layout lives in the team's scene, which this check does " +
                              "not police.");
                return 0;
            }

            var prefabPath = $"{PrefabsRoot}/{ScenePrefabsFolder}/{manifest.scene}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                findings.Add(new Entry
                {
                    name = manifest.scene,
                    kind = "prefab-missing",
                    detail = $"{prefabPath} does not exist; the next publish rebuilds it. " +
                             "This check does not fabricate a layout.",
                });
                Debug.LogWarning($"[Project3D] check {manifest.scene}: {prefabPath} is " +
                                 "missing -- nothing to check.");
                return 0;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var bad = 0;
            var changed = false;
            try
            {
                foreach (var placement in Checked(manifest))
                    bad += CheckChild(root.transform, "the scene prefab", placement,
                                      dryRun, interactive, findings, restored,
                                      ref changed);
            }
            finally
            {
                // Written only when something was put back, so an intact prefab keeps
                // its fileIDs and its bytes -- the property a geometry-only publish
                // depends on.
                if (changed && !dryRun)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.LogWarning($"[Project3D] check {manifest.scene}: wrote restores " +
                                     $"into {prefabPath}.");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            return bad;
        }

        // --- the open-scene surface ---------------------------------------------------

        static int CheckOpenScene(Scene scene, Manifest manifest, bool save, bool dryRun,
                                  bool interactive, List<Entry> findings,
                                  List<Entry> restored)
        {
            if (manifest.assembly == FatAssembly)
            {
                if (interactive)
                    Debug.Log($"[Project3D] check {manifest.scene}: assembly is `fat` -- " +
                              "the scene is the consumer's; MigrateScene's domain, not " +
                              "this check's.");
                return 0;
            }

            var prefabPath = $"{PrefabsRoot}/{ScenePrefabsFolder}/{manifest.scene}.prefab";
            var root = scene.GetRootGameObjects().FirstOrDefault(r =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == prefabPath);
            if (root == null)
            {
                // An inline checkout of a scene the pipeline already delivers thin --
                // the wrap is MigrateScene's one-time job, and until it runs the
                // instance surface has nothing to check. The prefab surface above
                // already ran.
                if (interactive)
                    Debug.Log($"[Project3D] check {manifest.scene}: no instance of " +
                              $"{prefabPath} at the scene root -- the checkout is still " +
                              "inline, so this check watches the prefab only.");
                return 0;
            }

            var bad = 0;
            var changed = false;
            foreach (var placement in Checked(manifest))
                bad += CheckChild(root.transform, "the scene instance", placement,
                                  dryRun, interactive, findings, restored, ref changed);

            if (changed && !dryRun)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (save) EditorSceneManager.SaveScene(scene);
            }
            return bad;
        }

        // --- the per-placement check ----------------------------------------------------

        /// <summary>
        /// Verify one checked placement under <paramref name="parent"/> (the scene
        /// prefab's root, or the scene instance's root) and restore it when it is
        /// missing, replaced, or carrying the wrong geometry. Returns 1 when the
        /// canonical copy itself is missing or tampered and nothing could be restored.
        ///
        /// Geometry only: an existing object's transform is never read, compared or
        /// written. A moved object passes through untouched.
        /// </summary>
        static int CheckChild(Transform parent, string surface, Placement placement,
                              bool dryRun, bool interactive, List<Entry> findings,
                              List<Entry> restored, ref bool changed)
        {
            var child = FindChild(parent, placement.name);

            if (child == null)
            {
                findings.Add(new Entry
                {
                    name = placement.name,
                    kind = "removed",
                    detail = $"{placement.name} is gone from {surface}.",
                });
                if (!ModelIntact(placement, out var note))
                {
                    Debug.LogError($"[Project3D] check: cannot restore '{placement.name}' " +
                                   $"-- {note}");
                    findings.Add(new Entry
                    {
                        name = placement.name,
                        kind = ModelPrefab(placement) == null ? "model-missing"
                                                              : "model-tampered",
                        detail = note,
                    });
                    return 1;
                }
                if (!dryRun)
                {
                    InstantiateModel(placement, ModelPrefab(placement), parent);
                    changed = true;
                    restored.Add(new Entry
                    {
                        name = placement.name,
                        kind = "restored",
                        detail = "re-instantiated from the model prefab at the manifest pose.",
                    });
                }
                return 0;
            }

            // The child exists but is not an instance of the model prefab -- unpacked,
            // or swapped for another asset. A prefab instance's source is not
            // assignable, so it is replaced.
            var modelPath = ModelPrefabPath(placement);
            if (modelPath == null)
            {
                Debug.LogError($"[Project3D] check: no prefab for asset " +
                               $"'{placement.asset}' under {PrefabsRoot}/; '{placement.name}' " +
                               "was not checked.");
                findings.Add(new Entry { name = placement.name, kind = "model-missing",
                                         detail = $"no prefab for asset '{placement.asset}'." });
                return 1;
            }
            // A null source is a plain object where the prefab instance should be --
            // unpacked, or hand-made -- and it is replaced the same way a swapped
            // source is: the delivered structure is one model-prefab instance per
            // placement, and that structure is part of what is being checked.
            var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child);
            if (source != modelPath)
            {
                findings.Add(new Entry
                {
                    name = placement.name,
                    kind = "replaced",
                    detail = source == null
                        ? "was not a prefab instance at all"
                        : $"was an instance of {source}, not of {modelPath}.",
                });
                if (!ModelIntact(placement, out var note))
                {
                    Debug.LogError($"[Project3D] check: cannot restore '{placement.name}' " +
                                   $"-- {note}");
                    findings.Add(new Entry
                    {
                        name = placement.name,
                        kind = ModelPrefab(placement) == null ? "model-missing"
                                                              : "model-tampered",
                        detail = note,
                    });
                    return 1;
                }
                if (!dryRun)
                {
                    ReplaceChild(child, placement, ModelPrefab(placement));
                    changed = true;
                    restored.Add(new Entry
                    {
                        name = placement.name,
                        kind = "restored",
                        detail = "replaced with a fresh instance of the model prefab.",
                    });
                }
                return 0;
            }

            // Mesh identity, per checked mesh entry.
            var details = MeshDifferences(child, placement, out var matchingFilters);
            if (details.Count == 0)
            {
                if (interactive)
                    foreach (var extra in ExtraMeshes(child, placement))
                        findings.Add(new Entry { name = extra, kind = "note",
                                                 detail = "an extra mesh under the checked " +
                                                          "object; not compared and left alone." });
                return 0;
            }

            findings.Add(new Entry
            {
                name = placement.name,
                kind = "mesh-changed",
                detail = string.Join("; ", details),
            });

            if (!ModelIntact(placement, out var tamper))
            {
                Debug.LogError($"[Project3D] check: cannot restore '{placement.name}' -- " +
                               $"{tamper}");
                findings.Add(new Entry
                {
                    name = placement.name,
                    kind = ModelPrefab(placement) == null ? "model-missing"
                                                          : "model-tampered",
                    detail = tamper,
                });
                return 1;
            }

            if (dryRun) return 0;

            // The repair ladder: revert the instance's overrides first (that undoes a
            // repointed or ProBuilderized mesh without touching anything the consumer
            // owns), then repoint the mesh at the model prefab's canonical copy, and
            // only replace the whole object when neither heals it.
            var healed = false;
            foreach (var filter in matchingFilters)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(filter.gameObject))
                    PrefabUtility.RevertObjectOverride(filter, InteractionMode.AutomatedAction);
            }
            if (MeshDifferences(child, placement, out _).Count == 0) healed = true;

            if (!healed)
            {
                foreach (var filter in matchingFilters)
                {
                    var canonical = CanonicalMesh(placement, filter.sharedMesh.name);
                    if (canonical == null) continue;
                    filter.sharedMesh = canonical;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                }
                if (MeshDifferences(child, placement, out _).Count == 0) healed = true;
            }

            if (!healed)
            {
                ReplaceChild(child, placement, ModelPrefab(placement));
                restored.Add(new Entry
                {
                    name = placement.name,
                    kind = "restored",
                    detail = "the mesh would not heal; the whole object was replaced.",
                });
            }
            else
            {
                restored.Add(new Entry
                {
                    name = placement.name,
                    kind = "restored",
                    detail = "mesh put back to the canonical geometry.",
                });
            }
            changed = true;
            return 0;
        }

        /// <summary>
        /// Compare every checked mesh entry against the meshes under
        /// <paramref name="child"/>, keyed by mesh name. Returns one detail line per
        /// mismatch, and the MeshFilters that matched a checked entry (the repair
        /// ladder works on exactly these, so a consumer's added mesh can never be
        /// "repaired").
        /// </summary>
        static List<string> MeshDifferences(GameObject child, Placement placement,
                                            out List<MeshFilter> matching)
        {
            var details = new List<string>();
            matching = new List<MeshFilter>();
            if (placement.guard_meshes == null || placement.guard_meshes.Length == 0)
            {
                details.Add("the manifest carries no guard_meshes fingerprint for it");
                return details;
            }

            var filters = child.GetComponentsInChildren<MeshFilter>(true)
                               .Where(f => f.sharedMesh != null).ToList();
            // The import's own scale, measured from the canonical prefab rather than from
            // `child`: an instance a consumer has scaled must not move the expectation.
            var prefab = ModelPrefab(placement);
            foreach (var expected in placement.guard_meshes)
            {
                var filter = FindCheckedMesh(filters, placement, expected);
                if (filter == null)
                {
                    details.Add($"mesh '{expected.name}' is missing");
                    continue;
                }
                matching.Add(filter);

                var mesh = filter.sharedMesh;
                var fileScale = MeshScale(prefab, mesh.name);
                if (mesh.triangles.Length / 3 != expected.tris)
                    details.Add($"mesh '{expected.name}' has {mesh.triangles.Length / 3} " +
                                $"tris, expected {expected.tris}");
                else if (!SameMeshBounds(expected.min, expected.max, mesh.bounds, fileScale))
                    details.Add($"mesh '{expected.name}' bounds " +
                                $"{BoundsText(InMetres(mesh.bounds, fileScale))} " +
                                "do not match the manifest");
                else if (!MaterialsMatch(expected.materials, filter))
                    details.Add($"mesh '{expected.name}' material slots differ");
            }
            return details;
        }

        static IEnumerable<string> ExtraMeshes(GameObject child, Placement placement)
        {
            // Resolved through the same lookup the comparison uses, so a checked mesh is
            // never also reported as an extra one -- which is what a placement the scene
            // renamed would otherwise produce, on the same object the check just claimed.
            var filters = child.GetComponentsInChildren<MeshFilter>(true)
                               .Where(f => f.sharedMesh != null).ToList();
            var checkedNames = new HashSet<string>(
                (placement.guard_meshes ?? new CheckMesh[0])
                    .Select(m => FindCheckedMesh(filters, placement, m))
                    .Where(f => f != null)
                    .Select(f => f.sharedMesh.name));
            return filters.Where(f => !checkedNames.Contains(f.sharedMesh.name))
                          .Select(f => f.sharedMesh.name);
        }

        /// <summary>
        /// The mesh under the checked object that a fingerprint entry names, or null when
        /// the object holds none.
        ///
        /// The entry's `name` is Blender's object name, and for a placement that name is
        /// the *placement's*; the mesh inside a model prefab is named after the **asset**
        /// instead. Measured, and exact: across 193 prefab lines in a build log,
        /// `mesh='X'` equals the prefab's own file name every time. The two agree for
        /// every placement whose name is its asset's, which is all but two -- scene 03
        /// renames its second house `coast_house_02` and its second roof `coast_roof_03`,
        /// and guards both. So the fingerprinted name is tried first, because it is the
        /// name the pipeline itself recorded, and the asset's name second.
        /// </summary>
        static MeshFilter FindCheckedMesh(List<MeshFilter> filters, Placement placement,
                                          CheckMesh expected)
            => filters.FirstOrDefault(f => f.sharedMesh.name == expected.name)
               ?? filters.FirstOrDefault(f => f.sharedMesh.name == placement.asset);

        /// <summary>
        /// `Mesh.bounds` is in the imported mesh's local space, and that space is neither
        /// Blender's nor Unity's world: it is Blender's mesh space with **x negated** and
        /// **scaled by the import's unit conversion**. Both were measured rather than
        /// assumed -- the reflection from the FBX vertex array (recorded in
        /// `UpdateScenes.cs`, "Unity negates x on FBX import"), and the scale from a build
        /// log: across 193 single-mesh prefab lines, `meshbox` is the build report's box
        /// at exactly 1/100 on every axis, while the same prefab's renderer reports the
        /// report's numbers, so the model root carries the reciprocal. The manifest holds
        /// Blender's own metres, so both have to be undone here, once, and nowhere else.
        ///
        /// The scale is passed in rather than applied by the caller because it comes from
        /// the canonical prefab (see `MeshScale`), which is the pipeline-owned reference
        /// and not the child under test.
        /// </summary>
        static bool SameMeshBounds(float[] min, float[] max, Bounds mesh, float fileScale)
        {
            var wantMin = new Vector3(-max[0], min[1], min[2]);
            var wantMax = new Vector3(-min[0], max[1], max[2]);
            var want = new Bounds();
            want.SetMinMax(wantMin, wantMax);
            var got = InMetres(mesh, fileScale);
            return Vector3.Distance(want.center, got.center) <= BoundsEpsilon &&
                   Vector3.Distance(want.size, got.size) <= BoundsEpsilon;
        }

        /// <summary>
        /// A mesh-space box, in the manifest's metres. `fileScale` is `MeshScale`'s
        /// metres per mesh unit -- ~100 for these files, because the mesh arrives at
        /// 1/100 of the manifest's numbers -- so the conversion **multiplies**. Dividing
        /// by it puts the comparison 10^4 out and reports every guarded shell as
        /// tampered, which is exactly the failure this line produced.
        /// </summary>
        static Bounds InMetres(Bounds mesh, float fileScale)
        {
            var factor = fileScale > 1e-9f ? fileScale : 1f;
            return new Bounds(mesh.center * factor, mesh.size * factor);
        }

        /// <summary>
        /// The factor from a canonical model prefab's mesh space to the manifest's
        /// metres -- about 100 for these files, whose FBX carries `UnitScaleFactor` 1.0 and
        /// which Unity nonetheless reads as centimetre-based. `ImportPostprocessor` has
        /// the measurement: with the conversion off the same asset arrives at 224 instead
        /// of 2.24.
        ///
        /// **Measured from the prefab rather than written down as a hundred**, for the
        /// reason that file already gives: the importer's unit handling has been flipped
        /// twice, and the version history there is the record of it. A constant would rot
        /// in the one direction that matters -- a check that never fires again, because
        /// both sides moved together.
        ///
        /// The factor is taken from the *magnitude* of the two size vectors because the
        /// model root also carries the FBX axis conversion, a quarter turn about x, and a
        /// quarter turn permutes the axes of an axis-aligned box without changing the
        /// length of its size vector. A rotation that is not a quarter turn about an axis
        /// would inflate the world box -- `renderer.bounds` is an axis-aligned box -- and
        /// so measure a factor that is too large, which reports a mismatch: an honest
        /// failure rather than a silent pass.
        ///
        /// Returns 1, with a named warning, when the prefab holds no mesh to measure: the
        /// bounds comparison then runs unscaled and reports the mismatch, which is
        /// truthful, instead of the check dying on a null.
        /// </summary>
        static float MeshScale(GameObject prefab, string meshName)
        {
            if (prefab != null)
            {
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || mesh.name != meshName) continue;
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    var local = mesh.bounds.size.magnitude;
                    if (local > 1e-6f)
                        return renderer.bounds.size.magnitude / local;
                }
            }
            Debug.LogWarning($"[Project3D] check: nothing on the canonical prefab to " +
                             $"measure the import scale of mesh '{meshName}' from; its " +
                             "bounds were compared unscaled.");
            return 1f;
        }

        static string BoundsText(Bounds bounds)
            => $"({bounds.min.x:0.###}, {bounds.min.y:0.###}, {bounds.min.z:0.###})" +
               $"..({bounds.max.x:0.###}, {bounds.max.y:0.###}, {bounds.max.z:0.###})";

        static bool MaterialsMatch(string[] expected, MeshFilter filter)
        {
            var renderer = filter.GetComponent<Renderer>();
            var actual = (renderer != null
                ? renderer.sharedMaterials.Where(m => m != null).Select(m => m.name)
                : new string[0]).OrderBy(n => n).ToArray();
            var want = (expected ?? new string[0]).OrderBy(n => n).ToArray();
            return want.SequenceEqual(actual);
        }

        // --- the canonical copy --------------------------------------------------------

        /// <summary>
        /// The model prefab's meshes, measured against the manifest fingerprint. False
        /// means the canonical copy itself is edited -- there is nothing locally intact
        /// to restore *from*, so every restore for this placement must refuse; the next
        /// publish rewrites the prefab from the FBX.
        /// </summary>
        static bool ModelIntact(Placement placement, out string note)
        {
            var prefab = ModelPrefab(placement);
            if (prefab == null)
            {
                note = $"no prefab for asset '{placement.asset}' under {PrefabsRoot}/ -- " +
                       "the next publish rebuilds it.";
                return false;
            }
            var differences = MeshDifferences(prefab, placement, out _);
            if (differences.Count == 0)
            {
                note = null;
                return true;
            }
            note = $"{PrefabsRoot}/…/{placement.asset}.prefab itself fails the " +
                   $"fingerprint ({string.Join("; ", differences)}) -- pull the next " +
                   "publish, which rewrites it from the FBX.";
            return false;
        }

        static GameObject ModelPrefab(Placement placement)
        {
            var path = ModelPrefabPath(placement);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static string ModelPrefabPath(Placement placement)
        {
            // The prefab the child is an instance of is the best answer -- the index is
            // the fallback for a child with no source to ask.
            var index = PrefabIndex();
            return index.TryGetValue(placement.asset, out var path) ? path : null;
        }

        /// <summary>The canonical mesh for one checked entry, read from the model prefab.</summary>
        static Mesh CanonicalMesh(Placement placement, string meshName)
        {
            var prefab = ModelPrefab(placement);
            if (prefab == null) return null;
            var filter = prefab.GetComponentsInChildren<MeshFilter>(true)
                               .FirstOrDefault(f => f.sharedMesh != null &&
                                                    f.sharedMesh.name == meshName);
            return filter != null ? filter.sharedMesh : null;
        }

        static void ReplaceChild(GameObject child, Placement placement,
                                 GameObject modelPrefab)
        {
            var parent = child.transform.parent;
            UnityEngine.Object.DestroyImmediate(child);
            InstantiateModel(placement, modelPrefab, parent);
        }

        /// <summary>
        /// A fresh instance of the model prefab, at the placement's manifest pose.
        ///
        /// `ToUnity` and `YawAboutBlenderUp` are duplicates of `UpdateScenes`' (that
        /// file is deleted after every build, so this one ships alone with its own
        /// copies). The pose math exists for exactly this one case -- creating a
        /// replacement for an object that is gone. An existing object's transform is
        /// never written.
        /// </summary>
        static GameObject InstantiateModel(Placement placement, GameObject modelPrefab,
                                           Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab,
                                                                 parent.gameObject.scene);
            go.name = placement.name;
            go.transform.SetParent(parent, false);

            if (placement.at == null)
            {
                Debug.LogWarning($"[Project3D] check: '{placement.name}' carries no " +
                                 "position in the manifest; it was re-created at the origin.");
                return go;
            }

            // Composed with the prefab's rotation, never assigned over it: every model's
            // root carries the FBX axis conversion, and assigning rotation outright
            // removes it -- the failure `UpdateScenes` measured when it produced a house
            // 3.85 tall on the wrong axis. And `-placement.rz`: `ToUnity` is a
            // reflection, and a yaw composed through a reflection inverts its sense.
            var position = ToUnity(placement.at);
            var baseRotation = modelPrefab.transform.rotation;
            var rotation = baseRotation * YawAboutBlenderUp(baseRotation, -placement.rz);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            return go;
        }

        static Vector3 ToUnity(float[] at) => new Vector3(-at[0], at[2], -at[1]);

        static Quaternion YawAboutBlenderUp(Quaternion rootRotation, float rzDegrees)
            => Quaternion.AngleAxis(rzDegrees, Quaternion.Inverse(rootRotation) * Vector3.up);

        // --- files, the manifest, the index ---------------------------------------------

        static List<Manifest> CheckedManifests()
        {
            var found = new List<Manifest>();
            if (!AssetDatabase.IsValidFolder(ManifestsRoot)) return found;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset",
                                                          new[] { ManifestsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json")) continue;
                var manifest = ReadManifest(path);
                if (manifest == null || string.IsNullOrEmpty(manifest.scene)) continue;
                if (manifest.placements != null &&
                    manifest.placements.Any(p => p.guard)) found.Add(manifest);
            }
            return found;
        }

        static IEnumerable<Placement> Checked(Manifest manifest)
            => manifest.placements.Where(p => p.guard);

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
            var path = $"{UpdateRoot}/{name}{ReportSuffix}";
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

        /// <summary>
        /// asset name -> prefab path, built from the Prefabs tree. Duplicate of
        /// `UpdateScenes.PrefabIndex()`: this file ships alone and cannot call it. The
        /// scene prefabs are excluded by path, for the same reason the original is.
        /// </summary>
        static Dictionary<string, string> PrefabIndex()
        {
            var index = new Dictionary<string, string>();
            var scenePrefabs = $"{PrefabsRoot}/{ScenePrefabsFolder}/";
            foreach (var path in AssetDatabase
                         .FindAssets("t:Prefab", new[] { PrefabsRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => !p.StartsWith(scenePrefabs))
                         .OrderBy(p => p))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (index.TryGetValue(name, out var previous))
                {
                    Debug.LogError($"[Project3D] two prefabs claim the asset name " +
                                   $"'{name}': {previous} and {path}.");
                    continue;
                }
                index[name] = path;
            }
            return index;
        }

        static GameObject FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child.gameObject;
                var deeper = FindChild(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
