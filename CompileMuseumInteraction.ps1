$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath 'D:\Github\museum'
$editorRoot = 'D:\Unity\Hub\Editor\2022.3.62f3\Editor\Data'
$compiler = Join-Path $editorRoot 'DotNetSdkRoslyn/csc.dll'
$runtime = Join-Path $editorRoot 'NetCoreRuntime/dotnet.exe'
$response = Get-ChildItem -LiteralPath 'Library/Bee/artifacts' -Recurse -Filter 'Assembly-CSharp.rsp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$response) { throw 'Unity runtime compiler response file missing.' }
New-Item -ItemType Directory -Path 'Library/MuseumCompileVerification' -Force | Out-Null
& $runtime $compiler ('@' + $response.FullName) '/out:Library/MuseumCompileVerification/Assembly-CSharp.dll'
if ($LASTEXITCODE -ne 0) { throw 'Runtime script compilation failed.' }
$editorResponse = Get-ChildItem -LiteralPath 'Library/Bee/artifacts' -Recurse -Filter 'Assembly-CSharp-Editor.rsp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$xrSource = 'Assets/Editor/MuseumOpenVRSetup.cs'
$extraSources = @()
if ((Get-Content -Raw -LiteralPath $editorResponse.FullName) -notmatch 'MuseumOpenVRSetup.cs') { $extraSources += $xrSource }
& $runtime $compiler ('@' + $editorResponse.FullName) '/out:Library/MuseumCompileVerification/Assembly-CSharp-Editor.dll' @extraSources
if ($LASTEXITCODE -ne 0) { throw 'Editor script compilation failed.' }
Write-Output 'Museum runtime and editor compilation passed with Unity compiler and project references.'
