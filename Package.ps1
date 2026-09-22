param()
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1')
$stage = Join-Path $PSScriptRoot ('build\package-' + [Guid]::NewGuid().ToString('N'))
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $stage,$dist | Out-Null
try {
    # An explicit allowlist prevents local game copies, logs and backups entering the ZIP.
    foreach ($file in @('README.md','LICENSE','Start.cmd','Restore.cmd','Launcher.ps1','Launcher.Core.ps1','Build.ps1','Package.ps1','BongoAutoChest.ini')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage
    }
    foreach ($dir in @('src','tests','vendor')) { New-Item -ItemType Directory -Force (Join-Path $stage $dir) | Out-Null }
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Copy-Item -Destination (Join-Path $stage 'src')
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -File | Copy-Item -Destination (Join-Path $stage 'tests')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor\Mono.Cecil.dll'),(Join-Path $PSScriptRoot 'vendor\Mono.Cecil.LICENSE.txt') -Destination (Join-Path $stage 'vendor')
    $zip = Join-Path $dist 'bongo-cat-auto-chest-windows.zip'
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $hash = (Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'),"$hash  bongo-cat-auto-chest-windows.zip`n")
    Write-Host "Created $zip"
} finally {
    if ([IO.Path]::GetFullPath($stage).StartsWith([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')) + '\',[StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
