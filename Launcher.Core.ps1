Set-StrictMode -Version 2.0

function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Invoke-Checked([string]$Exe, [string[]]$Arguments) {
    $result = & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Tool failed: $([IO.Path]::GetFileName($Exe))" }
    $result | ForEach-Object { Write-Host $_ }
}
function Get-SteamRoot {
    $key = Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if ($key -and $key.SteamPath) { return $key.SteamPath.Replace('/','\') }
    $fallback = Join-Path ${env:ProgramFiles(x86)} 'Steam'
    if (Test-Path -LiteralPath (Join-Path $fallback 'steam.exe')) { return $fallback }
    throw 'Steam was not found. Install/sign in to Steam first, or use -GameDirectory with -Action Install.'
}
function Find-Game([string]$ExplicitPath) {
    if ($ExplicitPath) { $paths = @($ExplicitPath) }
    else {
        $steam = Get-SteamRoot
        $libraries = @($steam)
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            $libraries += [regex]::Matches([IO.File]::ReadAllText($vdf),'"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value.Replace('\\','\') }
        }
        $paths = foreach ($library in ($libraries | Select-Object -Unique)) {
            $manifest = Join-Path $library 'steamapps\appmanifest_3419430.acf'
            if (Test-Path -LiteralPath $manifest) {
                $match = [regex]::Match([IO.File]::ReadAllText($manifest),'"installdir"\s+"([^"]+)"')
                if ($match.Success) { Join-Path $library ('steamapps\common\' + $match.Groups[1].Value) }
            }
        }
    }
    foreach ($path in $paths) {
        if ((Test-Path -LiteralPath (Join-Path $path 'BongoCat.exe')) -and
            (Test-Path -LiteralPath (Join-Path $path 'BongoCat_Data\Managed\Assembly-CSharp.dll'))) {
            return (Resolve-Path -LiteralPath $path).Path
        }
    }
    throw 'Bongo Cat (Steam app 3419430, Windows Mono) was not found. Use -GameDirectory "D:\...\BongoCat".'
}
function Get-RecipeHash([string]$Root, [string]$Managed) {
    $files = @('AutoChest.cs','ReadyGate.cs','Patch.cs','ValidateReferences.cs') | ForEach-Object { Get-Item -LiteralPath (Join-Path $Root "src\$_") }
    $files += Get-Item -LiteralPath (Join-Path $Root 'Launcher.Core.ps1'), (Join-Path $Root 'Build.ps1'), (Join-Path $Root 'vendor\Mono.Cecil.dll')
    $files += Get-ChildItem -LiteralPath $Managed -Filter '*.dll' -File | Where-Object { $_.Name -notin @('Assembly-CSharp.dll','BongoAutoChest.dll') }
    $text = ($files | Sort-Object FullName | ForEach-Object { $_.Name + ':' + (Get-Sha $_.FullName) }) -join "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-','') }
    finally { $sha.Dispose() }
}
function Assert-Settings($Settings) {
    foreach ($key in @('Enabled','AutoOwn','AutoOthers')) {
        if ($Settings.$key -isnot [bool]) { throw "Invalid setting: $key" }
    }
    foreach ($key in @('MinDelaySeconds','MaxDelaySeconds')) {
        $value = $Settings.$key
        if ($value -isnot [ValueType] -or $value -is [bool] -or [double]::IsNaN([double]$value) -or
            [double]::IsInfinity([double]$value) -or $value -lt 1 -or $value -gt 300) { throw "Invalid setting: $key" }
    }
    if ($Settings.MinDelaySeconds -gt $Settings.MaxDelaySeconds) { throw 'Minimum delay must not exceed maximum delay.' }
    if ($Settings.Enabled -and !$Settings.AutoOwn -and !$Settings.AutoOthers) { throw 'Choose at least one chest target, or pause automatic opening.' }
}
function Save-Settings([string]$Game, $Settings) {
    Assert-Settings $Settings
    $path = Join-Path $Game 'BongoAutoChest.ini'
    # Preserve comments and future/unknown keys while removing duplicate known keys.
    $values = [ordered]@{}
    foreach ($key in @('Enabled','AutoOwn','AutoOthers')) { $values[$key] = $Settings.$key.ToString().ToLowerInvariant() }
    foreach ($key in @('MinDelaySeconds','MaxDelaySeconds')) { $values[$key] = ([double]$Settings.$key).ToString([Globalization.CultureInfo]::InvariantCulture) }
    $lines = @()
    if (Test-Path -LiteralPath $path) {
        $lines = @(Get-Content -LiteralPath $path | Where-Object {
            $parts = $_ -split '=',2
            $parts.Count -ne 2 -or !$values.Contains($parts[0].Trim())
        })
    }
    foreach ($key in $values.Keys) { $lines += "$key=$($values[$key])" }
    $temp = Join-Path $Game ('settings-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllLines($temp,$lines,[Text.UTF8Encoding]::new($false))
        Replace-File $temp $path
    } finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
    Write-Host 'Settings saved.'
}
function Stop-GameForChange {
    foreach ($p in @(Get-Process BongoCat -ErrorAction SilentlyContinue)) {
        Write-Host 'Waiting for Bongo Cat to close normally...'
        if (!$p.CloseMainWindow() -or !$p.WaitForExit(15000)) { throw 'Close Bongo Cat from its menu/system tray, then run this helper again.' }
    }
}
function Assert-Stopped {
    if (@(Get-Process BongoCat -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Bongo Cat is running. Close it and retry.' }
}
function Assert-Current([string]$Target, [string]$ExpectedHash) {
    if ((Get-Sha $Target) -ne $ExpectedHash) { throw 'The game DLL changed during preparation (possibly a Steam update). Wait for Steam to finish and retry.' }
}
function Replace-File([string]$Source, [string]$Destination) {
    # Stage in the destination directory so rename/replace stays on one volume.
    $staged = $Destination + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::Copy($Source,$staged)
        if ([IO.File]::Exists($Destination)) { [IO.File]::Replace($staged,$Destination,[System.Management.Automation.Language.NullString]::Value) }
        else { [IO.File]::Move($staged,$Destination) }
    } finally { if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged -Force } }
}
function Read-InstallState([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return $null }
    $state = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($key in @('originalHash','patchedHash','helperHash','recipeHash')) {
        if ($state.$key -notmatch '^[0-9A-Fa-f]{64}$') { throw "Invalid install state ($key)." }
    }
    return $state
}
function Get-Original([string]$Target, [string]$StateRoot, $State, [string]$PatchTool) {
    $kind = & $PatchTool inspect $Target
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the installed game DLL.' }
    if ($kind -eq 'clean') { return $Target }
    $candidates = @()
    if ($State -and (Get-Sha $Target) -eq $State.patchedHash) {
        $backup = Join-Path $StateRoot ($State.originalHash + '.original.dll')
        if (!(Test-Path -LiteralPath $backup) -or (Get-Sha $backup) -ne $State.originalHash) { throw 'The original backup is missing or corrupt.' }
        $candidates += $backup
    }
    # Migrate the earlier local installer only if its backup exactly matches all original method bodies.
    $legacy = Join-Path ([IO.Path]::GetDirectoryName($Target)) 'Assembly-CSharp.pre-autochest.dll.bak'
    if (Test-Path -LiteralPath $legacy) { $candidates += $legacy }
    foreach ($candidate in $candidates) {
        & $PatchTool verify $candidate $Target | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -eq 0) { return $candidate }
    }
    throw 'Patched game DLL has no matching original backup. Use Steam Verify integrity, then run the helper again.'
}
function Invoke-Installation([string]$Root, [string]$Game, [string]$Action) {
    $managed = Join-Path $Game 'BongoCat_Data\Managed'
    $target = Join-Path $managed 'Assembly-CSharp.dll'
    $helper = Join-Path $managed 'BongoAutoChest.dll'
    $stateRoot = Join-Path $Game '.bongo-auto-chest'
    $statePath = Join-Path $stateRoot 'install.json'
    $patch = Join-Path $Root 'build\tools\Patch.exe'
    $currentHash = Get-Sha $target
    $state = Read-InstallState $statePath
    if ($Action -eq 'Restore') {
        $kind = & $patch inspect $target
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the game.' }
        if ($kind -eq 'clean') { Write-Host 'Already unpatched; no old backup was applied.'; return }
        $original = Get-Original $target $stateRoot $state $patch
        Stop-GameForChange
        Assert-Stopped
        Assert-Current $target $currentHash
        Replace-File $original $target
        if ((Get-Sha $target) -ne (Get-Sha $original)) { throw 'Restore verification failed.' }
        Write-Host 'Original game DLL restored. Settings and backups were kept.'
        return
    }
    $recipe = Get-RecipeHash $Root $managed
    if ($state -and $currentHash -eq $state.patchedHash -and $recipe -eq $state.recipeHash -and
        (Test-Path -LiteralPath $helper) -and (Get-Sha $helper) -eq $state.helperHash) {
        Write-Host 'Already current; no files changed.'
        return
    }
    $original = Get-Original $target $stateRoot $state $patch
    $originalHash = Get-Sha $original
    $temp = Join-Path $Root ('build\prepare-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $temp | Out-Null
    try {
        $sourceCopy = Join-Path $temp 'Assembly-CSharp.dll'
        Copy-Item -LiteralPath $original -Destination $sourceCopy
        $newHelper = Join-Path $temp 'BongoAutoChest.dll'
        $newGame = Join-Path $temp 'Assembly-CSharp.patched.dll'
        $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
        $compilerArgs = @('/nologo','/langversion:5','/target:library','/nowarn:1684',"/out:$newHelper","/reference:$sourceCopy")
        $compilerArgs += @('UnityEngine.CoreModule.dll','Heathen.Steamworks.dll','com.rlabrecque.steamworks.net.dll') | ForEach-Object { '/reference:' + (Join-Path $managed $_) }
        $compilerArgs += (Join-Path $Root 'src\AutoChest.cs'), (Join-Path $Root 'src\ReadyGate.cs')
        Invoke-Checked $csc $compilerArgs
        Invoke-Checked (Join-Path $Root 'build\tools\ValidateReferences.exe') @($newHelper,$managed)
        Invoke-Checked $patch @($sourceCopy,$newHelper,$managed,$newGame)
        Assert-Current $target $currentHash
        if ($recipe -ne (Get-RecipeHash $Root $managed)) { throw 'Dependencies changed during preparation. Retry after updates finish.' }
        if ($Action -eq 'Check') { Write-Host 'Compatibility check passed. Game files were not changed.'; return }
        Stop-GameForChange
        Assert-Stopped
        Assert-Current $target $currentHash
        if ($recipe -ne (Get-RecipeHash $Root $managed)) { throw 'Dependencies changed while closing the game. Retry after updates finish.' }
        New-Item -ItemType Directory -Force $stateRoot | Out-Null
        $backup = Join-Path $stateRoot ($originalHash + '.original.dll')
        if (Test-Path -LiteralPath $backup) {
            if ((Get-Sha $backup) -ne $originalHash) { throw 'Existing backup hash mismatch.' }
        } else { [IO.File]::Copy($sourceCopy,$backup) }
        if ((Get-Sha $backup) -ne $originalHash) { throw 'Backup verification failed.' }
        $oldGame = Join-Path $temp 'rollback-game.dll'
        $oldHelper = Join-Path $temp 'rollback-helper.dll'
        Copy-Item -LiteralPath $target -Destination $oldGame
        $hadHelper = Test-Path -LiteralPath $helper
        if ($hadHelper) { Copy-Item -LiteralPath $helper -Destination $oldHelper }
        try {
            Replace-File $newHelper $helper
            Replace-File $newGame $target
            $nextState = [ordered]@{ version = 1; originalHash = $originalHash; patchedHash = (Get-Sha $newGame); helperHash = (Get-Sha $newHelper); recipeHash = $recipe }
            if ((Get-Sha $target) -ne $nextState.patchedHash -or (Get-Sha $helper) -ne $nextState.helperHash) { throw 'Installed file verification failed.' }
            $config = Join-Path $Game 'BongoAutoChest.ini'
            if (!(Test-Path -LiteralPath $config)) { Copy-Item -LiteralPath (Join-Path $Root 'BongoAutoChest.ini') -Destination $config }
            $newState = Join-Path $temp 'install.json'
            $nextState | ConvertTo-Json | Set-Content -LiteralPath $newState -Encoding UTF8
            Replace-File $newState $statePath
        } catch {
            # The game is stopped; put both executable files back if installation fails.
            Replace-File $oldGame $target
            if ($hadHelper) { Replace-File $oldHelper $helper }
            else { Remove-Item -LiteralPath $helper -Force -ErrorAction SilentlyContinue }
            throw
        }
        Write-Host 'Installed for this game version. Existing settings were preserved.'
    } finally {
        if ([IO.Path]::GetFullPath($temp).StartsWith([IO.Path]::GetFullPath((Join-Path $Root 'build')) + '\',[StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $temp -Recurse -Force
        }
    }
}
