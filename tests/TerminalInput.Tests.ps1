# Real ConsoleHost/PSReadLine regressions. No VT queries touch the Pester host.
Describe 'Interactive terminal input regressions' -Tag 'Integration', 'TerminalInput' {
    BeforeAll {
        $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
        $diagnosticProject = Join-Path $PSScriptRoot 'diagnostics/ConPtyProbe/ConPtyProbe.csproj'
        if ($IsWindows) {
            $conptyLibrary = $env:SIXEL_TEST_CONPTY_DLL
            if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'output/Sixel.psd1'))) {
                throw 'Build the Sixel module into output before running terminal integration tests.'
            }
            $architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
            $runtime = "win-$architecture"
            $buildOutput = & dotnet build $diagnosticProject --configuration Release --runtime $runtime "-p:PlatformTarget=$architecture" --nologo --verbosity quiet 2>&1
            if ($LASTEXITCODE -ne 0) { throw ($buildOutput -join [Environment]::NewLine) }
            $diagnosticDirectory = Join-Path $PSScriptRoot "diagnostics/ConPtyProbe/bin/Release/net8.0/$runtime"
            $diagnosticAssembly = Join-Path $diagnosticDirectory 'ConPtyProbe.dll'
            if (-not $conptyLibrary) { $conptyLibrary = Join-Path $diagnosticDirectory 'conpty.dll' }
        }
        else {
            $conptyLibrary = 'script'
            $buildOutput = & dotnet build $diagnosticProject --configuration Release --nologo --verbosity quiet 2>&1
            if ($LASTEXITCODE -ne 0) { throw ($buildOutput -join [Environment]::NewLine) }
            $diagnosticAssembly = Join-Path $PSScriptRoot 'diagnostics/ConPtyProbe/bin/Release/net8.0/ConPtyProbe.dll'
        }

        function Invoke-TerminalInputScenario {
            param([int]$ReplyDelay)

            Push-Location -LiteralPath $projectRoot
            try {
                # Each call starts a fresh pwsh.exe -NoProfile, then types the command
                # and Tab before Enter. The child has its own module copy and history.
                $scenarioOutput = & dotnet $diagnosticAssembly $conptyLibrary shell $ReplyDelay 2>&1
                $scenarioExitCode = $LASTEXITCODE
                $jsonLine = $scenarioOutput | Where-Object { $_ -is [string] -and $_.StartsWith('{"Child":') } | Select-Object -Last 1
                if (-not $jsonLine) { throw "No diagnostic result (exit $scenarioExitCode): $($scenarioOutput -join [Environment]::NewLine)" }
                $result = $jsonLine | ConvertFrom-Json
                [pscustomobject]@{
                    ExitCode = $scenarioExitCode
                    EditingQueries = $result.editingQueries
                    ExecutionQueries = $result.executionQueries
                    Child = $result.Child | ConvertFrom-Json
                    WirePath = $result.WirePath
                }
            }
            finally { Pop-Location }
        }
    }

    It 'Auto-imports through real Tab completion without querying while PSReadLine is editing' {
        $result = Invoke-TerminalInputScenario -ReplyDelay 0
        $result.Child.ImportedByCompletion | Should -BeTrue
        $result.Child.CompletedLine | Should -Match 'cog_xkcd\.png'
        $result.EditingQueries | Should -Be 0
        $result.Child.ProbeBeforeExecution | Should -BeFalse
        $result.ExecutionQueries | Should -Be 1
        $result.Child.ProbeAfterExecution | Should -BeTrue
        $result.Child.NextLine | Should -BeExactly 'typed-after-query'
        $result.ExitCode | Should -Be 0 -Because "the isolated exchange must complete; trace: $($result.WirePath)"
    }

    It 'Consumes replies delayed beyond the old 500 ms timeout without contaminating the next input line' {
        $result = Invoke-TerminalInputScenario -ReplyDelay 750
        $result.EditingQueries | Should -Be 0
        $result.ExecutionQueries | Should -Be 1
        $result.Child.ProbeAfterExecution | Should -BeTrue
        $result.Child.NextLine | Should -BeExactly 'typed-after-query' -Because 'DA/APC/DCS replies must not become editable text'
        $result.ExitCode | Should -Be 0 -Because "the isolated exchange must complete; trace: $($result.WirePath)"
    }
}
