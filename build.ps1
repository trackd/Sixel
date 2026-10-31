#! /usr/bin/pwsh
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipHelp,
    [switch] $SkipTests,
    [string] $Task
)

$ErrorActionPreference = 'Stop'
# Helper function to get paths
$buildparams = @{
    Configuration = $Configuration
    SkipHelp      = $SkipHelp.IsPresent
    SkipTests     = $SkipTests.IsPresent
    File          = (Get-Item (Join-Path $PSScriptRoot '*.build.ps1')).FullName
    Task          = 'All'
    Result        = 'Result'
}
if (-not (Get-Module -ListAvailable -Name InvokeBuild)) {
    Install-Module -Name InvokeBuild -Scope CurrentUser -Force -AllowClobber
}
Import-Module InvokeBuild -ErrorAction Stop

if ($Task) {
    $buildparams.Task = $Task
}

# Keep binary module imports in a short-lived build process and propagate failures to CI.
$worker = {
    param($parameters)
    $ErrorActionPreference = 'Stop'
    Invoke-Build @parameters
}
$executable = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
& $executable -NoProfile -NonInteractive -Command $worker -args $buildparams
exit $LASTEXITCODE
