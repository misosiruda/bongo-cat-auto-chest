param(
    [ValidateSet('Launch','Install','Check','Restore')][string]$Action = 'Launch',
    [string]$GameDirectory
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Launcher.Core.ps1')
$lock = $null
$acquired = $false
try {
    $game = Find-Game $GameDirectory
    # Serialize this user's helpers even when launched from two different folders.
    $lock = New-Object Threading.Mutex($false,'Local\BongoAutoChestInstaller')
    try { $acquired = $lock.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (!$acquired) { throw 'Another Bongo Auto Chest helper is running.' }
    & (Join-Path $PSScriptRoot 'Build.ps1')
    Write-Host "Game: $game"
    Invoke-Installation $PSScriptRoot $game $Action
    if ($Action -eq 'Launch' -and @(Get-Process BongoCat -ErrorAction SilentlyContinue).Count -eq 0) {
        $steam = Join-Path (Get-SteamRoot) 'steam.exe'
        Start-Process -FilePath $steam -ArgumentList '-applaunch','3419430' -WindowStyle Hidden
        Write-Host 'Launch requested through Steam.'
    }
} catch {
    Write-Host ("ERROR: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'If access is denied, use a writable Steam library or run this helper as administrator.'
    exit 1
} finally {
    if ($lock) { if ($acquired) { $lock.ReleaseMutex() }; $lock.Dispose() }
}
