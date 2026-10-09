Set-Location -LiteralPath 'D:\Github\museum'
$xr = Get-ChildItem Library/PackageCache -Directory -Filter 'com.unity.xr.management*' | Select-Object -First 1
Write-Output $xr.FullName
Get-Content -LiteralPath "$($xr.FullName)/Editor/XRGeneralSettingsPerBuildTarget.cs"
Get-Content -LiteralPath "$($xr.FullName)/Editor/Metadata/XRPackageMetadataStore.cs" | Select-String 'AssignLoader' -Context 4,35 | ForEach-Object { $_.ToString() }
Get-Content -LiteralPath "$($xr.FullName)/Runtime/XRGeneralSettings.cs" -TotalCount 100
Get-Content -LiteralPath "$($xr.FullName)/Runtime/XRManagerSettings.cs" -TotalCount 130
