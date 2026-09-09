param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found: $UnityPath" }
if (!(Test-Path -LiteralPath (Join-Path $projectDirectory 'user.keystore'))) { throw 'Missing user.keystore.' }
$previousPassword = $env:SOBOK_KEYSTORE_PASSWORD
try {
    if (!$env:SOBOK_KEYSTORE_PASSWORD) {
        $secret = Read-Host 'Android keystore password' -AsSecureString
        $env:SOBOK_KEYSTORE_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
    }
    $logDirectory = Join-Path $projectDirectory 'Logs'
    [System.IO.Directory]::CreateDirectory($logDirectory) | Out-Null
    $logPath = Join-Path $logDirectory ('android-release-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
    $arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $projectDirectory + '"'),
        '-buildTarget', 'Android', '-executeMethod', 'SobokAndroidBuild.BuildRelease', '-logFile', ('"' + $logPath + '"'))
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "Build log: $logPath"
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity build failed (exit $($process.ExitCode)). See $logPath" }
    Write-Output 'Signed AAB build completed in Builds/Android.'
} finally {
    $env:SOBOK_KEYSTORE_PASSWORD = $previousPassword
}
