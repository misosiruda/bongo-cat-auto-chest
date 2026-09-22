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
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\Wizard.manifest') -Destination (Join-Path $stage 'src')
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -File | Copy-Item -Destination (Join-Path $stage 'tests')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor\Mono.Cecil.dll'),(Join-Path $PSScriptRoot 'vendor\Mono.Cecil.LICENSE.txt') -Destination (Join-Path $stage 'vendor')
    $zip = Join-Path $dist 'bongo-cat-auto-chest-windows.zip'
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $exe = Join-Path $dist 'BongoAutoChest.exe'
    $compilerArgs = @('/nologo','/langversion:5','/codepage:65001','/target:winexe','/platform:x64',"/out:$exe",
        "/resource:$zip,Payload.zip",('/win32manifest:' + (Join-Path $PSScriptRoot 'src\Wizard.manifest')),
        '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll',
        ('/reference:' + (Join-Path $framework 'System.IO.Compression.dll')),('/reference:' + (Join-Path $framework 'System.IO.Compression.FileSystem.dll')),
        (Join-Path $PSScriptRoot 'src\WizardModel.cs'),(Join-Path $PSScriptRoot 'src\Wizard.cs'),(Join-Path $PSScriptRoot 'src\WizardAssembly.cs'))
    & (Join-Path $framework 'csc.exe') @compilerArgs
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the Windows wizard.' }
    # The public ZIP is also a GUI distribution. Backend scripts live only inside the EXE payload.
    Compress-Archive -LiteralPath $exe,(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $PSScriptRoot 'vendor\Mono.Cecil.LICENSE.txt') -DestinationPath $zip -Force
    $hash = (Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant()
    $exeHash = (Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'),"$exeHash  BongoAutoChest.exe`n$hash  bongo-cat-auto-chest-windows.zip`n")
    Write-Host "Created $exe and $zip"
} finally {
    if ([IO.Path]::GetFullPath($stage).StartsWith([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')) + '\',[StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
