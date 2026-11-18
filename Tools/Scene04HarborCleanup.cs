using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Scene04HarborCleanup{
 static Scene04HarborCleanup(){EditorApplication.delayCall+=Run;}
 static void Run(){if(File.Exists("Temp/Scene04Harbor.cleanup.done"))return;var scene=SceneManager.GetActiveScene();if(scene.name!="scene_04_01"||EditorApplication.isPlaying)return;
 string[] names={"Scene04 dining burner preview","Harbor QA floorplan","Harbor plan camera"};int removed=0;
 foreach(var root in scene.GetRootGameObjects().Where(g=>names.Contains(g.name)).ToArray()){var c=root.GetComponent<Camera>();if(c&&c.targetTexture){var rt=c.targetTexture;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);}Object.DestroyImmediate(root);removed++;}
 var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Scene01CenserView>(true)).First();
 foreach(var t in new[]{view.storyBurner,view.carouselBurner,view.scene02.burner,view.scene03.burner,view.scene04.burner})foreach(var r in t.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=false;
 view.director.time=0;view.director.Evaluate();view.scene04.Sample(0);view.scene04.caption.text="";view.ApplyView();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("Temp/Scene04Harbor.cleanup.done","Removed temporary preview cameras="+removed);
 }
}
