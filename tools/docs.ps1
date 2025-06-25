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
