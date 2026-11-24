#! /usr/bin/pwsh
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipHelp,
    [switch] $SkipTests,
    [string] $Task
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion -lt [version]'7.4') { throw 'The build requires PowerShell 7.4 or newer.' }

# Pinned build dependencies, installed once into .tools/Modules from the pwsh.gallery mirror.
$requiredModules = @{
    InvokeBuild = '5.14.23'
    'Microsoft.PowerShell.PlatyPS' = '1.0.1'
    Pester = '6.2.0'
    'Microsoft.PowerShell.PSResourceGet' = '1.2.0'
}
$modulePath = Join-Path $PSScriptRoot '.tools' 'Modules'
$env:PSModulePath = $modulePath + [IO.Path]::PathSeparator + $env:PSModulePath
$missingModules = @(
    foreach ($module in $requiredModules.GetEnumerator()) {
        if (-not (Get-Module -ListAvailable -FullyQualifiedName @{
            ModuleName = $module.Key
            RequiredVersion = $module.Value
        })) {
            '{0}={1}' -f $module.Key, $module.Value
        }
    }
)
if ($missingModules.Count -gt 0) {
    if (Get-Module -ListAvailable -FullyQualifiedName @{ ModuleName = 'ModuleFast'; RequiredVersion = '0.6.1' }) {
        Import-Module ModuleFast -RequiredVersion 0.6.1 -ErrorAction Stop
    }
    else {
        $bootstrapPath = Join-Path $PSScriptRoot '.tools' 'ModuleFast.ps1'
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $bootstrapPath) -Force
        Invoke-WebRequest -Uri 'https://github.com/JustinGrote/ModuleFast/releases/download/v0.6.1/ModuleFast.ps1' -OutFile $bootstrapPath
        & $bootstrapPath -Release 'v0.6.1'
    }
    $installParameters = @{
        Specification = $missingModules
        Destination = $modulePath
        Source = 'https://pwsh.gallery/index.json'
        NoPSModulePathUpdate = $true
        NoProfileUpdate = $true
        ThrottleLimit = 2
        ErrorAction = 'Stop'
    }
    Install-ModuleFast @installParameters
}
$moduleImports = @(
    foreach ($module in $requiredModules.GetEnumerator()) {
        $installed = Get-Module -ListAvailable -FullyQualifiedName @{
            ModuleName = $module.Key
            RequiredVersion = $module.Value
        } | Select-Object -First 1
        if (-not $installed) {
            throw "Build dependency $($module.Key) $($module.Value) is not available after bootstrap."
        }
        $installed.Path
    }
)

$buildParameters = @{
    Configuration = $Configuration
    SkipHelp      = $SkipHelp.IsPresent
    SkipTests     = $SkipTests.IsPresent
    File          = Join-Path $PSScriptRoot 'Sixel.build.ps1'
    Task          = 'All'
    Result        = 'buildResult'
    Safe          = $true
}
if ($Task) {
    $buildParameters.Task = $Task
}

# Keep binary module imports in a short-lived build process and propagate failures to CI.
$worker = {
    param($parameters, $moduleImports)
    $ErrorActionPreference = 'Stop'
    foreach ($module in $moduleImports) {
        Import-Module -Name $module -ErrorAction Stop
    }
    Invoke-Build @parameters
    if ($buildResult.Error) { exit 1 }
}
$executable = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
& $executable -NoProfile -NonInteractive -Command $worker -args $buildParameters, $moduleImports
exit $LASTEXITCODE
