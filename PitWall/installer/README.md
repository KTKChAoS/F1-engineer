# Windows bundle

Run `Build-WinX64.ps1` to publish a self-contained win-x64 build and create `publish/PitWall-win-x64.zip`. The build needs the .NET 10 SDK and NuGet restore access.

Run `Install-PitWall.ps1` to copy the published app to `PitWall/app` and create a shortcut there. The app stays inside the workspace, and its recordings, logs, and local settings remain in the workspace root.

Close a running PitWall Host before rebuilding or installing an update.
