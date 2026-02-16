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
    Safe          = $true
}
if (-not (Get-Module -ListAvailable -Name InvokeBuild)) {
    Install-Module -Name InvokeBuild -Scope CurrentUser -Force -AllowClobber
}
Import-Module InvokeBuild -ErrorAction Stop

if ($task) {
    $buildparams.Task = $task
}

if (-not $env:CI) {
    # this is just so the dll doesn't get locked on and i can rebuild without restarting terminal
    $sb = {
        param($bp)
        Invoke-Build @bp
    }
    pwsh -NoProfile -Command $sb -args $buildparams
}
else {
    Invoke-Build @buildparams
}
