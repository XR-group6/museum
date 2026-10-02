"""Optional: open Blender Scripting, load this file, edit PACKAGE_DIR, Run Script.
Requires a Blender installation with glTF import and FBX export enabled.
Creates separate FBX files with animation actions. Not executed in this package's build environment.
"""
import bpy
from pathlib import Path
PACKAGE_DIR = r'C:\Users\YOUR_NAME\Downloads\Incense_Characters_v2'
base=Path(PACKAGE_DIR)
if not (base/'GLB').is_dir():raise RuntimeError('Edit PACKAGE_DIR to your extracted Incense_Characters folder first.')
(base/'FBX').mkdir(exist_ok=True)
for path in sorted((base/'GLB').glob('*.glb')):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 for action in list(bpy.data.actions):
  if action.users==0:bpy.data.actions.remove(action)
 bpy.ops.import_scene.gltf(filepath=str(path))
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.export_scene.fbx(filepath=str(base/'FBX'/(path.stem+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0)
print('FBX export finished. Check clips and material appearance in Unity before using the export.')
