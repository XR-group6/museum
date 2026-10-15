using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public sealed class LocalizedUIText : MonoBehaviour
{
    string source;
    public void Bind(string text) { source=text; Refresh(); }
    void OnEnable() { SubtitleLanguage.Changed+=Refresh; }
    void OnDisable() { SubtitleLanguage.Changed-=Refresh; }
    void Refresh() { if(source!=null) GetComponent<Text>().text=SubtitleLanguage.Translate(source); }
}
