using namespace System.IO
using namespace System.Management.Automation
using namespace System.Reflection

$importModule = Get-Command -Name Import-Module -Module Microsoft.PowerShell.Core
$moduleName = [Path]::GetFileNameWithoutExtension($PSCommandPath)
$isReload = $true

if ($IsCoreClr) {
    if (-not ('Sixel.Shared.LoadContext' -as [type])) {
        $isReload = $false
        Add-Type -Path ([Path]::Combine($PSScriptRoot, 'bin', 'net8.0', "$moduleName.Shared.dll"))
    }

    $mainModule = [Sixel.Shared.LoadContext]::Initialize()
    $innerMod = &$importModule -Assembly $mainModule -PassThru
}
else {
    # PowerShell 5.1 has no concept of an Assembly Load Context so it will
    # just load the module assembly directly.

    # The type can be any type within our ALCLoader project
    if (-not ('Sixel.Shared.AssemblyResolver' -as [type])) {
        Add-Type -Path ([Path]::Combine($PSScriptRoot, 'bin', 'net472', "$moduleName.Shared.dll"))
    }

    $appDomain = [AppDomain]::CurrentDomain
    $resolver = [Sixel.Shared.AssemblyResolver]::ResolveHandler
    $appDomain.add_AssemblyResolve($resolver)
    $innerMod = if ('Sixel.Terminal.SizeHelper' -as [type]) {
        $modAssembly = [Sixel.Terminal.SizeHelper].Assembly
        &$importModule -Assembly $modAssembly -Force -PassThru
    }
    else {
        $isReload = $false
        $modPath = [Path]::Combine($PSScriptRoot, 'bin', 'net472', "$moduleName.dll")
        &$importModule -Name $modPath -ErrorAction Stop -PassThru
    }
}

if ($isReload) {
    # Bug in pwsh, Import-Module in an assembly will pick up a cached instance
    # and not call the same path to set the nested module's cmdlets to the
    # current module scope. This is only technically needed if someone is
    # calling 'Import-Module -Name $module -Force' a second time. The first
    # import is still fine.
    # https://github.com/PowerShell/PowerShell/issues/20710
    $addExportedCmdlet = [PSModuleInfo].GetMethod(
        'AddExportedCmdlet',
        [BindingFlags]'Instance, NonPublic'
    )
    foreach ($cmd in $innerMod.ExportedCmdlets.Values) {
        $addExportedCmdlet.Invoke($ExecutionContext.SessionState.Module, @(, $cmd))
    }
}

$OnRemove = {
    $appDomain.remove_AssemblyResolve($resolver)
}
$registerEngineEventSplat = @{
    SourceIdentifier = ([System.Management.Automation.PsEngineEvent]::Exiting)
    Action           = $OnRemove
}
Register-EngineEvent @registerEngineEventSplat
