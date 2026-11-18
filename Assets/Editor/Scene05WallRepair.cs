using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Scene05WallRepair
{
    const string MeshPath = "Assets/Scene05HomecomingAnimation/Rowhouse_OperableDoor.asset";
    static Scene05WallRepair() { EditorApplication.delayCall += AutoRepair; }
    static void AutoRepair()
    {
        if (!File.Exists("Temp/scene05-wall-repair-v2.done") && !EditorApplication.isPlaying &&
            EditorSceneManager.GetActiveScene().name == "scene_05") Repair();
    }
    [MenuItem("Museum/Repair scene 05 walls from scene 04")]
    public static void Repair()
    {
        try
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before repairing walls.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "scene_05") return;
            var row = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
                .Single(f => f.name == "rowhouse_1960");
            // Scene 04 uses this house. Copy its geometry and material order together.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project3D/Prefabs/environment/rowhouse_1950.prefab")
                .GetComponentInChildren<MeshFilter>(true);
            var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            mesh.name = "Scene05 house walls from scene04";
            var vertices = mesh.vertices.Select(v => Vector3.Scale(v, source.transform.localScale)).ToArray();
            mesh.vertices = vertices;
            var materials = source.GetComponent<MeshRenderer>().sharedMaterials;
            int removed = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var triangles = source.sharedMesh.GetTriangles(s);
                var keep = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var points = new[] { triangles[i], triangles[i + 1], triangles[i + 2] }
                        .Select(k => row.transform.TransformPoint(vertices[k])).ToArray();
                    // Only remove the closed entrance door, never plaster or wall faces.
                    if (materials[s].name == "door_plank" && points.All(p => p.z < -4.49f && p.y < 1.9f && p.x > .8f)
                        && points.Any(p => p.z < -4.6f)) { removed++; continue; }
                    keep.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2], triangles[i + 2], triangles[i + 1], triangles[i] });
                }
                mesh.SetTriangles(keep, s);
            }
            mesh.RecalculateBounds();
            if (mesh.bounds.size.x < 5 || mesh.bounds.size.z < 8) throw new Exception("House scale is invalid.");
            var target = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (target) { EditorUtility.CopySerialized(mesh, target); UnityEngine.Object.DestroyImmediate(mesh); }
            else { AssetDatabase.CreateAsset(mesh, MeshPath); target = mesh; }
            row.sharedMesh = target;
            var renderer = row.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            var collider = row.GetComponent<MeshCollider>();
            if (!collider) collider = row.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = target;
            foreach (var component in new Component[] { row, renderer, collider })
            { EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/scene05-wall-repair-v2.done", "PASS: scene04 layout; bounds=" + target.bounds + "; submeshes=" + target.subMeshCount + "; entrance door triangles removed=" + removed + "; renderer and collider share mesh=" + (row.sharedMesh == collider.sharedMesh));
        }
        catch (Exception e) { File.WriteAllText("Temp/scene05-wall-repair.error", e.ToString()); Debug.LogException(e); }
    }
}
