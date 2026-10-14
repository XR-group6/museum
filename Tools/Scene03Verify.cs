using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using IncenseCharacters;

public static class Scene03Verify
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_03.unity");
            var story = UnityEngine.Object.FindObjectOfType<Scene03Story>();
            if (!story || !story.father || !story.daughter || !story.burner) throw new Exception("Unresolved scene references");
            Vector3 fp = story.father.position, dp = story.daughter.position;
            Quaternion fr = story.father.rotation, dr = story.daughter.rotation;
            var camera = new GameObject("QA Camera").AddComponent<Camera>();
            camera.nearClipPlane = .02f; camera.fieldOfView = 55; camera.aspect = 1.5f;
            camera.transform.position = new Vector3(2.2f, 2.3f, .3f);
            camera.transform.LookAt((fp + dp) * .5f + Vector3.up * .95f);
            var light = new GameObject("QA light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(35, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            Directory.CreateDirectory("Frames");
            foreach (float t in new[] { 0f, 5f, 8f, 14f, 19f, 22f, 30f, 8f, 0f })
            {
                story.Sample(t);
                if (story.father.position != fp || story.daughter.position != dp || story.father.rotation != fr || story.daughter.rotation != dr) throw new Exception("Actor root moved");
                if (t == 0 && story.EyeOpening != 0) throw new Exception("Eye must start closed");
                if (t == 30 && Mathf.Abs(story.ClothOpening - .03f) > .001f) throw new Exception("Final cloth slit incorrect");
                if (t == 14)
                {
                    var f = story.father.GetComponent<IncenseCharacterPlayer>();
                    var d = story.daughter.GetComponent<IncenseCharacterPlayer>();
                    var head = d.FindBone("Head");
                    Vector3 crown = head.position + head.up * .20f + Vector3.up * .022f + story.father.forward * .055f;
                    float contact = Vector3.Distance(f.FindBone("LeftHand").position, crown);
                    Debug.Log("SCENE03 crown wrist error=" + contact);
                    Debug.Log("SCENE03 reach upper=" + f.FindBone("LeftUpperArm").position + " crown=" + crown + " hand=" + f.FindBone("LeftHand").position);
                    if (contact > .035f) throw new Exception("Patting hand cannot reach crown");
                    Vector3 support = story.father.TransformPoint(story.carryOffset + new Vector3(0, -.07f, -.035f));
                    float supportError = Vector3.Distance(f.FindBone("RightHand").position, support);
                    Debug.Log("SCENE03 support error=" + supportError);
                    if (supportError > .035f) throw new Exception("Supporting hand cannot reach vessel");
                }
                Render(camera, "Frames/pose_" + t + ".png");
                Debug.Log("SCENE03 SAMPLE " + t + " burner=" + story.burner.position);
            }
            story.Sample(14);
            camera.transform.position = story.father.TransformPoint(story.carryOffset + new Vector3(0, .20f, .10f));
            Transform headF = story.father.GetComponent<IncenseCharacterPlayer>().FindBone("Head");
            Transform headD = story.daughter.GetComponent<IncenseCharacterPlayer>().FindBone("Head");
            camera.transform.LookAt(Vector3.Lerp(headD.position, headF.position, .65f));
            camera.fieldOfView = 105;
            story.BuildEffects(camera.transform);
            foreach (float t in new[] { 0f, 2f, 5f, 14f, 26f, 30f }) { story.Sample(t); Render(camera, "Frames/view_" + t + ".png"); }
            story.Restore();
            story.director.time = 14; story.director.Evaluate();
            if (Mathf.Abs(story.previewTime - 14) > .01f) throw new Exception("Director seek failed");
            story.Restore();
            Debug.Log("SCENE03 VERIFIED: references, all beats, nonmonotonic seeking, locked actor roots, closed opening and final slit.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Render(Camera camera, string path)
    {
        var skins = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>();
        var baked = new System.Collections.Generic.List<GameObject>();
        foreach (var skin in skins)
        {
            var mesh = new Mesh(); skin.BakeMesh(mesh);
            var go = new GameObject("Baked QA pose", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation); go.transform.localScale = skin.transform.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false; baked.Add(go);
        }
        var rt = new RenderTexture(900, 600, 24); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(900, 600, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 900, 600), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); camera.targetTexture = null; RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
        foreach (var go in baked) { UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh); UnityEngine.Object.DestroyImmediate(go); }
        foreach (var skin in skins) skin.enabled = true;
    }
}
