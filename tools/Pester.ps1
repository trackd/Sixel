using namespace System.IO
using namespace System.Runtime.InteropServices

#Requires -Module Pester

<#
.SYNOPSIS
Run Pester test

.PARAMETER TestPath
The path to the tests to run

.PARAMETER OutputFile
The path to write the Pester test results to.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [String]
    $TestPath,

    [Parameter()]
    [String]
    $OutputFile
)

$ErrorActionPreference = 'Stop'

[PSCustomObject]$PSVersionTable |
    Select-Object -Property *, @{
        N = 'Architecture'; E = { [RuntimeInformation]::ProcessArchitecture.ToString() }
    } |
    Format-List |
    Out-Host

$configuration = [PesterConfiguration]::Default
$configuration.Output.Verbosity = 'Detailed'
$configuration.Run.Path = $TestPath
$configuration.Run.Throw = $true
if ($OutputFile) {
    # $configuration.TestResult.Enabled = $true
    # $configuration.TestResult.OutputPath = $OutputFile
    # $configuration.TestResult.OutputFormat = 'NUnitXml'
}


Invoke-Pester -Configuration $configuration -WarningAction Ignore
