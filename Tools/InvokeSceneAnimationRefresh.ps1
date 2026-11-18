Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr w);[DllImport("user32.dll")] public static extern IntPtr GetSubMenu(IntPtr m,int p);[DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr m);[DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetMenuString(IntPtr m,uint i,System.Text.StringBuilder s,int n,uint f);[DllImport("user32.dll")] public static extern uint GetMenuItemID(IntPtr m,int p);[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint m,IntPtr p,IntPtr l);' -Name Refresh -Namespace SceneAnimation
$editorProcess=Get-Process Unity | Where-Object {$_.MainWindowHandle -ne 0}|Select-Object -First 1
$assetsMenu=[SceneAnimation.Refresh]::GetSubMenu([SceneAnimation.Refresh]::GetMenu($editorProcess.MainWindowHandle),2)
for($index=0;$index -lt [SceneAnimation.Refresh]::GetMenuItemCount($assetsMenu);$index++){
 $label=New-Object System.Text.StringBuilder 200
 [void][SceneAnimation.Refresh]::GetMenuString($assetsMenu,$index,$label,200,1024)
 if($label.ToString().StartsWith('Refresh')){[void][SceneAnimation.Refresh]::PostMessage($editorProcess.MainWindowHandle,273,[IntPtr]([SceneAnimation.Refresh]::GetMenuItemID($assetsMenu,$index)),[IntPtr]::Zero);Write-Output 'Unity asset refresh requested';break}
}
