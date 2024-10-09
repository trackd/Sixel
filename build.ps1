. $PSScriptRoot/common.ps1

@(
    'Pester'
    'PlatyPS'
) | ForEach-Object {
    Install-Module -Name $_ -Force -Scope CurrentUser
    Import-Module -Name $_ -Force
}

$output = Join-Path $PSScriptRoot 'output'
if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}
Invoke-ModuleBuild -Path $PSScriptRoot

Get-ChildItem $output -Recurse -File | Where-Object { $_.Extension -in '.json','.pdb' } | Remove-Item -Force

$docs = Join-Path $PSScriptRoot 'docs'

Get-ChildItem -LiteralPath $docs -Directory | ForEach-Object {
        Write-Host "Building docs for $($_.Name)"
        $helpParams = @{
            Path = $_.FullName
            OutputPath = [System.IO.Path]::Combine($output, $_.Name)
            Encoding = [System.Text.Encoding]::UTF8
        }
        New-ExternalHelp @helpParams | Out-Null
    }

$module = Get-Module -Name Sixel
$v = 'v' + $module.Version.ToString()
Publish-Module -Name Sixel -Path $output -NuGetApiKey $env:NuGetApiKey -Repository PSGallery -ErrorAction Stop
gh release create $v --target prerelease --generate-notes --prerelease
