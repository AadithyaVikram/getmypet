param([switch]$Launch)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $project 'artifacts/win-x64'
$install = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/DragonBreak'
$exe = Join-Path $install 'DragonBreak.exe'
if (-not (Test-Path (Join-Path $publish 'DragonBreak.exe'))) { throw 'Publish the app first. See README.md.' }
foreach ($process in (Get-Process -Name DragonBreak -ErrorAction SilentlyContinue)) {
    if ($process.Path -and [string]::Equals($process.Path, $exe, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Quit DragonBreak from its tray menu before updating.'
    }
}
New-Item -ItemType Directory -Path $install -Force | Out-Null
Get-ChildItem -LiteralPath $publish -Force | Copy-Item -Destination $install -Recurse -Force
$shell = New-Object -ComObject WScript.Shell
try {
    foreach ($location in @([Environment]::GetFolderPath('DesktopDirectory'), [Environment]::GetFolderPath('Programs'))) {
        $shortcut = $shell.CreateShortcut((Join-Path $location 'DragonBreak.lnk'))
        $shortcut.TargetPath = $exe
        $shortcut.WorkingDirectory = $install
        $shortcut.Description = 'DragonBreak - your dragon and break reminders'
        $shortcut.Save()
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
    }
} finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
Write-Output "Installed: $exe"
Write-Output 'Desktop and Start menu shortcuts created. Automatic startup is off.'
if ($Launch) { Start-Process -FilePath $exe }
