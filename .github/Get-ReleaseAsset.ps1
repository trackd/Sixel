<#
.SYNOPSIS
Validate and return the sixel module package for a release.
.DESCRIPTION
Requires exactly one downloaded asset with the expected module name and version.
Rejects missing, duplicate, unexpected and incorrectly versioned package files.
.PARAMETER Version
Module version selected for this release.
.PARAMETER Path
Directory containing downloaded workflow artifacts.
.EXAMPLE
./.github/Get-ReleaseAsset.ps1 -Version 0.13.0 -Path ./artifacts
.EXAMPLE
./.github/Get-ReleaseAsset.ps1 -Version 1.0.0 -Path ./artifacts/release
.OUTPUTS
System.IO.FileInfo. The validated module package.
.NOTES
Does not modify files or publish packages.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [version]$Version,
    [Parameter(Mandatory)]
    [string]$Path
)
$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath $Path -File -Recurse)
$expected = "Sixel.$Version.nupkg"
$matching = @($files | Where-Object Name -CEQ $expected)
if ($matching.Count -ne 1) {
    throw "Expected exactly one release asset named $expected; found $($matching.Count)."
}
if ($files.Count -ne 1) {
    throw 'Release artifacts contain unexpected files or mismatched versions.'
}
$matching[0]
