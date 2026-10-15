#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace XRTeam.DebugTools.Editor
{
    [Serializable]
    public sealed class WorkbenchSettings
    {
        public float cameraSpeed=2, lookSpeed=80, handSpeed=.7f, handTurnSpeed=75;
        public bool showOverlay=true, collisionMovement, muteAudio, rightHand=true;
        public float collisionRadius=.15f, defaultEyeHeight=1.65f;
        public Color markerColor=new Color(.3f,.85f,1);
        public static string Key=>"XRTeam.Workbench."+Application.dataPath;
        public static WorkbenchSettings Load(){try{return JsonUtility.FromJson<WorkbenchSettings>(EditorPrefs.GetString(Key+".Settings",""))??new WorkbenchSettings();}catch{return new WorkbenchSettings();}}
        public void Save(){EditorPrefs.SetString(Key+".Settings",JsonUtility.ToJson(this));}
    }
    [Serializable] public sealed class ViewBookmark {public string label,scene;public Vector3 position;public Quaternion rotation;}
    [Serializable] public sealed class BookmarkCollection {public List<ViewBookmark> items=new List<ViewBookmark>();}
    public static class WorkbenchBookmarks
    {
        public static BookmarkCollection Load(){try{return JsonUtility.FromJson<BookmarkCollection>(EditorPrefs.GetString(WorkbenchSettings.Key+".Bookmarks",""))??new BookmarkCollection();}catch{return new BookmarkCollection();}}
        public static void Save(BookmarkCollection data){EditorPrefs.SetString(WorkbenchSettings.Key+".Bookmarks",JsonUtility.ToJson(data));}
    }
}
#endif
