using UnityEngine;
[ExecuteAlways,RequireComponent(typeof(TextMesh))]
public sealed class Scene05DocumentLabel:MonoBehaviour
{
 Font font;
 void OnEnable(){font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial Unicode MS","Arial"},48);var t=GetComponent<TextMesh>();t.font=font;t.characterSize=.0032f;GetComponent<MeshRenderer>().sharedMaterial=font.material;}
 void OnDisable(){if(font){if(Application.isPlaying)Destroy(font);else DestroyImmediate(font);}}
}


