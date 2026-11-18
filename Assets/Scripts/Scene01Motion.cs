using UnityEngine;
public sealed class Scene01Motion : MonoBehaviour {
 public AnimationClip characters, burner;
 public GameObject actors, prop;
 Transform[] standingBones;
 public void Sample(double time) {
   if(time<0 || time>26) return;
   characters.SampleAnimation(actors,(float)time);
   burner.SampleAnimation(prop,(float)time);
   StabilizeStandingPose(time);
 }
 public void StabilizeStandingPose(double time) {
   // The walking clip's interpolation must never leak into the speaking pose.
   // These generated skeletons use identity rotations for the standing legs.
   if(time >= 0 && time <= 18) {
     if(standingBones == null) {
       var daughter = actors.transform.Find("Daughter_Child_Schoolbag");
       var bones = new System.Collections.Generic.List<Transform>();
       if(daughter) foreach(var bone in daughter.GetComponentsInChildren<Transform>(true))
         if(bone.name == "Hips" || bone.name.Contains("UpperLeg") || bone.name.Contains("LowerLeg") || bone.name.Contains("Foot") || bone.name.Contains("Toes")) bones.Add(bone);
       standingBones = bones.ToArray();
     }
     foreach(var bone in standingBones) if(bone) bone.localRotation = Quaternion.identity;
   }
 }
}
