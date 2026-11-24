Describe 'Packaged help for <CommandName>' -ForEach @(
    @{ CommandName = 'ConvertTo-Sixel' }
    @{ CommandName = 'ConvertTo-SixelGif' }
    @{ CommandName = 'ConvertTo-Kitty' }
) {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot '../output/Sixel.psd1')
        $command = Get-Command -Name $CommandName -Module Sixel
        $help = Get-Help -Name $CommandName -Full
    }
    It 'Ships authored help with examples and every parameter documented' {
        $help.Synopsis | Should -Not -BeNullOrEmpty
        $help.Synopsis.Trim() | Should -Not -Match "^$([regex]::Escape($CommandName))(\s|$)"
        ($help.Description.Text -join '') | Should -Not -BeNullOrEmpty
        @($help.Examples.Example).Count | Should -BeGreaterOrEqual 2
        foreach ($example in $help.Examples.Example) {
            $exampleCode = [string]$example.Code
            $explanation = ($example.Remarks.Text -join '').Trim()
            # PlatyPS 1.0.1 can emit authored fenced examples in introduction.
            if (-not $exampleCode) {
                $introduction = $example.Introduction.Text -join "`n"
                $fence = [regex]::Match($introduction, '(?is)```powershell\s*\r?\n(?<code>.*?)```')
                $exampleCode = $fence.Groups['code'].Value
                $explanation = $introduction.Replace($fence.Value, '').Trim()
            }
            $exampleCode | Should -Not -BeNullOrEmpty
            ($explanation -match '[\p{L}]{3,}') | Should -BeTrue
        }
        $common = [Management.Automation.PSCmdlet]::CommonParameters + [Management.Automation.PSCmdlet]::OptionalCommonParameters
        foreach ($parameterName in $command.Parameters.Keys | Where-Object { $_ -notin $common }) {
            $entry = $help.Parameters.Parameter | Where-Object Name -EQ $parameterName
            ($entry.Description.Text -join '').Trim() | Should -Not -BeNullOrEmpty -Because "$parameterName needs authored help"
        }
    }
}
