using UnityEngine;
namespace IncenseCharacters
{
    // Desktop-only demonstration panel, not an XR user interface.
    public class IncenseCastPreview:MonoBehaviour
    {
        IncenseCharacterPlayer[] players;int selected;Vector2 scroll;
        void Start(){players=FindObjectsOfType<IncenseCharacterPlayer>();System.Array.Sort(players,(a,b)=>string.CompareOrdinal(a.name,b.name));}
        void OnGUI()
        {
            if(players==null||players.Length==0)return;
            GUILayout.BeginArea(new Rect(12,12,240,Mathf.Max(200,Screen.height-24)),GUI.skin.box);
            GUILayout.Label("INCENSE STORY / DESKTOP PREVIEW");
            foreach(var p in players)if(GUILayout.Button((players[selected]==p?"> ":"")+p.name))selected=System.Array.IndexOf(players,p);
            GUILayout.Label("Action: "+players[selected].currentAction);scroll=GUILayout.BeginScrollView(scroll);
            foreach(var action in players[selected].actionNames)if(GUILayout.Button(action))players[selected].PlayAction(action);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
