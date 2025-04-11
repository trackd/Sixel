[cmdletbinding()]
param(
    [Switch]$Publish
)

. $PSScriptRoot/common.ps1

$reporoot = Split-Path $PSScriptRoot
@(
    'Pester'
    'PlatyPS'
) | ForEach-Object {
    if (-not (Get-Module -Name $_ -ListAvailable)) {
        Install-Module -Name $_ -Force -Scope CurrentUser
    }
    Import-Module -Name $_ -Force
}

$output = Join-Path $reporoot 'output'
if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}
$csproj = Get-ChildItem -File -Include *.csproj -Recurse -Path $reporoot
$ModuleFile = Import-PowerShellDataFile -Path (Join-Path $reporoot 'Module' 'Sixel.psd1')
$newVersion = '<Version>{0}</Version>' -f $ModuleFile.ModuleVersion.ToString()

foreach ($project in $csproj) {
    $Content = Get-Content $project.FullName -Raw
    # update <Version> tag in csproj file to match Module version
    if ($Content -match $newVersion) {
        # Write-Host "Version already set to $newVersion in $($project.Name)"
        continue
    }
    $Content = $Content -replace '<Version>.*</Version>', $newVersion
    Write-Host "Updating version to $newVersion in $($project.Name)"
    Set-Content -Path $project.FullName -Value $Content -Force
}

Invoke-ModuleBuilder -Path $reporoot

$docspath = Join-Path $output 'en-US'
if (Test-Path $docspath) {
    Remove-Item $docspath -Recurse -Force
}
Get-ChildItem $output -Recurse -File | Where-Object { $_.Extension -in '.json', '.pdb' } | Remove-Item -Force

$docs = Join-Path $reporoot 'docs'

Get-ChildItem -LiteralPath $docs -Directory | ForEach-Object {
    Write-Host "Building docs for $($_.Name)"
    $helpParams = @{
        Path       = $_.FullName
        OutputPath = [System.IO.Path]::Combine($output, $_.Name)
        Encoding   = [System.Text.Encoding]::UTF8
    }
    $null = New-ExternalHelp @helpParams
}

$testargs = @{
    reportFile = [System.IO.Path]::Combine($reporoot, 'testdata', ('Sixel.report-{0}.xml' -f (Get-Date).ToString('yyyyMMdd-HHmmss')))
    TestPath   = [System.IO.Path]::Combine($reporoot, 'tests')
    tools      = $PSScriptRoot
}
$sb = {
    param($ht)
    $tools, $TestPath, $reportFile = $ht.tools, $ht.TestPath, $ht.reportFile
    & $tools/Pester.ps1 -TestPath $TestPath -OutputFile $reportFile
}

if ($PSVersionTable.PSEdition -eq 'Core') {
    pwsh -NoProfile -Command $sb -args $testargs
}
else {
    # disabled test for 5.1, sixlabor produces slightly different output.
    # powershell -NoProfile -Command $sb -args $testargs
}

if ($Publish) {
    $module = Get-Module $output/Sixel.psd1 -ListAvailable
    $v = 'v' + $module.Version.ToString()
    Publish-PSResource -Path $output -ApiKey $env:NuGetApiKey -Repository PSGallery -ErrorAction Stop
    gh release create $v --target prerelease --generate-notes --prerelease
}
