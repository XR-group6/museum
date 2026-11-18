# Scene 05 wall repair

Scene 05 uses the scene 04 house layout in Rowhouse_OperableDoor.asset. Its vertices include the FBX import scale; the scene instance stays at scale 1. The material slots come from the same source mesh. Only the entrance door triangles are removed for the existing door animation.

The rowhouse_1960 placement in Assets/Project3D/SceneManifests/scene_05.json intentionally has guard=false: GeometryCheck otherwise replaces this authored mesh with the canonical FBX, losing the geometry and scale on scene open and before Play Mode. All other placements keep their existing guard settings.

Rebuild using Museum > Repair scene 05 walls from scene 04 while scene_05 is open. Verification reopened the saved scene and waited for the automatic geometry checks before inspecting and rendering it. See Logs/Scene05WallRepair-validation.txt and Previews/Scene05Homecoming/Walls_Verified.png.
