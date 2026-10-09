$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath 'D:\Github\museum'
if (Test-Path 'Logs/MuseumRemovalValidation.log') {
    Select-String -Path 'Logs/MuseumRemovalValidation.log' -Pattern '\[Removal validation\]|error CS|Exception:|executeMethod|Aborting batchmode' | ForEach-Object { Write-Output $_.Line }
    Get-Content 'Logs/MuseumRemovalValidation.log' -Tail 5
    Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | ForEach-Object { Write-Output $_.CommandLine }
    return
}
Select-String -LiteralPath 'Assets/SteamVR/Prefabs/[CameraRig].prefab' -Pattern '^Camera:','m_Name:','m_TargetEye:','m_Depth:','m_TargetDisplay:' | ForEach-Object { Write-Output $_.Line }
Select-String -LiteralPath 'Assets/Project3D/Scenes/scene_01.unity' -Pattern '^Camera:','m_TargetEye:','m_Depth:','m_TargetDisplay:','value:.*Camera','m_Name:.*Camera' | ForEach-Object { Write-Output $_.Line }
$openvrPackage = Get-ChildItem Library/PackageCache -Directory -Filter 'com.valvesoftware.unity.openvr*' | Select-Object -First 1
if ($openvrPackage) { rg -n 'enum.*Mirror|MirrorView|LeftEye|RightEye' $openvrPackage.FullName -g '*.cs' }
Get-ChildItem Assets/XR -Recurse -File -Filter '*.asset' | ForEach-Object { Write-Output $_.FullName; Get-Content -LiteralPath $_.FullName }
Get-Content ProjectSettings/EditorBuildSettings.asset
rg -n 'AssignLoader|TryAddLoader|GetOrCreateSettings|InitManagerOnStart|automaticLoading|automaticRunning' Library/PackageCache/com.unity.xr.management* Assets/SteamVR/OpenVRUnityXRPackage -g '*.cs'
Get-Content -LiteralPath 'Assets/Scripts/MuseumInteractionSetup.complete' -ErrorAction SilentlyContinue
Get-Content -LiteralPath 'ProjectSettings/EditorBuildSettings.asset'
Get-ChildItem -LiteralPath 'Assets/Timelines' -ErrorAction SilentlyContinue | Select-Object Name,Length
Get-ChildItem -LiteralPath 'Assets/SteamVR_Input' -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue | Select-Object FullName
rg -n 'SceneAudioTimeline|RightControllerDummy|rightHand:|censerColliders:|sequence:|timeline:|ambience:|playOnStart:|nextScene:' Assets/Project3D/Scenes/scene_01.unity Assets/Project3D/Scenes/scene_02.unity
rg -n 'fullSourceFilePath|initializationClass|GetActionsFilePath|streamingAssetsPath' Assets/SteamVR/Input/Editor/SteamVR_Input_Generator.cs Assets/SteamVR/Input/SteamVR_Input.cs
Get-Content -LiteralPath 'Assets/Timelines/CicadasTimeline.playable','Assets/Timelines/MuseumVoicesTimeline.playable'
Select-String -LiteralPath 'Assets/SteamVR/Prefabs/[CameraRig].prefab' -Pattern 'poseAction:','actionPath:','inputSource:','m_Name: Controller' | ForEach-Object { Write-Output $_.Line }
Select-String -LiteralPath 'Assets/Project3D/Scenes/scene_01.unity' -Pattern 'actionPath:','inputSource:','1222106202' | ForEach-Object { Write-Output $_.Line }
Get-ChildItem -LiteralPath 'Assets/SteamVR_Input' -Recurse -Filter '*.cs' | ForEach-Object { Write-Output $_.FullName }
Get-ChildItem -LiteralPath 'Library/ScriptAssemblies' -Filter 'Assembly-CSharp*.dll' | ForEach-Object { Write-Output ($_.Name + ' ' + $_.LastWriteTime) }
Select-String -LiteralPath "$env:LOCALAPPDATA/Unity/Editor/Editor.log" -Pattern '\[Museum\]|error CS|Mute Audio' | Select-Object -Last 12 | ForEach-Object { Write-Output $_.Line }
Get-ChildItem Assets/MP3 -Filter '*.meta' | ForEach-Object { $_.Name; Get-Content -LiteralPath $_.FullName -TotalCount 2 }
Get-Content -LiteralPath "$env:LOCALAPPDATA/Unity/Editor/Editor.log" -Tail 25
Get-ChildItem -LiteralPath 'Library/Bee' -Recurse -Filter '*Assembly-CSharp*.rsp' | ForEach-Object { Write-Output $_.FullName }
Get-ChildItem -LiteralPath 'Assets/Scripts' -Filter '*.cs' | ForEach-Object { Write-Output ($_.Name + ' ' + $_.LastWriteTime) }
