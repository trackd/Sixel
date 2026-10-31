#Requires -Version 7.0
param(
    [Parameter(Mandatory)]
    [version]$Version,
    [string]$Path = (Join-Path $PSScriptRoot '../module/Sixel.psd1')
)

$ErrorActionPreference = 'Stop'
$content = Get-Content -LiteralPath $Path -Raw
$pattern = [regex]::new("(?m)^(\s*ModuleVersion\s*=\s*)'[^']+'")
if ($pattern.Matches($content).Count -ne 1) {
    throw "Expected one ModuleVersion declaration in $Path."
}
$updated = $pattern.Replace($content, { param($match) "$($match.Groups[1].Value)'$Version'" })
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Path), $updated)
