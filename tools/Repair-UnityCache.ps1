# Run only after saving and closing this project's Unity Editor.
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'This script must remain inside the Unity project tools folder.'
}
$instanceFile = Join-Path $projectRoot 'Library/EditorInstance.json'
if (Test-Path -LiteralPath $instanceFile) {
    $instance = Get-Content -LiteralPath $instanceFile -Raw | ConvertFrom-Json
    $editorProcess = Get-Process -Id $instance.process_id -ErrorAction SilentlyContinue
    if ($editorProcess -and $editorProcess.ProcessName -eq 'Unity') {
        throw 'Save your scenes and close the W04 Unity Editor before rebuilding its cache.'
    }
}
$archive = [System.IO.Path]::GetFullPath((Join-Path $projectRoot ('obj/unity-cache-backup-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss'))))
$targets = @('Library', 'Temp')
# Validate every resolved path before moving any folder. Never follow links.
foreach ($relativePath in $targets) {
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $relativePath))
    if (-not $resolved.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Cache path escaped the project.'
    }
    if ((Test-Path -LiteralPath $resolved) -and ((Get-Item -LiteralPath $resolved).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing to move a linked cache folder.'
    }
}
if (-not $archive.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Archive path escaped the project.'
}
New-Item -ItemType Directory -Path $archive | Out-Null
foreach ($relativePath in $targets) {
    $source = Join-Path $projectRoot $relativePath
    if (Test-Path -LiteralPath $source) {
        Move-Item -LiteralPath $source -Destination (Join-Path $archive $relativePath)
    }
}
Write-Output "Cache preserved in: $archive"
Write-Output 'Reopen W04 in Unity. Assets will be reimported. Assets, .meta files, Packages and ProjectSettings were not changed.'
