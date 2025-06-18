. $PSScriptRoot/common.ps1

@(
    'Pester'
    'PlatyPS'
) | ForEach-Object {
    if (-not (Get-Module -Name $_ -ListAvailable)) {
        Install-Module -Name $_ -Force -Scope CurrentUser
    }
    Import-Module -Name $_ -Force
}
$reporoot = Split-Path $PSScriptRoot

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
