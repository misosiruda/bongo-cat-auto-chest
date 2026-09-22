param()
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Windows .NET Framework 4.x is required.' }
$vendor = Join-Path $PSScriptRoot 'vendor'
$cecil = Join-Path $vendor 'Mono.Cecil.dll'
$expected = 'C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B'
if (!(Test-Path -LiteralPath $cecil)) {
    $temp = Join-Path $PSScriptRoot ('build\download-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $temp | Out-Null
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $zip = Join-Path $temp 'cecil.zip'
        Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/mono.cecil/0.11.6/mono.cecil.0.11.6.nupkg' -OutFile $zip -UseBasicParsing
        if ((Get-FileHash $zip).Hash -ne 'D2A23832AAA948BA9A01ACC42B5726E34C5F995958F1B30D45C0E7C70B3A72D5') { throw 'NuGet package hash mismatch.' }
        Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $temp 'package')
        Copy-Item -LiteralPath (Join-Path $temp 'package\lib\net40\Mono.Cecil.dll') -Destination $cecil
    } finally {
        if ([IO.Path]::GetFullPath($temp).StartsWith([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')) + '\',[StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $temp -Recurse -Force
        }
    }
}
if ((Get-FileHash -LiteralPath $cecil).Hash -ne $expected) { throw 'Mono.Cecil.dll hash mismatch.' }
$bin = Join-Path $PSScriptRoot 'build\tools'
New-Item -ItemType Directory -Force $bin | Out-Null
Copy-Item -LiteralPath $cecil -Destination $bin -Force
foreach ($tool in @('Patch','ValidateReferences')) {
    & $csc /nologo /langversion:5 /target:exe "/reference:$cecil" "/out:$(Join-Path $bin "$tool.exe")" (Join-Path $PSScriptRoot "src\$tool.cs")
    if ($LASTEXITCODE -ne 0) { throw "Could not compile $tool." }
}
Write-Host 'Build tools ready.'
