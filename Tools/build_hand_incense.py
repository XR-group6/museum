"""Build the hand incense prop and add it to the existing scene without rebuilding it."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
PROPS = ROOT / 'Assets/IncenseCharacters/Props'
SCENE = ROOT / 'Assets/Project3D/Scenes/scene_02.unity'
TIMELINE = ROOT / 'Assets/IncenseCharacters/Generated/IncenseStory.playable'


def write(path, text):
    path.write_text(text, encoding='utf-8', newline='\n')


def meta(path, importer, body=''):
    target = Path(str(path) + '.meta')
    if not target.exists():
        write(target, f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{importer}:\n  externalObjects: {{}}\n{body}  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return re.search(r'guid: (\w+)', target.read_text()).group(1)


def header(kind, ident, label):
    return f'''--- !u!{kind} &{ident}
{label}:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
'''


def game_object(ident, name, components, active=1):
    return header(1, ident, 'GameObject') + '  serializedVersion: 6\n  m_Component:\n' + ''.join(f'  - component: {{fileID: {c}}}\n' for c in components) + f'''  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {active}
'''


def transform(ident, go, parent, pos, scale, children=()):
    return header(4, ident, 'Transform') + f'''  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}
  m_ConstrainProportionsScale: 0
''' + ('  m_Children:\n' + ''.join(f'  - {{fileID: {c}}}\n' for c in children) if children else '  m_Children: []\n') + f'''  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''


def main():
    PROPS.mkdir(exist_ok=True)
    folder_meta = Path(str(PROPS) + '.meta')
    if not folder_meta.exists():
        write(folder_meta, f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    material_template = (ROOT / 'Assets/IncenseCharacters/Generated/Father_Young/ClothTrim.mat').read_text()
    materials = []
    for name, color in [('IncenseBamboo', '0.62, g: 0.22, b: 0.09'), ('IncensePowder', '0.48, g: 0.30, b: 0.12'), ('IncenseEmber', '0.95, g: 0.22, b: 0.035')]:
        mat = PROPS / (name + '.mat')
        text = material_template.replace('m_Name: ClothTrim', 'm_Name: ' + name)
        text = re.sub(r'- _Color: .*', '- _Color: {r: ' + color + ', a: 1}', text)
        if name == 'IncenseEmber':
            text = text.replace('m_ValidKeywords: []', 'm_ValidKeywords:\n  - _EMISSION')
            text = text.replace('_EmissionColor: {r: 0, g: 0, b: 0, a: 1}', '_EmissionColor: {r: 1.5, g: 0.18, b: 0.015, a: 1}')
            text = re.sub(r'm_LightmapFlags: \d+', 'm_LightmapFlags: 1', text)
        write(mat, text)
        materials.append(meta(mat, 'NativeFormatImporter', '  mainObjectFileID: 2100000\n'))
    parts = []
    for stick, x in enumerate((-0.0045, 0, 0.0045)):
        # Each cylinder is two units tall before scaling; the lower 20 cm reaches into ash.
        for segment, (name, bottom, height, diameter) in enumerate((('Bamboo', -0.2, 0.1, 0.003), ('Powder', -0.1, 0.274, 0.0034), ('Ember', 0.174, 0.006, 0.0036))):
            ident = 100 + stick * 30 + segment * 5
            parts.append((ident, f'{name}_{stick + 1}', x, bottom + height / 2, height / 2, diameter, materials[segment]))
    prefab_text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + game_object(1000, 'HandIncense', [1001], 0)
    prefab_text += transform(1001, 1000, 0, (0, 0, 0), (1, 1, 1), [p[0] + 1 for p in parts])
    for ident, name, x, y, half_height, diameter, mat in parts:
        prefab_text += game_object(ident, name, [ident + 1, ident + 2, ident + 3])
        prefab_text += transform(ident + 1, ident, 1001, (x, y, 0), (diameter, half_height, diameter))
        prefab_text += header(33, ident + 2, 'MeshFilter') + f'  m_GameObject: {{fileID: {ident}}}\n  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}\n'
        prefab_text += header(23, ident + 3, 'MeshRenderer') + f'''  m_GameObject: {{fileID: {ident}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {mat}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
'''
    prefab = PROPS / 'HandIncense.prefab'
    write(prefab, prefab_text)
    prop_guid = meta(prefab, 'PrefabImporter')
    timeline = TIMELINE.read_text()
    scene = SCENE.read_text()
    if 'm_Name: Father_Incense\n' in timeline:
        assert 'Father_HandIncense' in scene
        print('Prop rebuilt; existing Timeline setup preserved.')
        return
    # Copy the existing clip serialization, changing only activation-related fields.
    father_track = timeline.split('  m_Name: Father_Animation\n', 1)[1]
    clip = '  - m_Version: 1\n' + father_track.split('  - m_Version: 1\n', 2)[1]
    clip = clip.replace('m_Duration: 5', 'm_Duration: 3.7')
    clip = re.sub(r'm_DisplayName: .*', 'm_DisplayName: Hold incense', clip)
    clip = re.sub(r'm_Asset: \{fileID: -?\d+\}', 'm_Asset: {fileID: 74100002}', clip)
    clip = re.sub(r'm_ParentTrack: \{fileID: -?\d+\}', 'm_ParentTrack: {fileID: 74100001}', clip)
    clip = clip.replace('m_PostExtrapolationMode: 1', 'm_PostExtrapolationMode: 0').replace('m_PreExtrapolationMode: 1', 'm_PreExtrapolationMode: 0')
    package = ROOT / 'Library/PackageCache/com.unity.timeline@1.8.6/Runtime/Activation'
    def script_guid(name):
        return re.search(r'guid: (\w+)', (package / (name + '.cs.meta')).read_text()).group(1)
    def mono(ident, guid, name):
        return header(114, ident, 'MonoBehaviour') + f'''  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
'''
    timeline = timeline.replace('  m_FixedDuration: 0', '  - {fileID: 74100001}\n  m_FixedDuration: 0', 1)
    timeline += mono(74100001, script_guid('ActivationTrack'), 'Father_Incense') + '''  m_Version: 3
  m_AnimClip: {fileID: 0}
  m_Locked: 0
  m_Muted: 0
  m_CustomPlayableFullTypename: 
  m_Curves: {fileID: 0}
  m_Parent: {fileID: 11400000}
  m_Children: []
  m_Clips:
''' + clip + '  m_Markers:\n    m_Objects: []\n  m_PostPlaybackState: 1\n'
    timeline += mono(74100002, script_guid('ActivationPlayableAsset'), 'ActivationPlayableAsset of Hold incense')
    # Nest the new prefab at the father's existing grip without touching other overrides.
    father_prefab = (ROOT / 'Assets/IncenseCharacters/Generated/Father_Young/Father_Young.prefab').read_text()
    grip_id = re.search(r'm_Component:\n  - component: \{fileID: (\d+)\}\n  m_Layer: 0\n  m_Name: Grip_R', father_prefab).group(1)
    father_guid = re.search(r'guid: (\w+)', (ROOT / 'Assets/IncenseCharacters/Generated/Father_Young/Father_Young.prefab.meta').read_text()).group(1)
    # Match from the last PrefabInstance header preceding the father's source reference.
    end = scene.index('  m_SourcePrefab: {fileID: 100100000, guid: ' + father_guid)
    start = scene.rfind('--- !u!1001 &', 0, end)
    father_block = scene[start:end]
    father_instance = re.search(r'&([0-9]+)', father_block).group(1)
    assert '    m_AddedGameObjects: []' in father_block
    modified = father_block.replace('    m_AddedGameObjects: []', f'''    m_AddedGameObjects:
    - targetCorrespondingSourceObject: {{fileID: {grip_id}, guid: {father_guid}, type: 3}}
      insertIndex: -1
      addedObject: {{fileID: 174100004}}''')
    scene = scene[:start] + modified + scene[end:]
    binding = '    value: {fileID: 1072180116}\n'
    assert scene.count(binding) == 1
    scene = scene.replace(binding, binding + '  - key: {fileID: 74100001, guid: ca2631ef862f4c84d8a148a38ebc396c, type: 2}\n    value: {fileID: 174100003}\n')
    new_objects = f'''--- !u!4 &174100001 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {grip_id}, guid: {father_guid}, type: 3}}
  m_PrefabInstance: {{fileID: {father_instance}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!1001 &174100002
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 174100001}}
    m_Modifications:
'''
    for target, field, value in [(1000, 'm_Name', 'Father_HandIncense'), (1001, 'm_LocalPosition.x', 0.108), (1001, 'm_LocalPosition.y', -0.016), (1001, 'm_LocalPosition.z', -0.005), (1001, 'm_LocalRotation.z', -0.7071068), (1001, 'm_LocalRotation.w', 0.7071068), (1001, 'm_LocalEulerAnglesHint.z', -90)]:
        new_objects += f'''    - target: {{fileID: {target}, guid: {prop_guid}, type: 3}}
      propertyPath: {field}
      value: {value}
      objectReference: {{fileID: 0}}
'''
    new_objects += f'''    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {prop_guid}, type: 3}}
--- !u!1 &174100003 stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: 1000, guid: {prop_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 174100002}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!4 &174100004 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 1001, guid: {prop_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 174100002}}
  m_PrefabAsset: {{fileID: 0}}
'''
    roots = scene.find('--- !u!1660057539')
    scene = scene[:roots] + new_objects + scene[roots:] if roots >= 0 else scene + new_objects
    for text in (scene, timeline, prefab_text):
        ids = re.findall(r'^--- !u!\d+ &(-?\d+)', text, re.M)
        assert len(ids) == len(set(ids)), 'Duplicate Unity object IDs'
    write(TIMELINE, timeline)
    write(SCENE, scene)
    print('Created three incense sticks, attached to Grip_R, active on Timeline [0, 3.7).')


if __name__ == '__main__':
    main()
