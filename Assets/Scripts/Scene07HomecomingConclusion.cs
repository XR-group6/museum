using System.Collections;
using System.Linq;
using UnityEngine;

public sealed class Scene07HomecomingConclusion : MonoBehaviour
{
    public Transform itinerary;
    public AudioClip finalVoice;
    public Shader subtitleShader;
    PermitInspection permit;
    VRSubtitle dialogue;
    AudioSource voice;
    Material material;
    bool played;
    void Start()
    {
        var objects=gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        permit=objects.Select(t=>t.GetComponent<PermitInspection>()).FirstOrDefault(p=>p && !p.singleSheet && p.tableModel != itinerary);
        if(permit)
        {
            permit.RequiredReadSeconds=0; // Returning the certificate is sufficient; no reading timer.
            permit.ReadAndReturned+=ReadAndReturned;
            permit.documentTitle="台湾同胞旅行证明 · 叙事重构道具";
            permit.englishDocumentTitle="Taiwan Compatriot Travel Certificate · Narrative prop";
        }
        if(itinerary && permit)
        {
            var host=new GameObject("Itinerary - Inspectable");host.transform.SetParent(transform,false);
            host.SetActive(false);
            var inspection=host.AddComponent<PermitInspection>();
            inspection.singleSheet=true;
            inspection.documentTitle="返乡探亲 · 行程单";
            inspection.englishDocumentTitle="Homecoming itinerary";
            inspection.closedPrefab=inspection.openPrefab=itinerary.gameObject;
            inspection.tableModel=itinerary;
            var hit=itinerary.GetComponent<BoxCollider>();
            if(!hit)hit=itinerary.gameObject.AddComponent<BoxCollider>();
            var bounds=PermitInspection.BoundsOf(itinerary.gameObject);
            hit.center=itinerary.InverseTransformPoint(bounds.center);
            // The paper lies in its local XY plane.
            hit.size=new Vector3(.24f,.29f,.015f);
            inspection.tableCollider=hit;
            var glow=itinerary.gameObject.AddComponent<SoftInteractionHighlight>();
            inspection.tableGlow=glow;
            inspection.tour=permit.tour;inspection.viewer=permit.viewer;
            host.SetActive(true);
        }
        var go=new GameObject("Scene07 final narration subtitle");go.transform.SetParent(transform,false);
        dialogue=go.AddComponent<VRSubtitle>();dialogue.cameraOffset=new Vector3(0,.65f,2.2f);
        dialogue.fontSize=38;dialogue.subtitleWidth=1100;dialogue.canvasScale=.002f;
        if(subtitleShader){material=new Material(subtitleShader);dialogue.SetMaterial(material);}
        voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;
    }
    void ReadAndReturned(){if(played)return;played=true;StartCoroutine(Narrate());}
    IEnumerator Narrate()
    {
        dialogue.ShowText("香炉：看来你终于要回家了。我也知道，你还会回家。");
        if(finalVoice)voice.PlayOneShot(finalVoice);
        yield return new WaitForSeconds(finalVoice?finalVoice.length:8);
        dialogue.ShowText("");
    }
    void OnDestroy(){if(permit)permit.ReadAndReturned-=ReadAndReturned;if(material)Destroy(material);}
}
