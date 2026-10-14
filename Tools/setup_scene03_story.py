"""Wire scene 03 without altering the user's character prefab overrides; make an isolated QA project."""
from pathlib import Path
import shutil
import uuid

root = Path(__file__).resolve().parents[1]
runtime = root / 'Assets/IncenseCharacters/Runtime'
shader_guid = '5cae3274581647adbb659fd42b955693'
shader_meta = runtime / 'Scene03Cover.shader.meta'
if not shader_meta.exists():
    shader_meta.write_text(f'fileFormatVersion: 2\nguid: {shader_guid}\nShaderImporter:\n  externalObjects: {{}}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
else:
    shader_guid = next(x.split(': ')[1] for x in shader_meta.read_text().splitlines() if x.startswith('guid:'))
guids = {'Scene03Story': '4f5c00287ebd4bbf8f8c4bb28a394361', 'Scene03StoryPlayable': '15fa41a188ec4662ad3f24b0b05a36f7'}
for name, guid in guids.items():
    path = runtime / (name + '.cs.meta')
    if not path.exists():
        path.write_text(f'fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    else:
        guids[name] = next(x.split(': ')[1] for x in path.read_text().splitlines() if x.startswith('guid:'))
asset = root / 'Assets/IncenseCharacters/Generated/Scene03Story.asset'
asset.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: SCRIPT_GUID, type: 3}
  m_Name: Scene03Story
  m_EditorClassIdentifier: 
'''.replace('SCRIPT_GUID', guids['Scene03StoryPlayable']))
asset_guid = 'df21eca64a894e34869788f0b56fb403'
Path(str(asset) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {asset_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
scene = root / 'Assets/Project3D/Scenes/scene_03.unity'
text = scene.read_text(encoding='utf-8')
backup = root / 'Temp/Scene03_before_story.unity'
if not backup.exists():
    backup.write_text(text, encoding='utf-8')
if 'm_Name: Scene03_Story' not in text:
    documents = '''--- !u!4 &903030010 stripped
Transform:
  m_CorrespondingSourceObject: {fileID: 7851043124935377643, guid: f45e5615b1fa0384c85022c961314279, type: 3}
  m_PrefabInstance: {fileID: 836417906}
  m_PrefabAsset: {fileID: 0}
--- !u!4 &903030011 stripped
Transform:
  m_CorrespondingSourceObject: {fileID: 8395879316821870369, guid: 0b7a4a517189cce41becb01bc50ce84d, type: 3}
  m_PrefabInstance: {fileID: 1943626887}
  m_PrefabAsset: {fileID: 0}
--- !u!4 &903030012 stripped
Transform:
  m_CorrespondingSourceObject: {fileID: 9182325097360103130, guid: 729444523bbf45fc38cd7a145849e1e4, type: 3}
  m_PrefabInstance: {fileID: 358036586}
  m_PrefabAsset: {fileID: 0}
--- !u!1 &903030000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 903030001}
  - component: {fileID: 903030002}
  - component: {fileID: 903030003}
  m_Layer: 0
  m_Name: Scene03_Story
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &903030001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 903030000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &903030002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 903030000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: SCRIPT_GUID, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  father: {fileID: 903030010}
  daughter: {fileID: 903030011}
  burner: {fileID: 903030012}
  director: {fileID: 903030003}
  daughterVoice: {fileID: 0}
  fatherVoice: {fileID: 0}
  carryOffset: {x: 0, y: 1.02, z: 0.3}
  previewTime: 0
--- !u!320 &903030003
PlayableDirector:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 903030000}
  m_Enabled: 1
  serializedVersion: 3
  m_PlayableAsset: {fileID: 11400000, guid: ASSET_GUID, type: 2}
  m_InitialState: 1
  m_WrapMode: 1
  m_DirectorUpdateMode: 1
  m_InitialTime: 0
  m_SceneBindings: []
  m_ExposedReferences:
    m_References: []
'''.replace('SCRIPT_GUID', guids['Scene03Story']).replace('ASSET_GUID', asset_guid)
    text = text.replace('--- !u!1660057539', documents + '--- !u!1660057539')
    text += '  - {fileID: 903030001}\n'
    scene.write_text(text, encoding='utf-8')
text = scene.read_text(encoding='utf-8')
text = text.replace('carryOffset: {x: 0, y: 1.02, z: 0.3}', 'carryOffset: {x: 0, y: 1.13, z: 0.27}')
scene.write_text(text, encoding='utf-8')
if '  coverShader:' not in text:
    text = text.replace('  fatherVoice: {fileID: 0}\n', f'  fatherVoice: {{fileID: 0}}\n  coverShader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}\n')
    scene.write_text(text, encoding='utf-8')

qa = root / 'Temp/Scene03Verification'
for folder in ['Assets/IncenseCharacters', 'Assets/Project3D']:
    shutil.copytree(root / folder, qa / folder, dirs_exist_ok=True)
# Existing editor utilities reference unrelated project systems. QA needs only the story inspector and runner.
editor_dir = qa / 'Assets/IncenseCharacters/Editor'
for path in editor_dir.glob('*.cs'):
    if path.name not in ['Scene03StoryEditor.cs']:
        path.unlink()
(qa / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
shutil.copy2(root / 'Tools/Scene03Verify.cs', qa / 'Assets/Editor/Scene03Verify.cs')
(qa / 'Packages').mkdir(exist_ok=True)
(qa / 'Packages/manifest.json').write_text('{"dependencies":{"com.unity.timeline":"1.8.6","com.unity.modules.audio":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.xr":"1.0.0"}}')
(qa / 'ProjectSettings').mkdir(exist_ok=True)
shutil.copy2(root / 'ProjectSettings/ProjectVersion.txt', qa / 'ProjectSettings/ProjectVersion.txt')
print('Scene wired; existing father/daughter overrides preserved. Verification project:', qa)
