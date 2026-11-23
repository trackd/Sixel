Describe 'Build entry point' -Tag 'Build' {
    BeforeAll {
        $projectRoot = Split-Path -Parent $PSScriptRoot
        $validationWorkflow = Get-Content -LiteralPath (Join-Path $projectRoot '.github/workflows/validate.yml') -Raw
    }

    It 'Exposes the bootstrap, build and test tasks through build.ps1' {
        $powerShell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
        $taskList = & $powerShell -NoProfile -File (Join-Path $projectRoot 'build.ps1') -Task '?' | Out-String -Width 240
        $LASTEXITCODE | Should -Be 0
        $taskList | Should -Match '(?m)^Bootstrap\s'
        # Format-Table truncates the jobs column, so match its stable prefix.
        $taskList | Should -Match 'All\s+\{Reset, Build, ModuleFiles, GenerateHelp'
    }

    It 'Routes CI through build.ps1 and its pinned dependencies' {
        $validationWorkflow | Should -Match '(?m)^\s+\./build\.ps1 -Configuration Release\s*$'
        # Windows PowerShell 5.1 tests install their own Pester; pwsh steps use the pinned .tools modules.
        $pwshSteps = $validationWorkflow -replace '(?ms)- name: Test built module in Windows PowerShell 5\.1.*?(?=^      - )', ''
        $pwshSteps | Should -Not -Match '\bInstall-Module\b|\bInstall-PSResource\b'
        $pwshSteps | Should -Match 'Import-Module Microsoft\.PowerShell\.PSResourceGet -RequiredVersion 1\.2\.0'
    }

    It 'Bootstraps missing pinned modules into a path inherited by the build worker and reuses them offline' {
        $root = Join-Path $TestDrive 'bootstrap'
        $moduleRoot = Join-Path $root 'modules'
        $fastPath = Join-Path $moduleRoot 'ModuleFast' '0.6.1'
        $null = New-Item -ItemType Directory -Path $fastPath -Force
        Copy-Item -LiteralPath (Join-Path $projectRoot 'build.ps1') -Destination $root
        $versions = @{
            InvokeBuild = '5.14.23'
            'Microsoft.PowerShell.PlatyPS' = '1.0.1'
            Pester = '6.2.0'
            'Microsoft.PowerShell.PSResourceGet' = '1.2.0'
        }
        $sources = @{}
        foreach ($module in $versions.GetEnumerator()) {
            $installed = Get-Module -ListAvailable -FullyQualifiedName @{
                ModuleName = $module.Key
                RequiredVersion = $module.Value
            } | Select-Object -First 1
            $installed | Should -Not -BeNullOrEmpty -Because "build.ps1 provides $($module.Key) $($module.Value) to the test run"
            $sources[$module.Key] = $installed.ModuleBase
        }
        $sources | Export-Clixml -LiteralPath (Join-Path $root 'sources.xml')
        Set-Content -LiteralPath (Join-Path $fastPath 'ModuleFast.psd1') -Value "@{ ModuleVersion = '0.6.1'; RootModule = 'ModuleFast.psm1' }"
        Set-Content -LiteralPath (Join-Path $fastPath 'ModuleFast.psm1') -Value @'
function Install-ModuleFast {
    [CmdletBinding()]
    param($Specification, $Destination, $Source, [switch]$NoPSModulePathUpdate, [switch]$NoProfileUpdate, $ThrottleLimit)
    if ($Source -ne 'https://pwsh.gallery/index.json' -or -not $NoPSModulePathUpdate -or -not $NoProfileUpdate) {
        throw 'Bootstrap must use the mirror without modifying user configuration.'
    }
    $root = Split-Path -Parent (Split-Path -Parent $Destination)
    $sources = Import-Clixml -LiteralPath (Join-Path $root 'sources.xml')
    foreach ($spec in $Specification) {
        $name, $version = $spec.Split('=')
        $target = Join-Path $Destination $name $version
        $null = New-Item -ItemType Directory -Path $target -Force
        Get-ChildItem -LiteralPath $sources[$name] -Force | Copy-Item -Destination $target -Recurse -Force
        Add-Content -LiteralPath (Join-Path $root 'installed.txt') -Value $spec
    }
}
'@
        Set-Content -LiteralPath (Join-Path $root 'Sixel.build.ps1') -Value @'
param($Configuration, [switch]$SkipHelp, [switch]$SkipTests)
task Bootstrap {
    foreach ($name in 'InvokeBuild', 'Microsoft.PowerShell.PlatyPS', 'Pester', 'Microsoft.PowerShell.PSResourceGet') {
        if (-not (Get-Module $name)) { throw "$name was not imported in the worker." }
    }
    Set-Content -LiteralPath (Join-Path $PSScriptRoot 'worker.txt') -Value $env:PSModulePath
}
'@
        $powerShell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
        $run = {
            param($root)
            $env:PSModulePath = (Join-Path $root 'modules') + [IO.Path]::PathSeparator + (Join-Path $PSHOME 'Modules')
            & (Join-Path $root 'build.ps1') -Task Bootstrap
            exit $LASTEXITCODE
        }
        & $powerShell -NoProfile -Command $run -args $root
        $LASTEXITCODE | Should -Be 0
        $installed = @(Get-Content -LiteralPath (Join-Path $root 'installed.txt'))
        $installed | Should -Contain 'InvokeBuild=5.14.23'
        $installed | Should -Contain 'Microsoft.PowerShell.PlatyPS=1.0.1'
        $installed | Should -Contain 'Pester=6.2.0'
        $installed | Should -Contain 'Microsoft.PowerShell.PSResourceGet=1.2.0'
        (Get-Content -LiteralPath (Join-Path $root 'worker.txt') -Raw) |
            Should -Match ([regex]::Escape((Join-Path $root '.tools' 'Modules')))
        Remove-Item -LiteralPath (Join-Path $fastPath 'ModuleFast.psm1')
        & $powerShell -NoProfile -Command $run -args $root
        $LASTEXITCODE | Should -Be 0 -Because 'The second build must need neither ModuleFast nor the network.'
        (Get-Content -LiteralPath (Join-Path $root 'installed.txt')) -join ',' | Should -Be ($installed -join ',')
    }
}

Describe 'CI ownership, secrets and release boundaries' -Tag 'Build' {
    BeforeAll {
        $projectRoot = Split-Path -Parent $PSScriptRoot
        $releaseWorkflow = Get-Content -LiteralPath (Join-Path $projectRoot '.github/workflows/release.yml') -Raw
        $validationWorkflow = Get-Content -LiteralPath (Join-Path $projectRoot '.github/workflows/validate.yml') -Raw
        $codeOwnersContent = Get-Content -LiteralPath (Join-Path $projectRoot '.github/CODEOWNERS') -Raw
        $gitIgnore = Get-Content -LiteralPath (Join-Path $projectRoot '.gitignore') -Raw
    }

    It 'Assigns CI and project builds to trackd' {
        foreach ($pattern in @('/.github/', '/build.ps1', '/*.build.ps1', '*.csproj')) {
            $codeOwnersContent | Should -Match ('(?m)^' + [regex]::Escape($pattern) + '\s+@trackd\s*$')
        }
    }

    It 'Keeps bootstrapped tools and the local license file out of the repository' {
        $gitIgnore | Should -Match '(?m)^\.tools/\s*$'
        $gitIgnore | Should -Match '(?m)^sixlabors\.lic\s*$'
    }

    It 'Restricts the <JobName> release job to the canonical repository and default branch' -ForEach @(
        @{ JobName = 'prepare' }
        @{ JobName = 'publish' }
    ) {
        $job = [regex]::Match($releaseWorkflow, "(?ms)^  ${JobName}:\r?\n(?<Body>.*?)(?=^  [a-z][a-z0-9_-]*:\r?$|\z)")
        $job.Success | Should -BeTrue
        $condition = [regex]::Match($job.Groups['Body'].Value, '(?m)^    if: (.+)\r?$')
        $condition.Success | Should -BeTrue
        $condition.Groups[1].Value.Trim() | Should -BeExactly "github.repository == 'trackd/Sixel' && github.ref == format('refs/heads/{0}', github.event.repository.default_branch)"
    }

    It 'Keeps releases manual and validation read-only' {
        [regex]::Match($releaseWorkflow, '(?ms)^on:\r?\n(?<Triggers>.*?)(?=^[^\s])').Groups['Triggers'].Value.Trim() | Should -BeExactly 'workflow_dispatch:'
        $validationWorkflow | Should -Match '(?m)^permissions:\r?\n  contents: read\s*$'
        $validationWorkflow | Should -Not -Match 'pull_request_target|workflow_run'
        $validationWorkflow | Should -Match '(?m)^\s+persist-credentials: false\s*$'
    }

    It 'Gives the ImageSharp build only the SixLabors license key' {
        @([regex]::Matches($validationWorkflow, 'secrets\.(\w+)').ForEach({ $_.Groups[1].Value }) | Select-Object -Unique) |
            Should -BeExactly @('SIXLABORS_LICENSE_KEY')
        $validationWorkflow | Should -Match '(?m)^\s+SixLaborsLicenseKey: \$\{\{ secrets\.SIXLABORS_LICENSE_KEY \}\}\s*$'
        $validationWorkflow | Should -Match '(?ms)workflow_call:\s+secrets:\s+SIXLABORS_LICENSE_KEY:\s+required: true'
    }

    It 'Passes the license key to release validation and the NuGet key only to publishing' {
        $validate = [regex]::Match($releaseWorkflow, '(?ms)^  validate:\r?\n(?<Body>.*?)(?=^  [a-z][a-z0-9_-]*:\r?$|\z)').Groups['Body'].Value
        $validate | Should -Match 'SIXLABORS_LICENSE_KEY: \$\{\{ secrets\.SIXLABORS_LICENSE_KEY \}\}'
        $publish = [regex]::Match($releaseWorkflow, '(?ms)^  publish:\r?\n(?<Body>.*?)(?=^  [a-z][a-z0-9_-]*:\r?$|\z)').Groups['Body'].Value
        [regex]::Matches($publish, 'secrets\.NUGETAPIKEY').Count | Should -Be 2
        $publish | Should -Match 'Publish-PSResource -NupkgPath \$env:PACKAGE_PATH -Repository PSGallery -ApiKey \$env:PSGALLERY_API_KEY'
        [regex]::Matches($releaseWorkflow, 'secrets\.NUGETAPIKEY').Count | Should -Be 2 -Because 'no other job sees the NuGet key'
    }
}

Describe 'Release artifact selection' -Tag 'Build' {
    BeforeAll {
        $assetScript = Join-Path $PSScriptRoot '../.github/Get-ReleaseAsset.ps1'
    }

    It 'Returns the exact module package without changing its contents' {
        $directory = Join-Path $TestDrive 'valid'
        New-Item -Path $directory -ItemType Directory | Out-Null
        $package = Join-Path $directory 'Sixel.2.3.0.nupkg'
        Set-Content -LiteralPath $package -Value 'package fixture'
        $hash = (Get-FileHash -LiteralPath $package).Hash

        $asset = & $assetScript -Version '2.3.0' -Path $directory

        $asset | Should -BeOfType ([IO.FileInfo])
        $asset.FullName | Should -BeExactly $package
        (Get-FileHash -LiteralPath $package).Hash | Should -BeExactly $hash
    }

    It 'Rejects missing, mismatched and incorrectly cased packages (<Name>)' -ForEach @(
        @{ Name = 'missing'; FileName = $null }
        @{ Name = 'wrong version'; FileName = 'Sixel.2.2.0.nupkg' }
        @{ Name = 'wrong case'; FileName = 'sixel.2.3.0.nupkg' }
    ) {
        $directory = Join-Path $TestDrive $Name
        New-Item -Path $directory -ItemType Directory | Out-Null
        if ($FileName) {
            Set-Content -LiteralPath (Join-Path $directory $FileName) -Value 'fixture'
        }
        { & $assetScript -Version '2.3.0' -Path $directory } | Should -Throw '*Expected exactly one release asset*'
    }

    It 'Rejects duplicate packages from separate downloaded artifact folders' {
        $directory = Join-Path $TestDrive 'duplicates'
        foreach ($folder in @('first', 'second')) {
            $child = Join-Path $directory $folder
            New-Item -Path $child -ItemType Directory -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $child 'Sixel.2.3.0.nupkg') -Value 'fixture'
        }
        { & $assetScript -Version '2.3.0' -Path $directory } | Should -Throw '*found 2*'
    }

    It 'Rejects unexpected files alongside the expected module package' {
        $directory = Join-Path $TestDrive 'unexpected'
        New-Item -Path $directory -ItemType Directory | Out-Null
        Set-Content -LiteralPath (Join-Path $directory 'Sixel.2.3.0.nupkg') -Value 'fixture'
        Set-Content -LiteralPath (Join-Path $directory 'extra.txt') -Value 'fixture'
        { & $assetScript -Version '2.3.0' -Path $directory } | Should -Throw '*unexpected files*'
    }
}

Describe 'Release version' -Tag 'Build' {
    BeforeAll {
        $projectRoot = Split-Path -Parent $PSScriptRoot
    }

    It 'Updates only the manifest version' {
        $path = Join-Path $TestDrive 'Sixel.psd1'
        $source = Get-Content -LiteralPath (Join-Path $projectRoot 'module/Sixel.psd1') -Raw
        Set-Content -LiteralPath $path -Value $source -NoNewline

        & (Join-Path $projectRoot '.github/Set-ModuleVersion.ps1') -Version '2.3.0' -Path $path

        (Import-PowerShellDataFile -LiteralPath $path).ModuleVersion | Should -Be '2.3.0'
        $expected = [regex]::Replace($source, "(?m)^(\s*ModuleVersion\s*=\s*')[^']+(')", '${1}2.3.0${2}')
        (Get-Content -LiteralPath $path -Raw) | Should -BeExactly $expected
    }

    It 'Rejects ambiguous manifest versions without changing the file' {
        $path = Join-Path $TestDrive 'ambiguous.psd1'
        $source = "ModuleVersion = '1.0.0'`nModuleVersion = '1.1.0'"
        Set-Content -LiteralPath $path -Value $source -NoNewline

        { & (Join-Path $projectRoot '.github/Set-ModuleVersion.ps1') -Version '2.3.0' -Path $path } | Should -Throw '*Expected one ModuleVersion*'
        (Get-Content -LiteralPath $path -Raw) | Should -BeExactly $source
    }

    It 'Ships the manifest version in the <Framework> assembly' -ForEach @(
        @{ Framework = 'net8.0' }
        @{ Framework = 'net472' }
    ) {
        $manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $projectRoot 'output/Sixel.psd1')
        $assemblyPath = Join-Path $projectRoot "output/lib/$Framework/Sixel.dll"
        [Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath).ProductVersion.Split('+')[0] |
            Should -Be $manifest.ModuleVersion
    }
}
