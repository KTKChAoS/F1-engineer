$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectFile = Join-Path $projectRoot 'src\PitWall.Host\PitWall.Host.csproj'
$buildOutput = Join-Path $projectRoot 'src\PitWall.Host\bin\PublishBuild\'
$publishDir = Join-Path $projectRoot 'publish\win-x64'
$archivePath = Join-Path $projectRoot 'publish\PitWall-win-x64.zip'

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnetCommand) { $dotnetCommand.Source } else { $null }
$temporarySdk = Join-Path $env:LOCALAPPDATA 'Temp\pitwall-dotnet-sdk-10\dotnet.exe'
if (Test-Path -LiteralPath $temporarySdk) { $dotnetExe = $temporarySdk }
if (-not $dotnetExe) { throw 'Install the .NET 10 SDK, then run this script again.' }

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
& $dotnetExe publish $projectFile --configuration Release --runtime win-x64 --self-contained true --output $publishDir "-p:BaseOutputPath=$buildOutput"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archivePath -Force
Write-Host "Published app: $publishDir"
Write-Host "Portable bundle: $archivePath"
