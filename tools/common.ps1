using namespace System.IO
using namespace System.Net
using namespace System.Runtime.InteropServices

# Common code used in the build.ps1 scripts of each process

$ErrorActionPreference = 'Stop'

Function Get-NugetAssembly {
    <#
    .SYNOPSIS
    Downloads the assembly.
    #>
    [OutputType([string])]
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [string]
        $Name,

        [Parameter(Mandatory)]
        [string]
        $Version
    )

    $targetFolder = Join-Path $PSScriptRoot bin
    if (-not (Test-Path -LiteralPath $targetFolder)) {
        New-Item -Path $targetFolder -ItemType Directory | Out-Null
    }

    $downloadUrl = "https://globalcdn.nuget.org/packages/$($Name.ToLowerInvariant()).$Version.nupkg"
    $targetFile = Join-Path $targetFolder "$Name.$Version.zip"

    $assemblyFolder = Join-Path $targetFolder "$Name.$Version"
    if (-not (Test-Path -LiteralPath $assemblyFolder)) {
        New-Item -Path $assemblyFolder -ItemType Directory | Out-Null
    }

    if (-not (Test-Path -LiteralPath $targetFile)) {
        $oldSecurityProtocol = [ServicePointManager]::SecurityProtocol
        try {
            & {
                $ProgressPreference = 'SilentlyContinue'
                [ServicePointManager]::SecurityProtocol = 'Tls12'
                Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $targetFile
            }
        }
        finally {
            [ServicePointManager]::SecurityProtocol = $oldSecurityProtocol
        }
    }

    Add-Type -As System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::Open(
        $targetFile,
        "Read")
    try {
        $archive.Entries | Where-Object {
            $_.FullName -like "lib/*/*.dll"
        } | ForEach-Object {
            $dllName = Split-Path -Path $_.FullName -Leaf
            $dllFolder = (Split-Path -Path $_.FullName -Parent).Substring(4)

            $binFolder = Join-Path $assemblyFolder $dllFolder
            if (-not (Test-Path -LiteralPath $binFolder)) {
                New-Item -Path $binFolder -ItemType Directory | Out-Null
            }

            $dllPath = Join-Path $binFolder $dllName
            if (-not (Test-Path -LiteralPath $dllPath)) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($_, $dllPath)
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $assemblyFolder
}

Function Get-PowerShell {
    <#
    .SYNOPSIS
    Downloads the version of PowerShell specified.

    .PARAMETER Version
    The version of PowerShell to download.
    #>
    [OutputType([string])]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version
    )

    $releaseArch = switch ([RuntimeInformation]::ProcessArchitecture) {
        X64 { 'x64' }
        X86 { 'x86' }
        ARM64 { 'arm64' }
        default {
            $err = [ErrorRecord]::new(
                [Exception]::new("Unsupported archecture requests '$_'"),
                "UnknownArch",
                [ErrorCategory]::InvalidArgument,
                $_
            )
            $PSCmdlet.ThrowTerminatingError($err)
        }
    }

    $targetFolder = Join-Path $PSScriptRoot bin
    if (-not (Test-Path -LiteralPath $targetFolder)) {
        New-Item -Path $targetFolder -ItemType Directory | Out-Null
    }

    if (-not $IsCoreCLR -or $IsWindows) {
        $downloadUrl = "https://github.com/PowerShell/PowerShell/releases/download/v$Version/PowerShell-$Version-win-$releaseArch.zip"
        $fileName = "pwsh-$Version.zip"
        $nativeExt = ".exe"
    }
    else {
        $downloadUrl = "https://github.com/PowerShell/PowerShell/releases/download/v$Version/powershell-$Version-linux-$releaseArch.tar.gz"
        $fileName = "pwsh-$Version.tar.gz"
        $nativeExt = ""
    }

    $targetFile = Join-Path $targetFolder $fileName
    if (-not (Test-Path -LiteralPath $targetFile)) {
        $oldSecurityProtocol = [ServicePointManager]::SecurityProtocol
        try {
            & {
                $ProgressPreference = 'SilentlyContinue'
                [ServicePointManager]::SecurityProtocol = 'Tls12'
                Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $targetFile
            }
        }
        finally {
            [ServicePointManager]::SecurityProtocol = $oldSecurityProtocol
        }
    }

    $pwshFolder = Join-Path $targetFolder "pwsh-$Version"
    if (-not (Test-Path -LiteralPath $pwshFolder)) {
        New-Item -Path $pwshFolder -ItemType Directory | Out-Null
    }

    $pwshFile = Join-Path $pwshFolder "pwsh$nativeExt"
    if (-not (Test-Path -LiteralPath $pwshFile)) {
        if (-not $IsCoreCLR -or $IsWindows) {
            $oldPreference = $global:ProgressPreference
            try {
                $global:ProgressPreference = 'SilentlyContinue'
                Expand-Archive -LiteralPath $targetFile -DestinationPath $pwshFolder
            }
            finally {
                $global:ProgressPreference = $oldPreference
            }
        }
        else {
            tar -xf $targetFile --directory $pwshFolder
            if ($LASTEXITCODE) {
                throw "Failed to extract pwsh tar for $Version"
            }

            chmod +x $pwshFile
            if ($LASTEXITCODE) {
                throw "Failed to set pwsh as executable at $pwshFile"
            }
        }
    }

    $pwshFile
}

Function Get-BuildInfo {
    <#
    .SYNOPSIS
    Gets the module build information.

    .PARAMETER Path
    The module directory.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [string]
        $Path
    )

    $moduleSrc = [Path]::Combine($Path, 'module')
    $manifestItem = Get-Item -Path ([Path]::Combine($moduleSrc, '*.psd1'))
    $manifest = Test-ModuleManifest -Path $manifestItem.FullName -ErrorAction Ignore -WarningAction Ignore
    $moduleName = $manifest.Name
    $moduleVersion = $manifest.Version

    $dotnetSrc = [Path]::Combine($Path, "src", $moduleName)
    if (Test-Path -LiteralPath $dotnetSrc) {
        [xml]$csharpProjectInfo = Get-Content -Path ([Path]::Combine($dotnetSrc, '*.csproj'))
        $targetFrameworks = @(@($csharpProjectInfo.Project.PropertyGroup)[0].TargetFrameworks.Split(
                ';', [StringSplitOptions]::RemoveEmptyEntries))
    }
    else {
        $dotnetSrc = $null
        $targetFrameworks = @()
    }

    [Ordered]@{
        ModuleName = $moduleName
        Version = $moduleVersion
        PowerShellSource = $moduleSrc
        DotnetSource = $dotnetSrc
        Configuration = "Release"
        TargetFrameworks = $targetFrameworks
        BuildDir = [Path]::Combine($Path, 'output', $build.ModuleName, $build.Version)
    }
}

Function Invoke-ModuleBuilder {
    <#
    .SYNOPSIS
    Builds the module.

    .PARAMETER Path
    The module directory to build.
    #>
    [CmdletBinding()]
    [Alias('Invoke-ModuleBuild')]
    param (
        [Parameter(Mandatory)]
        [string]
        $Path
    )

    Write-Host "Getting build information"
    $Build = Get-BuildInfo -Path $Path

    if (-not (Test-Path -LiteralPath $Build.BuildDir)) {
        New-Item -Path $Build.BuildDir -ItemType Directory -Force | Out-Null
    }

    Write-Host "Compiling Dotnet assemblies"
    Push-Location -LiteralPath $Build.DotnetSource
    try {
        $dotnetArgs = @(
            'publish'
            '--configuration', $Build.Configuration,
            '--verbosity', 'q',
            '-nologo',
            "-p:Version=$($Build.Version)"
        )

        foreach ($framework in $Build.TargetFrameworks) {
            dotnet @dotnetArgs --framework $framework
            if ($LASTEXITCODE) {
                throw "Failed to compile code for $framework"
            }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "Build PowerShell module result"
    Copy-Item -Path ([Path]::Combine($Build.PowerShellSource, "*")) -Destination $Build.BuildDir -Recurse

    foreach ($framework in $Build.TargetFrameworks) {
        $publishFolder = [Path]::Combine($Build.DotnetSource, "bin", $Build.Configuration, $framework, "publish")
        $binFolder = [Path]::Combine($Build.BuildDir, "bin", $framework)
        if (-not (Test-Path -LiteralPath $binFolder)) {
            New-Item -Path $binFolder -ItemType Directory | Out-Null
        }
        Copy-Item ([Path]::Combine($publishFolder, "*")) -Destination $binFolder -Recurse
    }
}

if (-not $IsCoreCLR -or $IsWindows) {
    Function Add-GacAssembly {
        [CmdletBinding()]
        param (
            [Parameter(Mandatory)]
            [string]
            $Path
        )

        if (-not (Test-Path -LiteralPath $Path)) {
            throw "Assembly does not exist at path '$Path'"
        }

        $invokeParams = @{}
        if ($IsCoreCLR) {
            $s = New-PSSession -UseWindowsPowerShell
            $invokeParams.Session = $s
        }

        try {
            Invoke-Command @invokeParams -ScriptBlock {
                $ErrorActionPreference = 'Stop'

                [System.Reflection.Assembly]::LoadWithPartialName("System.EnterpriseServices") | Out-Null
                $publish = [System.EnterpriseServices.Internal.Publish]::new()
                $publish.GacInstall($args[0])
            } -ArgumentList $Path
        }
        finally {
            if ($s) { Remove-PSSession -Session $s }
        }
    }

    Function Remove-GacAssembly {
        [CmdletBinding()]
        param (
            [Parameter(Mandatory)]
            [string]
            $Path
        )

        if (-not (Test-Path -LiteralPath $Path)) {
            throw "Assembly does not exist at path '$Path'"
        }

        $invokeParams = @{}
        if ($IsCoreCLR) {
            $s = New-PSSession -UseWindowsPowerShell
            $invokeParams.Session = $s
        }

        try {
            Invoke-Command @invokeParams -ScriptBlock {
                $ErrorActionPreference = 'Stop'

                [System.Reflection.Assembly]::LoadWithPartialName("System.EnterpriseServices") | Out-Null
                $publish = [System.EnterpriseServices.Internal.Publish]::new()
                $publish.GacRemove($args[0])
            } -ArgumentList $Path
        }
        finally {
            if ($s) { Remove-PSSession -Session $s }
        }
    }
}
