$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'publish\win-x64'
$installDir = Join-Path $projectRoot 'app'
$hostExe = Join-Path $sourceDir 'PitWall.Host.exe'
if (-not (Test-Path -LiteralPath $hostExe)) {
    throw 'Build the win-x64 bundle first by running Build-WinX64.ps1.'
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path (Join-Path $sourceDir '*') -Destination $installDir -Recurse -Force

$shortcutPath = Join-Path $installDir 'PitWall.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir 'PitWall.Host.exe'
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = 'PitWall F1 25 Race Engineer'
$shortcut.Save()

Write-Host "Installed PitWall under $installDir"
