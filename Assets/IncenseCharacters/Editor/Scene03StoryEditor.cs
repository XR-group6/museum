using UnityEditor;
using UnityEngine;

namespace IncenseCharacters.Editor
{
    [CustomEditor(typeof(Scene03Story))]
    public sealed class Scene03StoryEditor : UnityEditor.Editor
    {
        bool preview;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var story = (Scene03Story)target;
            EditorGUILayout.HelpBox("0–5 睁眼/眨眼；5–10 女儿看炉、抬头说话；11–20 父亲说话，一手摸头、一手托炉；21–23 一起看前方；23–30 盖布留缝。配音可拖入两个 Voice 槽位。", MessageType.Info);
            if (Application.isPlaying) return;
            if (GUILayout.Button(preview ? "结束预览并还原" : "预览当前秒数"))
            {
                if (preview) { story.Restore(); preview = false; }
                else { story.Sample(story.previewTime); preview = true; }
                SceneView.RepaintAll();
            }
            if (preview)
            {
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider("预览时间", story.previewTime, 0, 30);
                if (EditorGUI.EndChangeCheck()) { story.Sample(time); SceneView.RepaintAll(); }
            }
        }
        void OnDisable() { if (preview && target) ((Scene03Story)target).Restore(); }
    }
}
