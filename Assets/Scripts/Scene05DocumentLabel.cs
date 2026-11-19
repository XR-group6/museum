using UnityEngine;
[ExecuteAlways,RequireComponent(typeof(TextMesh))]
public sealed class Scene05DocumentLabel:MonoBehaviour
{
    Font font;
    void OnEnable()
    {
        if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial Unicode MS","Arial"},48);
        var text=GetComponent<TextMesh>();
        text.font=font;text.characterSize=.0032f;
        GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }
    void OnDestroy(){if(font){if(Application.isPlaying)Destroy(font);else DestroyImmediate(font);}}
}
