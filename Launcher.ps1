param(
    [ValidateSet('Launch','Install','Check','Restore')][string]$Action = 'Launch',
    [string]$GameDirectory,
    [string]$SettingsPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Launcher.Core.ps1')
$lock = $null
$acquired = $false
try {
    $game = Find-Game $GameDirectory
    $settings = $null
    if ($SettingsPath) {
        if ($Action -notin @('Launch','Install')) { throw 'Settings are supported only for Launch or Install.' }
        $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
        Assert-Settings $settings
    }
    # Serialize this user's helpers even when launched from two different folders.
    $lock = New-Object Threading.Mutex($false,'Local\BongoAutoChestInstaller')
    try { $acquired = $lock.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (!$acquired) { throw 'Another Bongo Auto Chest helper is running.' }
    & (Join-Path $PSScriptRoot 'Build.ps1')
    Write-Host "Game: $game"
    Invoke-Installation $PSScriptRoot $game $Action
    if ($settings) { Save-Settings $game $settings }
    if ($Action -eq 'Launch' -and @(Get-Process BongoCat -ErrorAction SilentlyContinue).Count -eq 0) {
        $steam = Join-Path (Get-SteamRoot) 'steam.exe'
        Start-Process -FilePath $steam -ArgumentList '-applaunch','3419430' -WindowStyle Hidden
        Write-Host 'Launch requested through Steam.'
    }
} catch {
    Write-Host ("ERROR: " + $_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    if ($lock) { if ($acquired) { $lock.ReleaseMutex() }; $lock.Dispose() }
}
