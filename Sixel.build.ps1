#! /usr/bin/pwsh
#Requires -Version 7.4 -Module InvokeBuild
param(
    [string]$Configuration = 'Release',
    [switch]$SkipHelp,
    [switch]$SkipTests
)
Write-Host "$($PSBoundParameters.GetEnumerator())" -ForegroundColor Cyan

$modulename = [System.IO.Path]::GetFileName($PSCommandPath) -replace '\.build\.ps1$'

$script:folders = @{
    ModuleName       = $modulename
    ProjectRoot      = $PSScriptRoot
    SourcePath       = Join-Path $PSScriptRoot 'src' $modulename
    OutputPath       = Join-Path $PSScriptRoot 'output'
    DestinationPath  = Join-Path $PSScriptRoot 'output' 'lib'
    ModuleSourcePath = Join-Path $PSScriptRoot 'module'
    DocsPath         = Join-Path $PSScriptRoot 'docs'
    TestPath         = Join-Path $PSScriptRoot 'tests'
    CsprojPath       = Join-Path $PSScriptRoot 'src' $modulename "$modulename.csproj"
}

task Reset {
    if (Test-Path $folders.OutputPath) {
        Remove-Item -Path $folders.OutputPath -Recurse -Force -ErrorAction 'Ignore'
    }
    New-Item -Path $folders.OutputPath -ItemType Directory -Force | Out-Null
}

task Build {
    if (-not (Test-Path $folders.CsprojPath)) {
        Write-Warning 'C# project not found, skipping Build'
        return
    }

    $ModuleFile = Import-PowerShellDataFile -Path (Join-Path $folders.ModuleSourcePath "$($folders.ModuleName).psd1")
    [xml]$csproj = Get-Content -Path $folders.CsprojPath -Raw
    $frameworks = $csproj.
    SelectNodes('//TargetFramework | //TargetFrameworks').
    '#text'.
    Split(';', [StringSplitOptions]::RemoveEmptyEntries)

    $dotnetArgs = @(
        'publish'
        $folders.CsprojPath
        '--configuration', $Configuration
        '--nologo'
        '--verbosity', 'minimal'
        ('-p:Version={0}' -f $ModuleFile.ModuleVersion.ToString())
    )

    foreach ($fwork in $frameworks) {
        exec { dotnet @dotnetArgs --framework $fwork --output (Join-Path $folders.DestinationPath $fwork) }
    }
}

task ModuleFiles {
    if (Test-Path $folders.ModuleSourcePath) {
        Get-ChildItem -Path $folders.ModuleSourcePath -File | Copy-Item -Destination $folders.OutputPath -Force
    }
    else {
        Write-Warning "Module directory not found at: $($folders.ModuleSourcePath)"
    }
}

task GenerateHelp -if (-not $SkipHelp) {
    if (-not (Test-Path $folders.DocsPath)) {
        Write-Warning "Documentation path not found at: $($folders.DocsPath)"
        return
    }
    if (-not (Get-Module -ListAvailable -Name Microsoft.PowerShell.PlatyPS)) {
        Write-Host '    Installing Microsoft.PowerShell.PlatyPS...' -ForegroundColor Yellow
        Install-Module -Name Microsoft.PowerShell.PlatyPS -Scope CurrentUser -Force -AllowClobber
    }

    Import-Module Microsoft.PowerShell.PlatyPS -ErrorAction Stop

    $modulePath = Join-Path $folders.OutputPath ($folders.ModuleName + '.psd1')
    if (-not (Test-Path $modulePath)) {
        Write-Warning "Module manifest not found at: $modulePath. Skipping help generation."
        return
    }

    Import-Module $modulePath -Force

    foreach ($lang in (Get-ChildItem -Path $folders.DocsPath -Directory)) {
        $helpOutputPath = Join-Path $folders.OutputPath $lang.Name
        if (-not (Test-Path $helpOutputPath)) {
            New-Item -Path $helpOutputPath -ItemType Directory -Force | Out-Null
        }

        $allCommandHelp = Get-ChildItem -Path $lang -Filter '*.md' -Recurse -File |
            Where-Object { $_.Name -ne "$($folders.ModuleName).md" } |
            Import-MarkdownCommandHelp

        if ($allCommandHelp.Count -gt 0) {
            $tempOutputPath = Join-Path $helpOutputPath 'temp'
            Export-MamlCommandHelp -CommandHelp $allCommandHelp -OutputFolder $tempOutputPath -Force | Out-Null

            $generatedFile = Get-ChildItem -Path $tempOutputPath -Filter '*.xml' -Recurse -File | Select-Object -First 1
            if ($generatedFile) {
                Move-Item -Path $generatedFile.FullName -Destination $helpOutputPath -Force
            }
            Remove-Item -Path $tempOutputPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}


task Test -if (-not $SkipTests) {
    if (-not (Test-Path $folders.TestPath)) {
        Write-Warning "Test directory not found at: $($folders.TestPath)"
        return
    }
    $resultsPath = Join-Path $folders.ProjectRoot 'artifacts/test-results'
    New-Item -Path $resultsPath -ItemType Directory -Force | Out-Null
    exec { dotnet test (Join-Path $folders.TestPath 'Sixel.Protocol.Tests') --configuration $Configuration --results-directory $resultsPath --logger 'trx;LogFileName=protocol.trx' }
    $pesterConfig = New-PesterConfiguration
    # $pesterConfig.Output.Verbosity = 'Detailed'
    $pesterConfig.Run.Path = $folders.TestPath
    $pesterConfig.Run.Throw = $true
    $pesterConfig.Run.PassThru = $true
    $pesterConfig.TestResult.Enabled = $true
    $pesterConfig.TestResult.OutputPath = Join-Path $resultsPath 'pester.xml'
    $pesterConfig.Debug.WriteDebugMessages = $false
    $result = Invoke-Pester -Configuration $pesterConfig
    if ($result.PassedCount -eq 0) { throw 'Pester did not run any passing tests.' }
}

task CleanAfter {
    if ($script:folders.DestinationPath -and (Test-Path $script:folders.DestinationPath)) {
        Get-ChildItem -Path $script:folders.DestinationPath -File -Recurse |
            Where-Object { $_.Extension -in @('.pdb', '.json') } |
            Remove-Item -Force -ErrorAction Ignore
    }
}

task All -Jobs Reset, Build, ModuleFiles, GenerateHelp, CleanAfter, Test
