using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public sealed class LocalizedUIText : MonoBehaviour
{
    string source;
    string englishSource;
    public void Bind(string text) { source=text; englishSource=null; Refresh(); }
    public void BindBilingual(string chinese, string english) { source=chinese; englishSource=english; Refresh(); }
    void OnEnable() { SubtitleLanguage.Changed+=Refresh; }
    void OnDisable() { SubtitleLanguage.Changed-=Refresh; }
    void Refresh() { if(source!=null) GetComponent<Text>().text=SubtitleLanguage.English && englishSource!=null ? englishSource : SubtitleLanguage.Translate(source); }
}
