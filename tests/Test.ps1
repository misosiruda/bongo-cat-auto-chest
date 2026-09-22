param([string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'Launcher.Core.ps1')
& (Join-Path $root 'Build.ps1')
$bin = Join-Path $root 'build\tools'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Invoke-Checked $csc @('/nologo','/langversion:5',"/out:$bin\ReadyGateTests.exe",(Join-Path $root 'src\ReadyGate.cs'),(Join-Path $PSScriptRoot 'ReadyGateTests.cs'))
Invoke-Checked (Join-Path $bin 'ReadyGateTests.exe') @()
foreach ($script in @(Get-ChildItem -LiteralPath $root -Filter '*.ps1') + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1')) {
    $tokens = $null; $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$errors)
    if ($errors.Count) { throw "Invalid PowerShell: $($script.Name): $errors" }
}
Write-Host 'PASS PowerShell syntax'
if (!$GameDirectory) { Write-Host 'Integration checks skipped: supply -GameDirectory for a local installed game (never distributed).'; exit 0 }
$installed = Find-Game $GameDirectory
$installedManaged = Join-Path $installed 'BongoCat_Data\Managed'
$patch = Join-Path $bin 'Patch.exe'
$installedStateRoot = Join-Path $installed '.bongo-auto-chest'
$installedState = Read-InstallState (Join-Path $installedStateRoot 'install.json')
$original = Get-Original (Join-Path $installedManaged 'Assembly-CSharp.dll') $installedStateRoot $installedState $patch
$fixture = Join-Path $root ('build\fixture-' + [Guid]::NewGuid().ToString('N'))
$managed = Join-Path $fixture 'BongoCat_Data\Managed'
New-Item -ItemType Directory -Force $managed | Out-Null
Get-ChildItem -LiteralPath $installedManaged -Filter '*.dll' -File | Copy-Item -Destination $managed
$target = Join-Path $managed 'Assembly-CSharp.dll'
Copy-Item -LiteralPath $original -Destination $target -Force
$originalHash = Get-Sha $target
$stateRoot = Join-Path $fixture '.bongo-auto-chest'
$statePath = Join-Path $stateRoot 'install.json'
# These tests touch only disposable copies. Never stop the user's actual game.
function Stop-GameForChange {}
function Assert-Stopped {}
function Check([bool]$condition,[string]$name) { if (!$condition) { throw "FAIL $name" }; Write-Host "PASS $name" }
function Expect-Failure([scriptblock]$block,[string]$name) {
    $failed = $false
    try { & $block } catch { $failed = $true; Write-Host "Expected rejection: $($_.Exception.Message)" }
    Check $failed $name
}
try {
    Invoke-Checked $csc @('/nologo','/langversion:5',"/reference:$bin\Mono.Cecil.dll","/out:$bin\MutateFixture.exe",(Join-Path $PSScriptRoot 'MutateFixture.cs'))
    Invoke-Installation $root $fixture 'Check'
    Check ((Get-Sha $target) -eq $originalHash -and !(Test-Path $stateRoot)) 'Check leaves game untouched'
    Invoke-Installation $root $fixture 'Install'
    $first = Read-InstallState $statePath
    Check ((Get-Sha (Join-Path $stateRoot ($first.originalHash + '.original.dll'))) -eq $originalHash) 'exact original backup'
    $config = Join-Path $fixture 'BongoAutoChest.ini'
    [IO.File]::WriteAllText($config,"Enabled=false`nMinDelaySeconds=4`nMaxDelaySeconds=7")
    $configHash = Get-Sha $config
    Invoke-Installation $root $fixture 'Install'
    Check ((Get-Sha $target) -eq $first.patchedHash) 'repeat install is idempotent'
    Invoke-Installation $root $fixture 'Restore'
    Check ((Get-Sha $target) -eq $originalHash) 'restore is byte exact'
    $v2 = Join-Path $fixture 'version2.dll'
    Invoke-Checked (Join-Path $bin 'MutateFixture.exe') @($target,$v2,'version')
    Copy-Item -LiteralPath $v2 -Destination $target -Force
    $v2Hash = Get-Sha $target
    Invoke-Installation $root $fixture 'Restore'
    Check ((Get-Sha $target) -eq $v2Hash) 'restore never overwrites clean Steam update with old backup'
    Invoke-Installation $root $fixture 'Install'
    $second = Read-InstallState $statePath
    Check ($second.originalHash -eq $v2Hash -and $second.originalHash -ne $first.originalHash) 'updated game gets its own backup and patch'
    Check ((Get-Sha $config) -eq $configHash) 'custom settings preserved across update'
    $tampered = Join-Path $fixture 'tampered.dll'
    Invoke-Checked (Join-Path $bin 'MutateFixture.exe') @($target,$tampered,'tamper')
    Expect-Failure { Invoke-Checked $patch @('verify',$v2,$tampered) } 'unexpected method changes rejected'
    $backup2 = Join-Path $stateRoot ($second.originalHash + '.original.dll')
    Copy-Item -LiteralPath $original -Destination $backup2 -Force
    Expect-Failure { Invoke-Installation $root $fixture 'Restore' } 'corrupt backup refused'
    Check ((Get-Sha $target) -eq $second.patchedHash) 'failed restore leaves game unchanged'
    Copy-Item -LiteralPath $v2 -Destination $backup2 -Force
    Invoke-Installation $root $fixture 'Restore'
    Check ((Get-Sha $target) -eq $v2Hash) 'updated version restores correctly'
    $broken = Join-Path $fixture 'broken.dll'
    Invoke-Checked (Join-Path $bin 'MutateFixture.exe') @($target,$broken,'break-field')
    Copy-Item -LiteralPath $broken -Destination $target -Force
    $brokenHash = Get-Sha $target
    Expect-Failure { Invoke-Installation $root $fixture 'Install' } 'incompatible reflection field rejected before install'
    Check ((Get-Sha $target) -eq $brokenHash) 'incompatible update left untouched'
    Write-Host 'All integration checks passed using disposable local copies.'
} finally {
    if ([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetFullPath((Join-Path $root 'build')) + '\',[StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}
