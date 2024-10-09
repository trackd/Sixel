$importModule = Get-Command -Name Import-Module -Module Microsoft.PowerShell.Core
$moduleName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)

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
<#
@(
    foreach ($asm in [System.AppDomain]::CurrentDomain.GetAssemblies()) {
        if (-not ($asm.GetName().Name -like '*SixLabors*')) {
            continue
        }
        $alc = [Runtime.Loader.AssemblyLoadContext]::GetLoadContext($asm)
        [PSCustomObject]@{
            Name = $asm.FullName
            Location = $asm.Location
            ALC = $alc
        }
    }
) | Format-List
#>
