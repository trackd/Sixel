$importModule = Get-Command -Name Import-Module -Module Microsoft.PowerShell.Core
$moduleName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)

if (-not $IsCoreClr) {
    # PowerShell 5.1 has no concept of an Assembly Load Context so it will
    # just load the module assembly directly.

    # The type can be any type within our ALCLoader project
    $innerMod = if ('Sixel.Terminal.SizeHelper' -as [type]) {
        $modAssembly = [Sixel.Terminal.SizeHelper].Assembly
        & $importModule -Assembly $modAssembly -Force -PassThru
    }
    else {
        $modPath = [System.IO.Path]::Combine($PSScriptRoot, 'bin', 'net472', "$moduleName.dll")
        & $importModule -Name $modPath -ErrorAction Stop -PassThru
    }
}
else {
    # This is used to load the shared assembly in the Default ALC which then sets
    # an ALC for the module and any dependencies of that module to be loaded in
    # that ALC.

    $isReload = $true
    if (-not ('Sixel.Shared.LoadContext' -as [type])) {
        $isReload = $false

        Add-Type -Path ([System.IO.Path]::Combine($PSScriptRoot, 'bin', 'net8.0', "$moduleName.Shared.dll"))
    }

    $mainModule = [Sixel.Shared.LoadContext]::Initialize()
    $innerMod = & $importModule -Assembly $mainModule -PassThru:$isReload
}

if ($innerMod) {
    # Bug in pwsh, Import-Module in an assembly will pick up a cached instance
    # and not call the same path to set the nested module's cmdlets to the
    # current module scope. This is only technically needed if someone is
    # calling 'Import-Module -Name ALCLoader -Force' a second time. The first
    # import is still fine.
    # https://github.com/PowerShell/PowerShell/issues/20710
    $addExportedCmdlet = [System.Management.Automation.PSModuleInfo].GetMethod(
        'AddExportedCmdlet',
        [System.Reflection.BindingFlags]'Instance, NonPublic'
    )
    foreach ($cmd in $innerMod.ExportedCmdlets.Values) {
        $addExportedCmdlet.Invoke($ExecutionContext.SessionState.Module, @(, $cmd))
    }
}
