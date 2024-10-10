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
    Install-Module -Name $_ -Force -Scope CurrentUser
    Import-Module -Name $_ -Force
}

$output = Join-Path $reporoot 'output'
if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}
Invoke-ModuleBuild -Path $reporoot

Get-ChildItem $output -Recurse -File | Where-Object { $_.Extension -in '.json','.pdb' } | Remove-Item -Force

$docs = Join-Path $reporoot 'docs'

Get-ChildItem -LiteralPath $docs -Directory | ForEach-Object {
        Write-Host "Building docs for $($_.Name)"
        $helpParams = @{
            Path = $_.FullName
            OutputPath = [System.IO.Path]::Combine($output, $_.Name)
            Encoding = [System.Text.Encoding]::UTF8
        }
        New-ExternalHelp @helpParams | Out-Null
    }

if ($Publish) {
    $module = Get-Module $output/Sixel.psd1 -ListAvailable
    $v = 'v' + $module.Version.ToString()
    Publish-PSResource -Path $output -ApiKey $env:NuGetApiKey -Repository PSGallery -ErrorAction Stop
    gh release create $v --target prerelease --generate-notes --prerelease
}
