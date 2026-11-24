# Child of the isolated ConPTY diagnostic, never run in the user's editing session.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResultPath,
    [switch]$ReplaceConsoleInput
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '../../..'))
Set-Location -LiteralPath $projectRoot
$moduleRoot = [System.IO.Path]::Combine($PSScriptRoot, 'obj', 'modules')
$moduleDirectory = [System.IO.Path]::Combine($moduleRoot, 'Sixel')
[System.IO.Directory]::CreateDirectory($moduleDirectory) > $null
Copy-Item -Path ([System.IO.Path]::Combine($projectRoot, 'output', '*')) -Destination $moduleDirectory -Recurse -Force
$env:PSModulePath = $moduleRoot + [System.IO.Path]::PathSeparator + $env:PSModulePath

# Bind the child to its own console even when the diagnostic parent has redirected handles.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ProbeConsole {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll")]
    public static extern bool SetStdHandle(int id, IntPtr handle);
}
'@
$consoleInput = [ProbeConsole]::CreateFileW('CONIN$', 3221225472, 3, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
$consoleOutput = [ProbeConsole]::CreateFileW('CONOUT$', 3221225472, 3, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
[ProbeConsole]::SetStdHandle(-10, $consoleInput) > $null
[ProbeConsole]::SetStdHandle(-11, $consoleOutput) > $null
Import-Module PSReadLine
Set-PSReadLineOption -HistorySavePath ([System.IO.Path]::Combine($PSScriptRoot, 'obj', 'isolated-history.txt')) -HistorySaveStyle SaveNothing -PredictionSource None

if (Get-Module Sixel) { throw 'Sixel must not be imported before the completion test.' }
$global:probeResultPath = $ResultPath
$global:probeOriginalReader = $function:global:PSConsoleHostReadLine
$global:probeResult = [ordered]@{
    PowerShell = $PSVersionTable.PSVersion.ToString()
    PSReadLine = (Get-Module PSReadLine).Version.ToString()
}
$global:probeReadCount = 0
$global:probeReplaceInput = $ReplaceConsoleInput.IsPresent
$global:probeSavedInput = $null
function global:prompt {
    if ($global:probeReadCount -eq 0) { [Console]::WriteLine('EDIT-BEGIN') }
    else { [Console]::WriteLine('EDIT-AGAIN') }
    'probe> '
}
function global:PSConsoleHostReadLine {
    try {
        $line = & $global:probeOriginalReader
        if ($global:probeReadCount -eq 0) {
            $global:probeResult.ImportedByCompletion = $null -ne (Get-Module Sixel)
            $global:probeResult.ProbeBeforeExecution = [Sixel.Terminal.Compatibility]::RuntimeProbeAttempted
            $global:probeResult.CompletedLine = $line
            $global:probeReadCount++
            if ($global:probeReplaceInput) {
                $global:probeSavedInput = [Console]::In
                [Console]::SetIn([System.IO.StringReader]::new('replacement-input'))
            }
            [Console]::WriteLine('EXEC-BEGIN')
            return $line
        }
        $global:probeResult.ProbeAfterExecution = [Sixel.Terminal.Compatibility]::RuntimeProbeAttempted
        $global:probeResult.NextLine = $line
        if ($global:probeReplaceInput) {
            $global:probeResult.ReplacementInputRemaining = [Console]::In.ReadToEnd()
            [Console]::SetIn($global:probeSavedInput)
        }
        [System.IO.File]::WriteAllText($global:probeResultPath, ($global:probeResult | ConvertTo-Json -Compress))
        return 'exit'
    }
    catch {
        if ($null -ne $global:probeSavedInput) { [Console]::SetIn($global:probeSavedInput) }
        [System.IO.File]::WriteAllText($global:probeResultPath, (@{ Error = $_.Exception.ToString() } | ConvertTo-Json -Compress))
        return 'exit 1'
    }
}
